using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Models;
using Knox.App.Logic.Mvvm;
using Knox.App.Logic.Services;
using Knox.App.Logic.Storage;
using Knox.App.Services;
using Microsoft.Maui.Controls;

namespace Knox.App.ViewModels;

/// <summary>
/// The main screen: lazily signs in for the selected connection, caches vault
/// secret metadata, and shows a searchable Vault -> Folder -> Secret expander tree.
///
/// Nothing loads until the user taps "Load" (or performs a search after loading),
/// so there is no startup hang. A loading state is shown while the cache builds.
/// </summary>
public sealed class BrowseViewModel : BaseViewModel
{
    private readonly VaultCacheService _cache;
    private readonly ConnectionStore _connectionStore;
    private readonly AppConfigStore _configStore;
    private readonly AppSession _session;

    private string _searchText = string.Empty;
    private bool _searchTags = false;
    private string _status = string.Empty;
    private bool _hasLoaded;
    private IReadOnlyList<VaultTreeNode> _tree = Array.Empty<VaultTreeNode>();
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;

    public BrowseViewModel(
        VaultCacheService cache,
        ConnectionStore connectionStore,
        AppConfigStore configStore,
        AppSession session)
    {
        _cache = cache;
        _connectionStore = connectionStore;
        _configStore = configStore;
        _session = session;
        Title = "Knox";

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RowTappedCommand = new AsyncRelayCommand<TreeRow>(OnRowTappedAsync);
        NewSecretCommand = new AsyncRelayCommand(NewSecretAsync);
        BrowseConnectionCommand = new AsyncRelayCommand<KnoxConnection>(BrowseConnectionAsync);
        ChangeConnectionCommand = new AsyncRelayCommand(ChangeConnectionAsync);
    }

    public ObservableCollection<TreeRow> Rows { get; } = new();

    /// <summary>Registered connections the user can tap to browse.</summary>
    public ObservableCollection<KnoxConnection> Connections { get; } = new();

    public string ConnectionName => _session.CurrentConnection?.Name ?? "(no connection)";

    public bool HasLoaded
    {
        get => _hasLoaded;
        private set => SetProperty(ref _hasLoaded, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value) && _cache.IsLoaded)
            {
                RebuildTree();
            }
        }
    }

    /// <summary>
    /// When true, search also matches tag values and the real secret name; when
    /// false (default), only the display title is searched. Toggling re-runs the
    /// current search immediately.
    /// </summary>
    public bool SearchTags
    {
        get => _searchTags;
        set
        {
            if (SetProperty(ref _searchTags, value) && _cache.IsLoaded &&
                !string.IsNullOrWhiteSpace(SearchText))
            {
                RebuildTree();
            }
        }
    }

    public ICommand LoadCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RowTappedCommand { get; }
    public ICommand NewSecretCommand { get; }

    /// <summary>Tap a connection in the list to sign in and browse it.</summary>
    public ICommand BrowseConnectionCommand { get; }

    /// <summary>Return to the connection list to pick a different connection.</summary>
    public ICommand ChangeConnectionCommand { get; }

    /// <summary>
    /// Called when the page appears. If the user hasn't registered any connections
    /// yet, redirect to the Connections page (Browse is meaningless without one).
    /// Otherwise show the connection list, or the cached tree if one is loaded.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        var connections = await _connectionStore.LoadAsync();
        if (connections.Count == 0)
        {
            await Shell.Current.GoToAsync($"//{nameof(Views.ConnectionsPage)}");
            return;
        }

        _session.Config = await _configStore.LoadAsync();

        // Refresh the tappable list (a connection may have been added/edited/removed).
        Connections.Clear();
        foreach (var c in connections)
        {
            Connections.Add(c);
        }

        OnPropertyChanged(nameof(ConnectionName));
        OnAppearing();
    }

    /// <summary>Sign in and load the tapped connection, then show its tree.</summary>
    private async Task BrowseConnectionAsync(KnoxConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        _session.CurrentConnection = connection;
        _session.Config.LastConnectionId = connection.Id;
        await _configStore.SaveAsync(_session.Config);
        OnPropertyChanged(nameof(ConnectionName));
        await LoadAsync();
    }

    /// <summary>Drop back to the connection list so the user can switch connections.</summary>
    private Task ChangeConnectionAsync()
    {
        _loadCancellation?.Cancel();
        _loadVersion++;
        HasLoaded = false;
        Rows.Clear();
        _tree = Array.Empty<VaultTreeNode>();
        Status = string.Empty;
        SearchText = string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>Refreshes header + tree if cached (no connection lookup).</summary>
    public void OnAppearing()
    {
        OnPropertyChanged(nameof(ConnectionName));
        if (_cache.IsLoaded &&
            _cache.LoadedConnectionId == _session.CurrentConnection?.Id)
        {
            HasLoaded = true;
            RebuildTree();
        }
        else
        {
            HasLoaded = false;
            Rows.Clear();
        }
    }

    private async Task LoadAsync()
    {
        var connection = _session.CurrentConnection;
        if (connection is null)
        {
            Status = "Pick a connection first (Connections tab).";
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        var loadVersion = ++_loadVersion;

        IsBusy = true;
        Status = "Signing in and loading vaults\u2026";
        HasLoaded = false;
        Rows.Clear();
        _tree = Array.Empty<VaultTreeNode>();
        try
        {
            _session.Config = await _configStore.LoadAsync();
            var progress = new Progress<VaultLoadProgress>(update =>
            {
                if (loadVersion != _loadVersion)
                {
                    return;
                }

                if (update.VaultName is null)
                {
                    HasLoaded = true;
                    IsBusy = false;
                }

                RebuildTree();
                UpdateLoadingStatus(update.CompletedVaults, update.TotalVaults);
            });

            await _cache.RefreshAsync(
                connection,
                PlatformAuthParent.Get,
                RedirectUriProvider.GetForCurrentPlatform(),
                _session.Config.GetDisabledVaultNames(connection.Id),
                progress,
                cancellationToken);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            HasLoaded = true;
            RebuildTree();
            if (_cache.VaultNames.Count == 0)
            {
                Status = "Signed in \u2014 no Key Vaults found for this account.";
            }
            else if (_cache.FailedVaults.Count > 0)
            {
                Status = $"{_cache.VaultNames.Count} vault(s), {_cache.Secrets.Count} secret(s). " +
                    $"\u26a0 {_cache.FailedVaults.Count} vault(s) may be incomplete " +
                    $"({string.Join(", ", _cache.FailedVaults)}) \u2014 tap Refresh to retry.";
            }
            else
            {
                Status = $"{_cache.VaultNames.Count} vault(s), {_cache.Secrets.Count} secret(s).";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A refresh or connection change superseded this load.
        }
        catch (Exception ex)
        {
            if (loadVersion != _loadVersion)
            {
                return;
            }

            Knox.AuthLog.Error("Browse.Load", ex);
            Status = $"Load failed: {ex.Message}";
            var shell = Shell.Current;
            if (shell is not null)
            {
                await shell.DisplayAlertAsync("Sign-in / load failed", ex.Message, "OK");
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsBusy = false;
            }
        }
    }

    private void UpdateLoadingStatus(int completedVaults, int totalVaults)
    {
        if (totalVaults == 0)
        {
            Status = "Signed in \u2014 no Key Vaults found for this account.";
            return;
        }

        var readyVaults = _cache.LoadedVaultNames.Count;
        var failedVaults = _cache.FailedVaults.Count;
        var disabledVaults = _cache.VaultNames.Count(
            name => _cache.GetVaultLoadState(name) == VaultLoadState.Disabled);
        var loadingVaults = totalVaults - completedVaults;
        Status = $"{readyVaults} of {totalVaults} vault(s) ready for search";
        if (loadingVaults > 0)
        {
            Status += $"; loading {loadingVaults}";
        }

        if (failedVaults > 0)
        {
            Status += $"; \u26a0 {failedVaults} failed";
        }

        if (disabledVaults > 0)
        {
            Status += $"; {disabledVaults} off";
        }

        Status += ".";
    }

    private void RebuildTree()
    {
        var searching = !string.IsNullOrWhiteSpace(SearchText);
        _tree = SecretTreeBuilder.Build(_cache.VaultNames, _cache.Secrets, SearchText, SearchTags);

        if (searching)
        {
            // Auto-expand matches so they're visible (mirrors the WPF app).
            foreach (var vault in _tree)
            {
                ExpandAll(vault);
            }
        }

        Flatten(_tree);
    }

    /// <summary>Rebuilds the flat, indented row list honoring each node's expansion.</summary>
    private void Flatten(IReadOnlyList<VaultTreeNode> tree)
    {
        Rows.Clear();
        foreach (var vault in tree)
        {
            var state = _cache.GetVaultLoadState(vault.VaultName);
            Rows.Add(new TreeRow(
                TreeRowKind.Vault,
                0,
                vault.VaultName,
                state != VaultLoadState.Disabled,
                SetVaultMetadataEnabledAsync)
            {
                Glyph = state is VaultLoadState.Loading or VaultLoadState.Disabled
                    ? string.Empty
                    : vault.IsExpanded ? "\u25be" : "\u25b8",
                VaultName = vault.VaultName,
                Vault = vault,
                IsVaultLoading = state == VaultLoadState.Loading,
                HasVaultLoadFailed = state == VaultLoadState.Failed,
            });

            if (vault.IsExpanded)
            {
                AddChildren(vault.Folders, vault.Secrets, 1, vault.VaultName);
            }
        }
    }

    private void AddChildren(
        IEnumerable<FolderTreeNode> folders,
        IEnumerable<SecretTreeNode> secrets,
        int level,
        string vaultName)
    {
        foreach (var folder in folders)
        {
            Rows.Add(new TreeRow(TreeRowKind.Folder, level, folder.Name)
            {
                Glyph = folder.IsExpanded ? "\u25be" : "\u25b8",
                VaultName = vaultName,
                Folder = folder,
            });

            if (folder.IsExpanded)
            {
                AddChildren(folder.Folders, folder.Secrets, level + 1, vaultName);
            }
        }

        foreach (var secret in secrets)
        {
            Rows.Add(new TreeRow(TreeRowKind.Secret, level, secret.DisplayName)
            {
                VaultName = secret.VaultName,
                SecretName = secret.SecretName,
            });
        }
    }

    private static void ExpandAll(VaultTreeNode vault)
    {
        vault.IsExpanded = true;
        foreach (var folder in vault.Folders)
        {
            ExpandAll(folder);
        }
    }

    private static void ExpandAll(FolderTreeNode folder)
    {
        folder.IsExpanded = true;
        foreach (var sub in folder.Folders)
        {
            ExpandAll(sub);
        }
    }

    private async Task OnRowTappedAsync(TreeRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.Kind == TreeRowKind.Vault && row.Vault != null)
        {
            if (row.IsVaultLoading)
            {
                Status = $"Loading metadata for {row.VaultName}\u2026";
                return;
            }

            row.Vault.IsExpanded = !row.Vault.IsExpanded;
            RebuildFlatFromCache();
        }
        else if (row.Kind == TreeRowKind.Folder && row.Folder != null)
        {
            row.Folder.IsExpanded = !row.Folder.IsExpanded;
            RebuildFlatFromCache();
        }
        else if (row.IsSecret)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(Views.SecretPage)}?vault={Uri.EscapeDataString(row.VaultName)}&name={Uri.EscapeDataString(row.SecretName)}");
        }
    }

    private async Task SetVaultMetadataEnabledAsync(TreeRow row, bool enabled)
    {
        var connection = _session.CurrentConnection;
        if (connection is null || row.Kind != TreeRowKind.Vault)
        {
            return;
        }

        try
        {
            _session.Config.SetVaultMetadataEnabled(connection.Id, row.VaultName, enabled);
            await _configStore.SaveAsync(_session.Config);

            if (!enabled)
            {
                _cache.DisableVault(row.VaultName);
                Status = $"{row.VaultName} metadata loading is off.";
                RebuildTree();
                return;
            }

            Status = $"Loading metadata for {row.VaultName}\u2026";
            RebuildTree();
            var state = await _cache.EnableVaultAsync(row.VaultName);
            Status = state == VaultLoadState.Loaded
                ? $"{row.VaultName} is ready for search."
                : $"Could not load metadata for {row.VaultName}. Tap Refresh to retry.";
            RebuildTree();
        }
        catch (Exception ex)
        {
            Knox.AuthLog.Error($"Browse.ToggleVault '{row.VaultName}'", ex);
            Status = $"Could not update {row.VaultName}: {ex.Message}";
            RebuildTree();
        }
    }

    // Re-flatten using current expansion state (no rebuild), so toggling one node
    // preserves the expansion of untouched branches.
    private void RebuildFlatFromCache() => Flatten(_tree);

    private async Task NewSecretAsync()
    {
        var loadedVaultNames = _cache.LoadedVaultNames;
        if (!_cache.IsLoaded || loadedVaultNames.Count == 0)
        {
            Status = "Wait for at least one vault to finish loading first.";
            return;
        }

        // If multiple vaults, ask which to create in; otherwise use the only one.
        string vaultName;
        if (loadedVaultNames.Count == 1)
        {
            vaultName = loadedVaultNames[0];
        }
        else
        {
            var chosen = await Shell.Current.DisplayActionSheetAsync(
                "Create secret in which vault?", "Cancel", null, loadedVaultNames.ToArray());
            if (string.IsNullOrEmpty(chosen) || chosen == "Cancel")
            {
                return;
            }

            vaultName = chosen;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(Views.SecretPage)}?vault={Uri.EscapeDataString(vaultName)}&new=true");
    }
}
