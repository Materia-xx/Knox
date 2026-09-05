using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Knox.App.Logic.Models;

namespace Knox.App.Logic.Storage;

/// <summary>
/// Persists the user's list of <see cref="KnoxConnection"/> registrations as JSON
/// in an <see cref="ISecureStore"/>. This multi-connection support is MAUI-only;
/// the WPF app keeps its single client/tenant in Knox.Core's KnoxSettings.
/// </summary>
public sealed class ConnectionStore
{
    // Bump the key suffix if the serialized shape ever changes incompatibly.
    internal const string StorageKey = "knox.connections.v1";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ISecureStore _store;

    public ConnectionStore(ISecureStore store) => _store = store;

    /// <summary>Loads all saved connections (empty list when none/invalid).</summary>
    public async Task<List<KnoxConnection>> LoadAsync()
    {
        var json = await _store.GetAsync(StorageKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<KnoxConnection>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<KnoxConnection>>(json, JsonOptions)
                   ?? new List<KnoxConnection>();
        }
        catch (JsonException)
        {
            // Corrupt/unreadable payload -> start clean rather than crashing.
            return new List<KnoxConnection>();
        }
    }

    /// <summary>Overwrites the stored connection list.</summary>
    public Task SaveAsync(IEnumerable<KnoxConnection> connections)
    {
        var json = JsonSerializer.Serialize(connections.ToList(), JsonOptions);
        return _store.SetAsync(StorageKey, json);
    }

    /// <summary>Adds a new connection or updates the existing one with the same Id.</summary>
    public async Task AddOrUpdateAsync(KnoxConnection connection)
    {
        var all = await LoadAsync().ConfigureAwait(false);
        var index = all.FindIndex(c => c.Id == connection.Id);
        if (index >= 0)
        {
            all[index] = connection;
        }
        else
        {
            all.Add(connection);
        }

        await SaveAsync(all).ConfigureAwait(false);
    }

    /// <summary>Removes the connection with the given id, if present.</summary>
    public async Task DeleteAsync(string id)
    {
        var all = await LoadAsync().ConfigureAwait(false);
        if (all.RemoveAll(c => c.Id == id) > 0)
        {
            await SaveAsync(all).ConfigureAwait(false);
        }
    }
}
