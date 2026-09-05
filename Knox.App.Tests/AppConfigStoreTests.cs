using System.Threading.Tasks;
using Knox.App.Logic.Models;
using Knox.App.Logic.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Knox.App.Tests;

[TestClass]
public sealed class AppConfigStoreTests
{
    [TestMethod]
    public async Task Load_WhenEmpty_ReturnsDefaults()
    {
        var store = new AppConfigStore(new InMemorySecureStore());
        var config = await store.LoadAsync();

        Assert.AreEqual(5, config.IdleMinutesLock);
        Assert.AreEqual(120, config.ClipboardClearSeconds);
        Assert.IsTrue(config.RequireBiometricUnlock);
        Assert.IsFalse(config.SuppressWarnings);
        Assert.IsTrue(config.IsVaultMetadataEnabled("connection", "vault"));
    }

    [TestMethod]
    public async Task Save_ThenLoad_RoundTrips()
    {
        var store = new AppConfigStore(new InMemorySecureStore());
        var config = new AppConfig
        {
            IdleMinutesLock = 10,
            SuppressWarnings = true,
            ClipboardClearSeconds = 30,
            RequireBiometricUnlock = false,
            LastConnectionId = "abc",
        };
        config.SetVaultMetadataEnabled("abc", "vault-one", enabled: false);

        await store.SaveAsync(config);
        var loaded = await store.LoadAsync();

        Assert.AreEqual(10, loaded.IdleMinutesLock);
        Assert.IsTrue(loaded.SuppressWarnings);
        Assert.AreEqual(30, loaded.ClipboardClearSeconds);
        Assert.IsFalse(loaded.RequireBiometricUnlock);
        Assert.AreEqual("abc", loaded.LastConnectionId);
        Assert.IsFalse(loaded.IsVaultMetadataEnabled("abc", "vault-one"));
        Assert.IsTrue(loaded.IsVaultMetadataEnabled("abc", "vault-two"));
    }

    [TestMethod]
    public async Task Load_WithCorruptJson_ReturnsDefaults()
    {
        var backing = new InMemorySecureStore();
        await backing.SetAsync(AppConfigStore.StorageKey, "not json");
        var store = new AppConfigStore(backing);

        var config = await store.LoadAsync();

        Assert.AreEqual(5, config.IdleMinutesLock);
    }

    [TestMethod]
    public void VaultMetadataSetting_IsConnectionSpecific_AndDefaultsOn()
    {
        var config = new AppConfig();

        config.SetVaultMetadataEnabled("connection-a", "shared-vault", enabled: false);

        Assert.IsFalse(config.IsVaultMetadataEnabled("connection-a", "SHARED-VAULT"));
        Assert.IsTrue(config.IsVaultMetadataEnabled("connection-b", "shared-vault"));

        config.SetVaultMetadataEnabled("connection-a", "shared-vault", enabled: true);

        Assert.IsTrue(config.IsVaultMetadataEnabled("connection-a", "shared-vault"));
        Assert.AreEqual(0, config.DisabledVaultsByConnection.Count);
    }
}
