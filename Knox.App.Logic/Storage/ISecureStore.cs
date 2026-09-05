using System.Threading.Tasks;

namespace Knox.App.Logic.Storage;

/// <summary>
/// Minimal async key/value store abstraction. The MAUI app backs this with
/// <c>Microsoft.Maui.Storage.SecureStorage</c> (encrypted via the Android Keystore
/// / Windows DPAPI, no extra package). Abstracting it keeps the connection/config
/// stores platform-agnostic and lets BVT tests use an in-memory implementation.
/// </summary>
public interface ISecureStore
{
    /// <summary>Returns the stored value, or null when the key is absent.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Stores (or overwrites) the value for the key.</summary>
    Task SetAsync(string key, string value);

    /// <summary>Removes the key. No-op when absent.</summary>
    void Remove(string key);
}
