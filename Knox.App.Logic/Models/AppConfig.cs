namespace Knox.App.Logic.Models;

/// <summary>
/// App-wide preferences for Knox.App. Distinct from the WPF app's KnoxSettings
/// (which is single-connection and lives in Knox.Core); these are MAUI-only and
/// include the mobile security features the desktop app doesn't have.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Auto-lock the app after this many minutes of inactivity. 0 disables.</summary>
    public int IdleMinutesLock { get; set; } = 5;

    /// <summary>When true, skips confirmation prompts (mirrors WPF SuppressWarnings).</summary>
    public bool SuppressWarnings { get; set; }

    /// <summary>
    /// Seconds after copying a secret before we attempt a safe clipboard clear.
    /// 0 disables auto-clear. Default ~2 minutes per the design decision.
    /// </summary>
    public int ClipboardClearSeconds { get; set; } = 120;

    /// <summary>
    /// When true, require biometric / device-credential auth to ENTER the app
    /// (bank-app style). Fallback to device PIN/pattern is handled per-platform.
    /// </summary>
    public bool RequireBiometricUnlock { get; set; } = true;

    /// <summary>Id of the connection last used, so the app can preselect it.</summary>
    public string? LastConnectionId { get; set; }
}
