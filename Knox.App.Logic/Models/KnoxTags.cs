namespace Knox.App.Logic.Models;

/// <summary>
/// Well-known special tag names Knox uses to drive UI behavior. These match the
/// WPF app's conventions so both apps interpret the same vault data identically.
/// </summary>
public static class KnoxTags
{
    /// <summary>Virtual folder path; split on '/' and '\' to build the tree.</summary>
    public const string Folder = "Folder";

    /// <summary>Overrides the shown name (the real secret name stays the id).</summary>
    public const string DisplayName = "DisplayName";

    /// <summary>Characters a Folder tag value is split on (matches the WPF app).</summary>
    public static readonly char[] FolderSeparators = { '/', '\\' };
}
