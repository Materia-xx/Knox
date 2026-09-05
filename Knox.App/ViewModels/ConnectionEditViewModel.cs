using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Models;
using Knox.App.Logic.Mvvm;
using Knox.App.Logic.Storage;
using Knox.App.Services;
using Microsoft.Maui.Controls;

namespace Knox.App.ViewModels;

/// <summary>
/// Add/edit form for a single <see cref="KnoxConnection"/>. Shows the platform
/// redirect URI the user must register in their own Entra app registration.
/// </summary>
[QueryProperty(nameof(ConnectionId), "id")]
public sealed class ConnectionEditViewModel : BaseViewModel
{
    private readonly ConnectionStore _store;
    private string? _connectionId;
    private string _name = string.Empty;
    private string _clientId = string.Empty;
    private string _tenantId = string.Empty;

    public ConnectionEditViewModel(ConnectionStore store)
    {
        _store = store;
        Title = "Connection";
        RedirectUri = RedirectUriProvider.GetForCurrentPlatform();
        PackageName = RedirectUriProvider.GetPackageName();
        SignatureHash = RedirectUriProvider.GetSignatureHash();
        HasAndroidPlatformInfo = !string.IsNullOrEmpty(PackageName);
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new AsyncRelayCommand(() => Shell.Current.GoToAsync(".."));
    }

    /// <summary>Set from the navigation query; empty/null means "add new".</summary>
    public string? ConnectionId
    {
        get => _connectionId;
        set
        {
            _connectionId = value;
            _ = LoadExistingAsync(value);
        }
    }

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) RaiseCanSave(); }
    }

    public string ClientId
    {
        get => _clientId;
        set { if (SetProperty(ref _clientId, value)) RaiseCanSave(); }
    }

    public string TenantId
    {
        get => _tenantId;
        set { if (SetProperty(ref _tenantId, value)) RaiseCanSave(); }
    }

    /// <summary>Redirect URI to register in Entra for this platform.</summary>
    public string RedirectUri { get; }

    /// <summary>App package name to register in the Entra Android platform config.</summary>
    public string PackageName { get; }

    /// <summary>Base64 SHA-1 signing-cert hash to register in the Entra Android platform config.</summary>
    public string SignatureHash { get; }

    /// <summary>True when package/signature apply (Android), so the UI can show that section.</summary>
    public bool HasAndroidPlatformInfo { get; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task LoadExistingAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var all = await _store.LoadAsync();
        var existing = all.Find(c => c.Id == id);
        if (existing != null)
        {
            _connectionId = existing.Id;
            Name = existing.Name;
            ClientId = existing.ClientId;
            TenantId = existing.TenantId;
            Title = "Edit connection";
        }
    }

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(TenantId);

    private void RaiseCanSave() => (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

    private async Task SaveAsync()
    {
        var connection = new KnoxConnection
        {
            Name = Name.Trim(),
            ClientId = ClientId.Trim(),
            TenantId = TenantId.Trim(),
        };
        if (!string.IsNullOrEmpty(_connectionId))
        {
            connection.Id = _connectionId;
        }

        await _store.AddOrUpdateAsync(connection);
        await Shell.Current.GoToAsync("..");
    }
}
