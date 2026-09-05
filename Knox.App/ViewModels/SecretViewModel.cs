using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Models;
using Knox.App.Logic.Mvvm;
using Knox.App.Services;
using Knox.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Knox.App.ViewModels;

/// <summary>
/// View / create / edit a single secret. Mirrors the WPF SecretWindow:
///   * Create mode: name + value.
///   * Edit mode: display name, value show/hide/copy, a dedicated Folder field,
///     and an editable tag list. A value change writes a new version.
/// The always-present <c>Folder</c> tag works around the Key Vault lib quirk where
/// a 0-tag update is ignored (see KnoxVaultClient.UpdateSecret).
/// </summary>
[QueryProperty(nameof(VaultName), "vault")]
[QueryProperty(nameof(SecretNameParam), "name")]
[QueryProperty(nameof(IsNewParam), "new")]
public sealed class SecretViewModel : BaseViewModel
{
    private readonly VaultCacheService _cache;
    private readonly ClipboardService _clipboard;
    private readonly AppSession _session;

    private string _vaultName = string.Empty;
    private string _secretName = string.Empty;
    private string _editableName = string.Empty;
    private string _value = string.Empty;
    private string _folder = string.Empty;
    private bool _isNew;
    private bool _isValueVisible;
    private bool _valueChanged;
    private string _status = string.Empty;

    public SecretViewModel(VaultCacheService cache, ClipboardService clipboard, AppSession session)
    {
        _cache = cache;
        _clipboard = clipboard;
        _session = session;

        ToggleValueCommand = new RelayCommand(() => IsValueVisible = !IsValueVisible);
        CopyValueCommand = new AsyncRelayCommand(CopyValueAsync);
        AddTagCommand = new RelayCommand(() => Tags.Add(new TagRow()));
        RemoveTagCommand = new RelayCommand<TagRow>(t => { if (t != null) Tags.Remove(t); });
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        OpenLinkCommand = new AsyncRelayCommand<TagRow>(OpenLinkAsync);
        CopyTagCommand = new AsyncRelayCommand<TagRow>(CopyTagAsync);
    }

    // --- Navigation query properties ---
    public string VaultName
    {
        get => _vaultName;
        set { _vaultName = Uri.UnescapeDataString(value ?? string.Empty); }
    }

    public string SecretNameParam
    {
        set
        {
            _secretName = Uri.UnescapeDataString(value ?? string.Empty);
            OnPropertyChanged(nameof(SecretNameCaption));
            if (!_isNew)
            {
                EditableName = _secretName;
            }
            _ = LoadAsync();
        }
    }

    public string IsNewParam
    {
        set => IsNew = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    // --- Bindable state ---
    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (SetProperty(ref _isNew, value))
            {
                OnPropertyChanged(nameof(IsExisting));
                OnPropertyChanged(nameof(NameLabel));
                OnPropertyChanged(nameof(NamePlaceholder));
                OnPropertyChanged(nameof(ShowSecretNameCaption));
                Title = value ? "New secret" : Title;
            }
        }
    }

    public bool IsExisting => !_isNew;

    public string NameLabel => _isNew ? "Name" : "Display Name";

    public string NamePlaceholder => _isNew ? "Secret name" : "Shown in the secret list";

    /// <summary>
    /// The single name field creates the immutable Key Vault name in create mode
    /// and edits the optional friendly display name in edit mode.
    /// </summary>
    public string EditableName
    {
        get => _editableName;
        set
        {
            if (SetProperty(ref _editableName, value) && !_isNew)
            {
                Title = EffectiveDisplayName;
                OnPropertyChanged(nameof(ShowSecretNameCaption));
            }
        }
    }

    public string SecretNameCaption => $"Secret Name: {_secretName}";

    public bool ShowSecretNameCaption =>
        !_isNew && !string.Equals(EffectiveDisplayName, _secretName, StringComparison.Ordinal);

    private string EffectiveDisplayName =>
        string.IsNullOrWhiteSpace(_editableName) ? _secretName : _editableName.Trim();

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                _valueChanged = true;
                OnPropertyChanged(nameof(DisplayValue));
            }
        }
    }

    public bool IsValueVisible
    {
        get => _isValueVisible;
        set
        {
            if (SetProperty(ref _isValueVisible, value))
            {
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(ShowHideText));
            }
        }
    }

    /// <summary>Masked or plain value depending on <see cref="IsValueVisible"/>.</summary>
    public string DisplayValue => _isValueVisible ? _value : new string('\u2022', Math.Min(_value.Length, 12));

    public string ShowHideText => _isValueVisible ? "Hide" : "Show";

    public string Folder
    {
        get => _folder;
        set => SetProperty(ref _folder, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ObservableCollection<TagRow> Tags { get; } = new();

    public ICommand ToggleValueCommand { get; }
    public ICommand CopyValueCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand CopyTagCommand { get; }

    private async Task LoadAsync()
    {
        if (_isNew || string.IsNullOrEmpty(_secretName))
        {
            Title = "New secret";
            return;
        }

        Title = _secretName;
        var client = _cache.GetClient(_vaultName);
        if (client is null)
        {
            Status = "Vault not loaded.";
            return;
        }

        IsBusy = true;
        try
        {
            var secret = await Task.Run(() => client.GetSecret(_secretName));
            Value = secret.Value ?? string.Empty;
            _valueChanged = false;

            var storedDisplayName = string.Empty;
            Tags.Clear();
            foreach (var tag in secret.Properties.Tags)
            {
                if (tag.Key == KnoxTags.Folder)
                {
                    Folder = tag.Value;
                }
                else if (tag.Key == KnoxTags.DisplayName)
                {
                    storedDisplayName = tag.Value;
                }
                else
                {
                    Tags.Add(new TagRow(tag.Key, tag.Value));
                }
            }

            EditableName = string.IsNullOrWhiteSpace(storedDisplayName)
                ? _secretName
                : storedDisplayName;
        }
        catch (Exception ex)
        {
            Knox.AuthLog.Error("Secret.Load", ex);
            Status = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CopyValueAsync()
    {
        if (string.IsNullOrEmpty(_value))
        {
            return;
        }

        await _clipboard.CopySecretAsync(_value, _session.Config.ClipboardClearSeconds);
        var secs = _session.Config.ClipboardClearSeconds;
        Status = secs > 0
            ? $"Copied. Clipboard auto-clears in {secs}s (if unchanged)."
            : "Copied.";
    }

    private Task OpenLinkAsync(TagRow? tag)
    {
        if (tag != null && tag.IsHttpLink)
        {
            return Launcher.Default.OpenAsync(tag.Value.Trim());
        }

        return Task.CompletedTask;
    }

    private async Task CopyTagAsync(TagRow? tag)
    {
        if (tag is null || string.IsNullOrEmpty(tag.Value))
        {
            return;
        }

        await _clipboard.CopySecretAsync(tag.Value, _session.Config.ClipboardClearSeconds);
        var secs = _session.Config.ClipboardClearSeconds;
        Status = secs > 0
            ? $"Copied tag value. Clipboard auto-clears in {secs}s (if unchanged)."
            : "Copied tag value.";
    }

    private async Task SaveAsync()
    {
        var client = _cache.GetClient(_vaultName);
        if (client is null)
        {
            Status = "Vault not loaded.";
            return;
        }

        var secretName = _isNew ? _editableName.Trim() : _secretName;
        if (string.IsNullOrWhiteSpace(secretName))
        {
            Status = "A secret name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_isNew)
            {
                await Task.Run(() => client.CreateSecret(secretName, _value));
                // Apply folder/displayname/tags on the freshly created secret.
                await Task.Run(() => client.UpdateSecret(secretName, false, string.Empty, BuildTagList()));
            }
            else
            {
                await Task.Run(() => client.UpdateSecret(_secretName, _valueChanged, _value, BuildTagList()));
            }

            _cache.RebuildSecretIndex();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Knox.AuthLog.Error("Secret.Save", ex);
            Status = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_isNew)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        var confirm = _session.Config.SuppressWarnings ||
            await Shell.Current.DisplayAlertAsync(
                "Delete secret",
                $"Delete '{_secretName}'? (Soft delete \u2014 purge from the Azure portal if needed.)",
                "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        var client = _cache.GetClient(_vaultName);
        if (client is null)
        {
            Status = "Vault not loaded.";
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => client.DeleteSecret(_secretName));
            _cache.RebuildSecretIndex();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Knox.AuthLog.Error("Secret.Delete", ex);
            Status = $"Delete failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Builds the tag list to persist: user tags + DisplayName (when set) + a
    /// Folder tag that is ALWAYS present (even empty) to satisfy the Key Vault
    /// "keep at least one tag" quirk.
    /// </summary>
    private List<SecretTag> BuildTagList()
    {
        var tags = new List<SecretTag>();
        foreach (var row in Tags)
        {
            if (!string.IsNullOrWhiteSpace(row.Name))
            {
                tags.Add(new SecretTag(row.Name.Trim(), row.Value ?? string.Empty));
            }
        }

        var displayName = _editableName.Trim();
        if (!_isNew &&
            !string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(displayName, _secretName, StringComparison.Ordinal))
        {
            tags.Add(new SecretTag(KnoxTags.DisplayName, displayName));
        }

        // Always include Folder last so there is >= 1 tag.
        tags.Add(new SecretTag(KnoxTags.Folder, _folder?.Trim() ?? string.Empty));
        return tags;
    }
}
