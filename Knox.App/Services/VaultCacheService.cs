using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Knox.App.Logic.Models;
using Knox.Models;

namespace Knox.App.Services;

public enum VaultLoadState
{
    Disabled,
    Loading,
    Loaded,
    Failed,
}

public sealed record VaultLoadProgress(
    string? VaultName,
    VaultLoadState? State,
    int CompletedVaults,
    int TotalVaults);

/// <summary>
/// Lazily discovers Key Vaults for a connection and caches each vault's secret
/// metadata for browsing/search.
///
/// Design decision (differs from the WPF app): NOTHING happens at app launch. The
/// cache is only built when the user explicitly browses/searches, and the UI shows
/// a loading state while <see cref="RefreshAsync"/> runs. This avoids the WPF
/// app's startup hang.
///
/// Lives in Knox.App (not Knox.App.Logic) because it depends on MSAL
/// (MsalTokenCredential) and the Azure Key Vault SDK (KnoxVaultClient), which are
/// deliberately kept out of the pure, unit-tested logic library.
/// </summary>
public sealed class VaultCacheService : IDisposable
{
    private readonly Dictionary<string, KnoxVaultClient> _clients =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private List<SecretInfo> _secrets = new();
    private List<string> _failedVaults = new();
    private IReadOnlyDictionary<string, VaultLoadState> _vaultLoadStates =
        new Dictionary<string, VaultLoadState>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _disabledVaults =
        new(StringComparer.OrdinalIgnoreCase);
    private TokenCredential? _credential;
    private IDisposable? _credentialLifetime;

    /// <summary>
    /// True once vault discovery completes. Individual vaults may still be loading;
    /// use <see cref="GetVaultLoadState"/> to determine per-vault readiness.
    /// </summary>
    public bool IsLoaded { get; private set; }

    public IReadOnlyList<string> VaultNames { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<SecretInfo> Secrets => _secrets;

    public IReadOnlyList<string> LoadedVaultNames =>
        VaultNames.Where(name => GetVaultLoadState(name) == VaultLoadState.Loaded).ToArray();

    /// <summary>
    /// Vaults that were discovered but whose secret list could not be read (RBAC) or
    /// could not be fully enumerated after retries (throttling). Non-empty means the
    /// browse tree may be missing secrets, so the UI should warn and offer a refresh.
    /// </summary>
    public IReadOnlyList<string> FailedVaults => _failedVaults;

    /// <summary>The connection the current cache was built for (null when empty).</summary>
    public string? LoadedConnectionId { get; private set; }

    /// <summary>
    /// Signs in for the given connection (interactively if needed), discovers all
    /// accessible vaults, and (re)builds the secret metadata cache. Off-box; only
    /// call in response to an explicit user action.
    /// </summary>
    public async Task RefreshAsync(
        KnoxConnection connection,
        Func<object?>? parentWindowProvider,
        string? redirectUri,
        IEnumerable<string>? disabledVaultNames = null,
        IProgress<VaultLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Clear();

            // One credential is shared by ARM discovery and every vault client.
#if WINDOWS
            TokenCredential credential = new PersistentInteractiveBrowserTokenCredential(
                connection.Id, connection.ClientId, connection.TenantId);
            _credentialLifetime = (IDisposable)credential;
#else
            var credential = new MsalTokenCredential(
                connection.ClientId, connection.TenantId, parentWindowProvider, redirectUri);
#endif
            _credential = credential;

            var vaultNames = await KeyVaultDiscovery
                .ListAccessibleVaultsAsync(credential, cancellationToken)
                .ConfigureAwait(false);

            VaultNames = vaultNames;
            LoadedConnectionId = connection.Id;
            IsLoaded = true;
            _disabledVaults.Clear();
            if (disabledVaultNames is not null)
            {
                _disabledVaults.UnionWith(disabledVaultNames);
            }

            _vaultLoadStates = vaultNames.ToDictionary(
                name => name,
                name => _disabledVaults.Contains(name)
                    ? VaultLoadState.Disabled
                    : VaultLoadState.Loading,
                StringComparer.OrdinalIgnoreCase);
            progress?.Report(new VaultLoadProgress(null, null, 0, vaultNames.Count));

            var secrets = new List<SecretInfo>();
            var failed = new List<string>();
            var completedVaults = 0;
            foreach (var vaultName in vaultNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_disabledVaults.Contains(vaultName))
                {
                    completedVaults++;
                    progress?.Report(new VaultLoadProgress(
                        vaultName,
                        VaultLoadState.Disabled,
                        completedVaults,
                        vaultNames.Count));
                    continue;
                }

                try
                {
                    var (client, vaultSecrets) = LoadVault(vaultName, credential);

                    if (!_disabledVaults.Contains(vaultName))
                    {
                        _clients[vaultName] = client;
                        secrets.AddRange(vaultSecrets);
                        _secrets = secrets
                            .Where(secret => !_disabledVaults.Contains(secret.VaultName))
                            .ToList();
                        SetVaultLoadState(vaultName, VaultLoadState.Loaded);
                    }
                }
                catch (Exception ex)
                {
                    // A vault we can list but not read secrets from (RBAC), or one whose
                    // secret list couldn't be fully enumerated after retries (throttling),
                    // shouldn't abort the whole refresh; record it so the UI can warn.
                    if (!_disabledVaults.Contains(vaultName))
                    {
                        failed.Add(vaultName);
                        _failedVaults = new List<string>(failed);
                        SetVaultLoadState(vaultName, VaultLoadState.Failed);
                        AuthLog.Error($"VaultCache: reading '{vaultName}'", ex);
                    }
                }

                completedVaults++;
                progress?.Report(new VaultLoadProgress(
                    vaultName,
                    GetVaultLoadState(vaultName),
                    completedVaults,
                    vaultNames.Count));
            }

            _secrets = secrets
                .Where(secret => !_disabledVaults.Contains(secret.VaultName))
                .ToList();
            _failedVaults = failed
                .Where(name => !_disabledVaults.Contains(name))
                .ToList();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public VaultLoadState? GetVaultLoadState(string vaultName) =>
        _vaultLoadStates.TryGetValue(vaultName, out var state) ? state : null;

    public void DisableVault(string vaultName)
    {
        _disabledVaults.Add(vaultName);
        _clients.Remove(vaultName);
        _secrets = _secrets
            .Where(secret => !string.Equals(
                secret.VaultName,
                vaultName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        _failedVaults.RemoveAll(
            name => string.Equals(name, vaultName, StringComparison.OrdinalIgnoreCase));
        SetVaultLoadState(vaultName, VaultLoadState.Disabled);
    }

    public async Task<VaultLoadState> EnableVaultAsync(
        string vaultName,
        CancellationToken cancellationToken = default)
    {
        _disabledVaults.Remove(vaultName);
        SetVaultLoadState(vaultName, VaultLoadState.Loading);

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disabledVaults.Contains(vaultName))
            {
                return VaultLoadState.Disabled;
            }

            if (GetVaultLoadState(vaultName) == VaultLoadState.Loaded)
            {
                return VaultLoadState.Loaded;
            }

            if (_credential is null || !VaultNames.Contains(vaultName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The vault connection is not loaded.");
            }

            try
            {
                var (client, vaultSecrets) = LoadVault(vaultName, _credential);
                if (_disabledVaults.Contains(vaultName))
                {
                    return VaultLoadState.Disabled;
                }

                _clients[vaultName] = client;
                _secrets = _secrets
                    .Where(secret => !string.Equals(
                        secret.VaultName,
                        vaultName,
                        StringComparison.OrdinalIgnoreCase))
                    .Concat(vaultSecrets)
                    .ToList();
                _failedVaults.RemoveAll(
                    name => string.Equals(name, vaultName, StringComparison.OrdinalIgnoreCase));
                SetVaultLoadState(vaultName, VaultLoadState.Loaded);
                return VaultLoadState.Loaded;
            }
            catch (Exception ex)
            {
                if (!_failedVaults.Contains(vaultName, StringComparer.OrdinalIgnoreCase))
                {
                    _failedVaults.Add(vaultName);
                }

                SetVaultLoadState(vaultName, VaultLoadState.Failed);
                AuthLog.Error($"VaultCache: reading '{vaultName}'", ex);
                return VaultLoadState.Failed;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Gets the vault client for CRUD, or null if the vault isn't cached.</summary>
    public KnoxVaultClient? GetClient(string vaultName) =>
        _clients.TryGetValue(vaultName, out var client) ? client : null;

    /// <summary>
    /// Rebuilds the in-memory <see cref="SecretInfo"/> list from the (already
    /// signed-in) vault clients. Call after a CRUD op so the tree reflects changes
    /// without a full re-discovery/sign-in.
    /// </summary>
    public void RebuildSecretIndex()
    {
        var secrets = new List<SecretInfo>();
        foreach (var (vaultName, client) in _clients)
        {
            foreach (var kvp in client.AllSecretProperties)
            {
                var props = kvp.Value;
                var tags = props.Tags != null
                    ? new Dictionary<string, string>(props.Tags)
                    : new Dictionary<string, string>();
                secrets.Add(new SecretInfo(vaultName, kvp.Key, tags));
            }
        }

        _secrets = secrets;
    }

    public void Clear()
    {
        _clients.Clear();
        _credentialLifetime?.Dispose();
        _credentialLifetime = null;
        _credential = null;
        _secrets = new List<SecretInfo>();
        _failedVaults = new List<string>();
        _disabledVaults.Clear();
        _vaultLoadStates =
            new Dictionary<string, VaultLoadState>(StringComparer.OrdinalIgnoreCase);
        VaultNames = Array.Empty<string>();
        LoadedConnectionId = null;
        IsLoaded = false;
    }

    private void SetVaultLoadState(string vaultName, VaultLoadState state)
    {
        var states = new Dictionary<string, VaultLoadState>(
            _vaultLoadStates,
            StringComparer.OrdinalIgnoreCase)
        {
            [vaultName] = state,
        };
        _vaultLoadStates = states;
    }

    private static (KnoxVaultClient Client, List<SecretInfo> Secrets) LoadVault(
        string vaultName,
        TokenCredential credential)
    {
        var uri = new Uri($"https://{vaultName}.vault.azure.net/");
        var client = new KnoxVaultClient(uri, credential);
        var secrets = new List<SecretInfo>();
        foreach (var kvp in client.AllSecretProperties)
        {
            var props = kvp.Value;
            var tags = props.Tags != null
                ? new Dictionary<string, string>(props.Tags)
                : new Dictionary<string, string>();
            secrets.Add(new SecretInfo(vaultName, kvp.Key, tags));
        }

        return (client, secrets);
    }

    public void Dispose()
    {
        Clear();
        _refreshGate.Dispose();
    }
}
