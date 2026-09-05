using System;
using System.Collections.Generic;

namespace Knox.App.Logic.Models;

/// <summary>
/// A platform/SDK-agnostic snapshot of a Key Vault secret's metadata, used by the
/// pure tree/search logic. The MAUI app maps Azure's <c>SecretProperties</c> onto
/// this so <c>Knox.App.Logic</c> stays free of Azure SDK dependencies and remains
/// unit-testable off-box.
/// </summary>
public sealed class SecretInfo
{
    public SecretInfo(string vaultName, string secretName, IReadOnlyDictionary<string, string>? tags = null)
    {
        VaultName = vaultName;
        SecretName = secretName;
        Tags = tags ?? new Dictionary<string, string>();
    }

    public string VaultName { get; }

    /// <summary>The real Key Vault secret name (the id used for CRUD).</summary>
    public string SecretName { get; }

    /// <summary>Secret tags. Includes the special <c>Folder</c>/<c>DisplayName</c> tags.</summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

    /// <summary>
    /// The name to show in the UI: the <c>DisplayName</c> tag if present, else the
    /// real secret name (mirrors the WPF app's behavior).
    /// </summary>
    public string DisplayName =>
        Tags.TryGetValue(KnoxTags.DisplayName, out var dn) && !string.IsNullOrWhiteSpace(dn)
            ? dn
            : SecretName;

    /// <summary>
    /// The virtual folder path segments derived from the <c>Folder</c> tag, split
    /// on both <c>/</c> and <c>\</c> (mirrors the WPF app). Empty when untagged.
    /// </summary>
    public IReadOnlyList<string> FolderSegments
    {
        get
        {
            if (!Tags.TryGetValue(KnoxTags.Folder, out var folder) || string.IsNullOrWhiteSpace(folder))
            {
                return Array.Empty<string>();
            }

            return folder.Split(KnoxTags.FolderSeparators, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
