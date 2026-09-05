using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knox.App.Logic.Models;
using Knox.Models;

namespace Knox.App.Services;

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
public sealed class VaultCacheService
{
    private readonly Dictionary<string, KnoxVaultClient> _clients =
        new(StringComparer.OrdinalIgnoreCase);
    private List<SecretInfo> _secrets = new();
    private List<string> _failedVaults = new();

    public bool IsLoaded { get; private set; }

    public IReadOnlyList<string> VaultNames { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<SecretInfo> Secrets => _secrets;

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
        CancellationToken cancellationToken = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));

        Clear();

        // One credential shared by ARM discovery AND every vault client so the MSAL
        // token cache is reused (a single interactive sign-in for the whole refresh).
        var credential = new MsalTokenCredential(
            connection.ClientId, connection.TenantId, parentWindowProvider, redirectUri);

        var vaultNames = await KeyVaultDiscovery
            .ListAccessibleVaultsAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        var secrets = new List<SecretInfo>();
        var failed = new List<string>();
        foreach (var vaultName in vaultNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var uri = new Uri($"https://{vaultName}.vault.azure.net/");
                var client = new KnoxVaultClient(uri, credential);
                _clients[vaultName] = client;

                foreach (var kvp in client.AllSecretProperties)
                {
                    var props = kvp.Value;
                    var tags = props.Tags != null
                        ? new Dictionary<string, string>(props.Tags)
                        : new Dictionary<string, string>();
                    secrets.Add(new SecretInfo(vaultName, kvp.Key, tags));
                }
            }
            catch (Exception ex)
            {
                // A vault we can list but not read secrets from (RBAC), or one whose
                // secret list couldn't be fully enumerated after retries (throttling),
                // shouldn't abort the whole refresh; record it so the UI can warn.
                failed.Add(vaultName);
                AuthLog.Error($"VaultCache: reading '{vaultName}'", ex);
            }
        }

        VaultNames = vaultNames;
        _secrets = secrets;
        _failedVaults = failed;
        LoadedConnectionId = connection.Id;
        IsLoaded = true;
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
        _secrets = new List<SecretInfo>();
        _failedVaults = new List<string>();
        VaultNames = Array.Empty<string>();
        LoadedConnectionId = null;
        IsLoaded = false;
    }
}
