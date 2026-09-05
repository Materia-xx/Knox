using System;
using System.Collections.Generic;
using System.Linq;
using Knox.App.Logic.Models;

namespace Knox.App.Logic.Services;

/// <summary>
/// Builds the Vault -> virtual Folder -> Secret tree shown on the browse screen,
/// applying the same rules as the WPF app (Knox/MainWindow.xaml.cs):
///   * A vault node is emitted for every known vault, even with no matching secrets.
///   * The <c>Folder</c> tag defines a nested virtual-folder path (split on / and \).
///   * The <c>DisplayName</c> tag overrides the shown name (real name kept for CRUD).
///   * Search always matches the display title (case-insensitive); when
///     <c>includeTags</c> is set it also matches the real secret name and all tag
///     values.
///
/// This is pure and off-box, so it is covered by BVT tests.
/// </summary>
public static class SecretTreeBuilder
{
    public static IReadOnlyList<VaultTreeNode> Build(
        IEnumerable<string> vaultNames,
        IEnumerable<SecretInfo> secrets,
        string? searchTerm = null,
        bool includeTags = true)
    {
        var secretsByVault = secrets
            .GroupBy(s => s.VaultName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var term = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        var result = new List<VaultTreeNode>();

        foreach (var vaultName in vaultNames.Distinct(StringComparer.OrdinalIgnoreCase)
                                            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            var vaultNode = new VaultTreeNode(vaultName);

            if (secretsByVault.TryGetValue(vaultName, out var vaultSecrets))
            {
                foreach (var secret in vaultSecrets
                             .Where(s => Matches(s, term, includeTags))
                             .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    AddSecretToTree(vaultNode, secret);
                }
            }

            result.Add(vaultNode);
        }

        return result;
    }

    /// <summary>
    /// True when the secret should be shown for the given search term. The display
    /// title is always searched; the real secret name and tag values are searched
    /// only when <paramref name="includeTags"/> is true.
    /// </summary>
    public static bool Matches(SecretInfo secret, string? term, bool includeTags = true)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        if (Contains(secret.DisplayName, term))
        {
            return true;
        }

        if (!includeTags)
        {
            return false;
        }

        if (Contains(secret.SecretName, term))
        {
            return true;
        }

        foreach (var tagValue in secret.Tags.Values)
        {
            if (Contains(tagValue, term))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddSecretToTree(VaultTreeNode vaultNode, SecretInfo secret)
    {
        var targetFolders = vaultNode.Folders;
        var targetSecrets = vaultNode.Secrets;
        var pathSoFar = vaultNode.VaultName;

        foreach (var segment in secret.FolderSegments)
        {
            pathSoFar = pathSoFar + "/" + segment;
            var existing = targetFolders.FirstOrDefault(
                f => string.Equals(f.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new FolderTreeNode(segment, pathSoFar);
                targetFolders.Add(existing);
            }

            targetFolders = existing.Folders;
            targetSecrets = existing.Secrets;
        }

        targetSecrets.Add(new SecretTreeNode(secret.VaultName, secret.SecretName, secret.DisplayName));
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}
