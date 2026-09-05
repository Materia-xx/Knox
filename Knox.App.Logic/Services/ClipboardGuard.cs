namespace Knox.App.Logic.Services;

/// <summary>
/// Pure decision logic for "safe" clipboard clearing.
///
/// Android has a single system clipboard (one primary clip); there is no way to
/// delete just one entry. To avoid wiping something the user copied AFTER a secret,
/// we remember the exact text we placed and only clear when the clipboard STILL
/// holds that same text. The platform layer marks the clip sensitive on Android 13+
/// (ClipDescription EXTRA_IS_SENSITIVE) and drives the timer; this class just makes
/// the keep-or-clear decision so it can be BVT-tested.
/// </summary>
public sealed class ClipboardGuard
{
    private string? _trackedValue;

    /// <summary>Whether a secret we copied is currently being tracked for clearing.</summary>
    public bool HasTrackedValue => _trackedValue != null;

    /// <summary>Record the exact text we just copied so we can safely clear it later.</summary>
    public void Track(string value) => _trackedValue = value;

    /// <summary>Forget any tracked value (e.g. after a successful clear).</summary>
    public void Reset() => _trackedValue = null;

    /// <summary>
    /// True only when we are tracking a value AND the clipboard still contains
    /// exactly that value. If the user copied something else in the meantime, we
    /// must NOT clear it.
    /// </summary>
    public bool ShouldClear(string? currentClipboardText) =>
        _trackedValue != null && string.Equals(currentClipboardText, _trackedValue, System.StringComparison.Ordinal);
}
