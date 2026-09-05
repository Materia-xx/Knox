using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Knox.App.Logic.Storage;

/// <summary>
/// Volatile in-memory <see cref="ISecureStore"/> for BVT tests. Never persists and
/// never touches the platform keystore, so it is safe to run off-box.
/// </summary>
public sealed class InMemorySecureStore : ISecureStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => _values.TryRemove(key, out _);
}
