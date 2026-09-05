using System;
using System.Linq;
using System.Threading.Tasks;
using Knox.App.Logic.Storage;
using Knox.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Knox.App.Services;

/// <summary>
/// Coordinates the biometric app-lock: shows the lock overlay on launch, when the
/// app returns from the background, and after an idle timeout. Uses a modal
/// <see cref="LockPage"/> so sensitive content is covered while locked.
/// </summary>
public sealed class LockController
{
    /// <summary>
    /// When true, the biometric app-lock is skipped entirely: the session starts
    /// unlocked and never re-locks. This is enabled ONLY in the dedicated
    /// "DebugEmulator" build configuration (which defines the EMULATOR_BYPASS_LOCK
    /// symbol), so you can test app features on an emulator that has no screen lock.
    /// Regular Debug and Release builds always enforce the lock, on emulator and
    /// hardware alike.
    /// </summary>
#if EMULATOR_BYPASS_LOCK
    private static readonly bool BypassLock = true;
#else
    private static readonly bool BypassLock = false;
#endif

    private readonly IServiceProvider _services;
    private readonly AppSession _session;
    private readonly AppConfigStore _configStore;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _showing;

    public LockController(IServiceProvider services, AppSession session, AppConfigStore configStore)
    {
        _services = services;
        _session = session;
        _configStore = configStore;
    }

    /// <summary>Record user activity so the idle timer restarts.</summary>
    public void NoteActivity() => _lastActivityUtc = DateTime.UtcNow;

    /// <summary>Lock the session (e.g. on background); next check re-prompts.</summary>
    public void Relock()
    {
        if (BypassLock)
        {
            return; // emulator: stay unlocked while testing
        }

        _session.IsUnlocked = false;
    }

    /// <summary>
    /// If biometric unlock is required and the session isn't unlocked, show the
    /// lock overlay. Safe to call repeatedly (guards against double-show).
    /// </summary>
    public async Task EnsureUnlockedAsync()
    {
        if (BypassLock)
        {
            _session.IsUnlocked = true;
            return;
        }
        // Window.Activated can fire repeatedly and re-enter this before the first
        // PushModalAsync completes. Set the guard synchronously (before any await)
        // so a second call can't push a second LockPage.
        if (_showing || _session.IsUnlocked)
        {
            return;
        }

        _session.Config = await _configStore.LoadAsync();

        if (!_session.Config.RequireBiometricUnlock)
        {
            _session.IsUnlocked = true;
            return;
        }

        if (_session.IsUnlocked)
        {
            return;
        }

        var nav = Shell.Current?.Navigation;
        if (nav is null || nav.ModalStack.Any(p => p is LockPage))
        {
            return; // already showing
        }

        _showing = true;
        try
        {
            var page = _services.GetRequiredService<LockPage>();
            await nav.PushModalAsync(page, false);
            NoteActivity();
            await page.PromptAsync();
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>
    /// Idle check for the periodic timer: locks and re-prompts once the configured
    /// idle window elapses. No-op when idle-lock is disabled (0 minutes).
    /// </summary>
    public async Task CheckIdleAsync()
    {
        var minutes = _session.Config.IdleMinutesLock;
        if (minutes <= 0 || !_session.IsUnlocked)
        {
            return;
        }

        if (DateTime.UtcNow - _lastActivityUtc >= TimeSpan.FromMinutes(minutes))
        {
            Relock();
            await EnsureUnlockedAsync();
        }
    }
}
