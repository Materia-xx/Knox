using System.Collections.Generic;
using System.Collections.ObjectModel;
using Knox.App.Logic.Mvvm;

namespace Knox.App.Logic.Models;

/// <summary>
/// A secret as shown in the browse tree. Carries both the display name (possibly
/// the DisplayName tag) and the real secret name used for CRUD.
/// </summary>
public sealed class SecretTreeNode
{
    public SecretTreeNode(string vaultName, string secretName, string displayName)
    {
        VaultName = vaultName;
        SecretName = secretName;
        DisplayName = displayName;
    }

    public string VaultName { get; }
    public string SecretName { get; }
    public string DisplayName { get; }
}

/// <summary>
/// A virtual folder node (from the <c>Folder</c> tag). May contain sub-folders and
/// secrets. <see cref="IsExpanded"/> is bindable so the UI can remember expansion.
/// </summary>
public sealed class FolderTreeNode : ObservableObject
{
    private bool _isExpanded;

    public FolderTreeNode(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }

    /// <summary>Full path from the vault root, used as a stable expansion key.</summary>
    public string Path { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<FolderTreeNode> Folders { get; } = new();
    public ObservableCollection<SecretTreeNode> Secrets { get; } = new();
}

/// <summary>
/// A vault node: the top level of the browse tree. Contains virtual folders and
/// the vault's root-level (untagged) secrets.
/// </summary>
public sealed class VaultTreeNode : ObservableObject
{
    private bool _isExpanded;

    public VaultTreeNode(string vaultName)
    {
        VaultName = vaultName;
    }

    public string VaultName { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<FolderTreeNode> Folders { get; } = new();
    public ObservableCollection<SecretTreeNode> Secrets { get; } = new();
}
