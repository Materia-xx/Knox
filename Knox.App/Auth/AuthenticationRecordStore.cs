#if WINDOWS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;

namespace Knox;

internal sealed class AuthenticationRecordStore
{
    private readonly string _recordPath;

    public AuthenticationRecordStore(string connectionId, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            throw new ArgumentException("A connection ID is required.", nameof(connectionId));
        }

        var directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Knox",
            "Authentication");
        var fileName = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(connectionId))) + ".bin";
        _recordPath = Path.Combine(directory, fileName);
    }

    public AuthenticationRecord? Load(string clientId, string tenantId)
    {
        if (!File.Exists(_recordPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(_recordPath);
            var record = AuthenticationRecord.Deserialize(stream);
            return string.Equals(record.ClientId, clientId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                    ? record
                    : null;
        }
        catch (JsonException ex)
        {
            AuthLog.Error($"Authentication record '{_recordPath}'", ex);
            File.Delete(_recordPath);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            AuthLog.Error($"Authentication record '{_recordPath}'", ex);
            File.Delete(_recordPath);
            return null;
        }
    }

    public void Save(AuthenticationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var directory = Path.GetDirectoryName(_recordPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _recordPath + ".tmp";
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                record.Serialize(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _recordPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
#endif
