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

        await store.SaveAsync(config);
        var loaded = await store.LoadAsync();

        Assert.AreEqual(10, loaded.IdleMinutesLock);
        Assert.IsTrue(loaded.SuppressWarnings);
        Assert.AreEqual(30, loaded.ClipboardClearSeconds);
        Assert.IsFalse(loaded.RequireBiometricUnlock);
        Assert.AreEqual("abc", loaded.LastConnectionId);
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
}
