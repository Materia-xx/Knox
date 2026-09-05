using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Mvvm;
using Knox.App.Services;

namespace Knox.App.ViewModels;

/// <summary>
/// Backs the biometric lock overlay. On appearing it prompts for biometric /
/// device-credential auth; on success it marks the session unlocked and the page
/// dismisses itself. On failure the user can retry.
/// </summary>
public sealed class LockViewModel : BaseViewModel
{
    private readonly BiometricService _biometric;
    private readonly AppSession _session;
    private string _status = string.Empty;

    public LockViewModel(BiometricService biometric, AppSession session)
    {
        _biometric = biometric;
        _session = session;
        Title = "Locked";
        UnlockCommand = new AsyncRelayCommand(PromptAsync);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand UnlockCommand { get; }

    /// <summary>Raised when the session becomes unlocked so the UI can dismiss.</summary>
    public event EventHandler? Unlocked;

    /// <summary>Prompts for biometrics. Returns true when the session is unlocked.</summary>
    public async Task<bool> PromptAsync()
    {
        Status = "Authenticating\u2026";
        var outcome = await _biometric.AuthenticateAsync("Unlock Knox");

        switch (outcome)
        {
            case BiometricOutcome.Success:
            case BiometricOutcome.Unavailable:   // device has no secure lock screen -> allow entry
            case BiometricOutcome.NotConfigured: // platform support absent -> fail-open
                _session.IsUnlocked = true;
                Unlocked?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                Status = "Authentication failed. Tap to try again.";
                return false;
        }
    }
}
