using System.Threading.Tasks;
using Knox.App.Logic.Storage;
using Microsoft.Maui.Storage;

namespace Knox.App.Services;

/// <summary>
/// <see cref="ISecureStore"/> backed by MAUI <see cref="SecureStorage"/>. Values are
/// encrypted at rest via the Android Keystore / Windows DPAPI credential locker.
/// Built-in to MAUI, so no extra NuGet package is needed (Knox dependency policy).
/// </summary>
public sealed class MauiSecureStore : ISecureStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public void Remove(string key) => SecureStorage.Default.Remove(key);
}
