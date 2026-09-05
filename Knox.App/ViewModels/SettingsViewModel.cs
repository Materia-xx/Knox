using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Mvvm;
using Knox.App.Logic.Storage;
using Knox.App.Services;

namespace Knox.App.ViewModels;

/// <summary>
/// Edits <see cref="Knox.App.Logic.Models.AppConfig"/>: idle-lock minutes, warning
/// suppression, clipboard auto-clear seconds, and the biometric-unlock toggle.
/// </summary>
public sealed class SettingsViewModel : BaseViewModel
{
    private readonly AppConfigStore _configStore;
    private readonly AppSession _session;

    private int _idleMinutesLock;
    private bool _suppressWarnings;
    private int _clipboardClearSeconds;
    private bool _requireBiometricUnlock;
    private string _status = string.Empty;

    public SettingsViewModel(AppConfigStore configStore, AppSession session)
    {
        _configStore = configStore;
        _session = session;
        Title = "Settings";
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public int IdleMinutesLock
    {
        get => _idleMinutesLock;
        set => SetProperty(ref _idleMinutesLock, value);
    }

    public bool SuppressWarnings
    {
        get => _suppressWarnings;
        set => SetProperty(ref _suppressWarnings, value);
    }

    public int ClipboardClearSeconds
    {
        get => _clipboardClearSeconds;
        set => SetProperty(ref _clipboardClearSeconds, value);
    }

    public bool RequireBiometricUnlock
    {
        get => _requireBiometricUnlock;
        set => SetProperty(ref _requireBiometricUnlock, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand SaveCommand { get; }

    public async Task LoadAsync()
    {
        var config = await _configStore.LoadAsync();
        _session.Config = config;
        IdleMinutesLock = config.IdleMinutesLock;
        SuppressWarnings = config.SuppressWarnings;
        ClipboardClearSeconds = config.ClipboardClearSeconds;
        RequireBiometricUnlock = config.RequireBiometricUnlock;
    }

    private async Task SaveAsync()
    {
        var config = _session.Config;
        config.IdleMinutesLock = IdleMinutesLock;
        config.SuppressWarnings = SuppressWarnings;
        config.ClipboardClearSeconds = ClipboardClearSeconds;
        config.RequireBiometricUnlock = RequireBiometricUnlock;
        await _configStore.SaveAsync(config);
        Status = "Saved.";
    }
}
