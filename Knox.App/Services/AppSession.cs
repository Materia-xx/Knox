using Knox.App.Logic.Models;

namespace Knox.App.Services;

/// <summary>
/// Process-wide session state shared across pages (registered as a singleton):
/// the currently selected connection, the loaded app config, and the "unlocked"
/// flag used by the biometric app-lock gate.
/// </summary>
public sealed class AppSession
{
    /// <summary>The connection currently being browsed, if any.</summary>
    public KnoxConnection? CurrentConnection { get; set; }

    /// <summary>Cached app config; loaded at startup and after settings changes.</summary>
    public AppConfig Config { get; set; } = new();

    /// <summary>
    /// True once the user has passed the biometric/app-lock gate for this session.
    /// Reset when the app is backgrounded or idles out so re-entry re-prompts.
    /// </summary>
    public bool IsUnlocked { get; set; }
}
