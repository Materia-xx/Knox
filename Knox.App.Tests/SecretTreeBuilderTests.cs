using System.Collections.Generic;
using System.Linq;
using Knox.App.Logic.Models;
using Knox.App.Logic.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Knox.App.Tests;

[TestClass]
public sealed class SecretTreeBuilderTests
{
    private static SecretInfo Secret(string vault, string name, params (string, string)[] tags)
    {
        var dict = tags.ToDictionary(t => t.Item1, t => t.Item2);
        return new SecretInfo(vault, name, dict);
    }

    [TestMethod]
    public void EmptyVault_StillProducesVaultNode()
    {
        var tree = SecretTreeBuilder.Build(new[] { "vaultA" }, new List<SecretInfo>());

        Assert.AreEqual(1, tree.Count);
        Assert.AreEqual("vaultA", tree[0].VaultName);
        Assert.AreEqual(0, tree[0].Secrets.Count);
        Assert.AreEqual(0, tree[0].Folders.Count);
    }

    [TestMethod]
    public void RootSecret_WithoutFolderTag_GoesUnderVault()
    {
        var secrets = new[] { Secret("v", "db-password") };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets);

        Assert.AreEqual(1, tree[0].Secrets.Count);
        Assert.AreEqual("db-password", tree[0].Secrets[0].SecretName);
        Assert.AreEqual(0, tree[0].Folders.Count);
    }

    [TestMethod]
    public void FolderTag_SplitsOnSlashAndBackslash_IntoNestedFolders()
    {
        var secrets = new[] { Secret("v", "s1", (KnoxTags.Folder, "prod/web\\api")) };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets);

        var prod = tree[0].Folders.Single();
        Assert.AreEqual("prod", prod.Name);
        var web = prod.Folders.Single();
        Assert.AreEqual("web", web.Name);
        var api = web.Folders.Single();
        Assert.AreEqual("api", api.Name);
        Assert.AreEqual("s1", api.Secrets.Single().SecretName);
    }

    [TestMethod]
    public void FolderPath_IsBuiltFromVaultRoot()
    {
        var secrets = new[] { Secret("v", "s1", (KnoxTags.Folder, "prod/web")) };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets);

        var prod = tree[0].Folders.Single();
        Assert.AreEqual("v/prod", prod.Path);
        Assert.AreEqual("v/prod/web", prod.Folders.Single().Path);
    }

    [TestMethod]
    public void SecretsSharingAFolder_AreGroupedTogether()
    {
        var secrets = new[]
        {
            Secret("v", "s1", (KnoxTags.Folder, "shared")),
            Secret("v", "s2", (KnoxTags.Folder, "shared")),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets);

        Assert.AreEqual(1, tree[0].Folders.Count);
        Assert.AreEqual(2, tree[0].Folders[0].Secrets.Count);
    }

    [TestMethod]
    public void DisplayNameTag_OverridesShownName_ButKeepsRealName()
    {
        var secrets = new[] { Secret("v", "guid-like-name", (KnoxTags.DisplayName, "Friendly Name")) };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets);

        var node = tree[0].Secrets.Single();
        Assert.AreEqual("Friendly Name", node.DisplayName);
        Assert.AreEqual("guid-like-name", node.SecretName);
    }

    [TestMethod]
    public void Search_MatchesSecretName_CaseInsensitive()
    {
        var secrets = new[]
        {
            Secret("v", "AzureConnectionString"),
            Secret("v", "GithubToken"),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "github");

        Assert.AreEqual(1, tree[0].Secrets.Count);
        Assert.AreEqual("GithubToken", tree[0].Secrets[0].SecretName);
    }

    [TestMethod]
    public void Search_MatchesTagValue()
    {
        var secrets = new[]
        {
            Secret("v", "s1", ("env", "production")),
            Secret("v", "s2", ("env", "staging")),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "production");

        Assert.AreEqual(1, tree[0].Secrets.Count);
        Assert.AreEqual("s1", tree[0].Secrets[0].SecretName);
    }

    [TestMethod]
    public void Search_NoMatches_LeavesVaultButNoSecrets()
    {
        var secrets = new[] { Secret("v", "s1") };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "zzz-nope");

        Assert.AreEqual(1, tree.Count);
        Assert.AreEqual(0, tree[0].Secrets.Count);
    }

    [TestMethod]
    public void Search_MatchesDisplayNameTag()
    {
        var secrets = new[]
        {
            Secret("v", "guid-1", (KnoxTags.DisplayName, "Production DB")),
            Secret("v", "guid-2", (KnoxTags.DisplayName, "Staging DB")),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "production");

        Assert.AreEqual(1, tree[0].Secrets.Count);
        Assert.AreEqual("Production DB", tree[0].Secrets[0].DisplayName);
    }

    [TestMethod]
    public void Search_TitleOnly_DoesNotMatchTagValues()
    {
        var secrets = new[]
        {
            Secret("v", "s1", ("env", "production")),
            Secret("v", "s2", ("env", "staging")),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "production", includeTags: false);

        Assert.AreEqual(0, tree[0].Secrets.Count);
    }

    [TestMethod]
    public void Search_TitleOnly_DoesNotMatchRealNameWhenDisplayNameDiffers()
    {
        var secrets = new[] { Secret("v", "github-token", (KnoxTags.DisplayName, "CI Credential")) };

        var titleOnly = SecretTreeBuilder.Build(new[] { "v" }, secrets, "github", includeTags: false);
        Assert.AreEqual(0, titleOnly[0].Secrets.Count);

        var withTags = SecretTreeBuilder.Build(new[] { "v" }, secrets, "github", includeTags: true);
        Assert.AreEqual(1, withTags[0].Secrets.Count);
    }

    [TestMethod]
    public void Search_TitleOnly_StillMatchesDisplayName()
    {
        var secrets = new[]
        {
            Secret("v", "guid-1", (KnoxTags.DisplayName, "Production DB")),
            Secret("v", "guid-2", (KnoxTags.DisplayName, "Staging DB")),
        };

        var tree = SecretTreeBuilder.Build(new[] { "v" }, secrets, "staging", includeTags: false);

        Assert.AreEqual(1, tree[0].Secrets.Count);
        Assert.AreEqual("Staging DB", tree[0].Secrets[0].DisplayName);
    }

    [TestMethod]
    public void Vaults_AreOrderedCaseInsensitively()
    {
        var tree = SecretTreeBuilder.Build(new[] { "zeta", "Alpha", "mid" }, new List<SecretInfo>());

        CollectionAssert.AreEqual(
            new[] { "Alpha", "mid", "zeta" },
            tree.Select(v => v.VaultName).ToArray());
    }
}
