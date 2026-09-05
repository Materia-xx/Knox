using System.Threading.Tasks;
using Knox.App.Logic.Models;
using Knox.App.Logic.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Knox.App.Tests;

[TestClass]
public sealed class ConnectionStoreTests
{
    private static ConnectionStore NewStore() => new(new InMemorySecureStore());

    [TestMethod]
    public async Task Load_WhenEmpty_ReturnsEmptyList()
    {
        var store = NewStore();
        var connections = await store.LoadAsync();
        Assert.AreEqual(0, connections.Count);
    }

    [TestMethod]
    public async Task Save_ThenLoad_RoundTripsAllFields()
    {
        var store = NewStore();
        var conn = new KnoxConnection { Name = "Work", ClientId = "cid", TenantId = "tid" };

        await store.SaveAsync(new[] { conn });
        var loaded = await store.LoadAsync();

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(conn.Id, loaded[0].Id);
        Assert.AreEqual("Work", loaded[0].Name);
        Assert.AreEqual("cid", loaded[0].ClientId);
        Assert.AreEqual("tid", loaded[0].TenantId);
    }

    [TestMethod]
    public async Task AddOrUpdate_AddsNew_ThenUpdatesInPlace()
    {
        var store = NewStore();
        var conn = new KnoxConnection { Name = "Work", ClientId = "c1", TenantId = "t1" };

        await store.AddOrUpdateAsync(conn);
        Assert.AreEqual(1, (await store.LoadAsync()).Count);

        conn.Name = "Work Renamed";
        await store.AddOrUpdateAsync(conn);

        var loaded = await store.LoadAsync();
        Assert.AreEqual(1, loaded.Count, "updating should not add a duplicate");
        Assert.AreEqual("Work Renamed", loaded[0].Name);
    }

    [TestMethod]
    public async Task Delete_RemovesById()
    {
        var store = NewStore();
        var a = new KnoxConnection { Name = "A", ClientId = "c", TenantId = "t" };
        var b = new KnoxConnection { Name = "B", ClientId = "c", TenantId = "t" };
        await store.SaveAsync(new[] { a, b });

        await store.DeleteAsync(a.Id);

        var loaded = await store.LoadAsync();
        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(b.Id, loaded[0].Id);
    }

    [TestMethod]
    public async Task Load_WithCorruptJson_ReturnsEmptyListInsteadOfThrowing()
    {
        var backing = new InMemorySecureStore();
        await backing.SetAsync(ConnectionStore.StorageKey, "{ this is not valid json ]");
        var store = new ConnectionStore(backing);

        var loaded = await store.LoadAsync();

        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public void IsComplete_RequiresAllThreeFields()
    {
        Assert.IsFalse(new KnoxConnection { Name = "n", ClientId = "c" }.IsComplete);
        Assert.IsTrue(new KnoxConnection { Name = "n", ClientId = "c", TenantId = "t" }.IsComplete);
    }
}
