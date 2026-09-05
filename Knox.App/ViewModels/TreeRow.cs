using System;
using System.Threading.Tasks;
using Knox.App.Logic.Mvvm;
using Microsoft.Maui;

namespace Knox.App.ViewModels;

/// <summary>Kind of node a <see cref="TreeRow"/> represents.</summary>
public enum TreeRowKind
{
    Vault,
    Folder,
    Secret,
}

/// <summary>
/// One visible row in the flattened browse tree. We flatten the Vault/Folder/Secret
/// hierarchy into a single list (honoring each node's expanded state) instead of
/// using a nested Expander control - MAUI's built-in toolkit has no TreeView and
/// the CommunityToolkit Expander is a third-party package we avoid. Tapping an
/// expandable row toggles it and re-flattens; tapping a secret opens it.
/// </summary>
public sealed class TreeRow : ObservableObject
{
    private readonly Func<TreeRow, bool, Task>? _metadataToggle;
    private bool _isVaultMetadataEnabled = true;

    public TreeRow(
        TreeRowKind kind,
        int level,
        string text,
        bool isVaultMetadataEnabled = true,
        Func<TreeRow, bool, Task>? metadataToggle = null)
    {
        Kind = kind;
        Level = level;
        Text = text;
        _isVaultMetadataEnabled = isVaultMetadataEnabled;
        _metadataToggle = metadataToggle;
    }

    public TreeRowKind Kind { get; }
    public int Level { get; }
    public string Text { get; }

    public bool IsExpandable => Kind != TreeRowKind.Secret;
    public bool IsSecret => Kind == TreeRowKind.Secret;
    public bool IsVaultLoading { get; set; }
    public bool HasVaultLoadFailed { get; set; }
    public bool IsVault => Kind == TreeRowKind.Vault;

    public bool IsVaultMetadataEnabled
    {
        get => _isVaultMetadataEnabled;
        set
        {
            if (SetProperty(ref _isVaultMetadataEnabled, value) && _metadataToggle is not null)
            {
                _ = _metadataToggle(this, value);
            }
        }
    }

    /// <summary>Expand/collapse glyph for expandable rows (empty for secrets).</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>Left indent based on depth, bound by the row template.</summary>
    public Thickness Indent => new(Level * 18, 0, 0, 0);

    // Back-references used to drive toggling / navigation.
    public string VaultName { get; set; } = string.Empty;
    public string SecretName { get; set; } = string.Empty;
    public Logic.Models.VaultTreeNode? Vault { get; set; }
    public Logic.Models.FolderTreeNode? Folder { get; set; }
}
