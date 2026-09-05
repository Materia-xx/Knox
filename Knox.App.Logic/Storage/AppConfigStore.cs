using System.Text.Json;
using System.Threading.Tasks;
using Knox.App.Logic.Models;

namespace Knox.App.Logic.Storage;

/// <summary>
/// Persists <see cref="AppConfig"/> as JSON in an <see cref="ISecureStore"/>.
/// Returns defaults when nothing has been saved yet.
/// </summary>
public sealed class AppConfigStore
{
    internal const string StorageKey = "knox.appconfig.v1";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ISecureStore _store;

    public AppConfigStore(ISecureStore store) => _store = store;

    public async Task<AppConfig> LoadAsync()
    {
        var json = await _store.GetAsync(StorageKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
    }

    public Task SaveAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        return _store.SetAsync(StorageKey, json);
    }
}
