using System;
using System.Collections.Generic;
using System.Linq;

namespace Knox.App.Logic.Models;

/// <summary>
/// App-wide preferences for Knox.App. Distinct from the WPF app's KnoxSettings
/// (which is single-connection and lives in Knox.Core); these are MAUI-only and
/// include the mobile security features the desktop app doesn't have.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Auto-lock the app after this many minutes of inactivity. 0 disables.</summary>
    public int IdleMinutesLock { get; set; } = 5;

    /// <summary>When true, skips confirmation prompts (mirrors WPF SuppressWarnings).</summary>
    public bool SuppressWarnings { get; set; }

    /// <summary>
    /// Seconds after copying a secret before we attempt a safe clipboard clear.
    /// 0 disables auto-clear. Default ~2 minutes per the design decision.
    /// </summary>
    public int ClipboardClearSeconds { get; set; } = 120;

    /// <summary>
    /// When true, require biometric / device-credential auth to ENTER the app
    /// (bank-app style). Fallback to device PIN/pattern is handled per-platform.
    /// </summary>
    public bool RequireBiometricUnlock { get; set; } = true;

    /// <summary>Id of the connection last used, so the app can preselect it.</summary>
    public string? LastConnectionId { get; set; }

    /// <summary>
    /// Vault names whose metadata loading is disabled, grouped by connection id.
    /// Missing connections and vaults default to enabled.
    /// </summary>
    public Dictionary<string, List<string>> DisabledVaultsByConnection { get; set; } = new();

    public bool IsVaultMetadataEnabled(string connectionId, string vaultName) =>
        !GetDisabledVaultNames(connectionId).Contains(vaultName, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetDisabledVaultNames(string connectionId)
    {
        DisabledVaultsByConnection ??= new Dictionary<string, List<string>>();
        return DisabledVaultsByConnection.TryGetValue(connectionId, out var vaultNames) &&
               vaultNames is not null
            ? vaultNames
            : Array.Empty<string>();
    }

    public void SetVaultMetadataEnabled(string connectionId, string vaultName, bool enabled)
    {
        DisabledVaultsByConnection ??= new Dictionary<string, List<string>>();
        if (!DisabledVaultsByConnection.TryGetValue(connectionId, out var disabledVaults) ||
            disabledVaults is null)
        {
            disabledVaults = new List<string>();
            DisabledVaultsByConnection[connectionId] = disabledVaults;
        }

        disabledVaults.RemoveAll(
            name => string.Equals(name, vaultName, StringComparison.OrdinalIgnoreCase));
        if (!enabled)
        {
            disabledVaults.Add(vaultName);
        }

        if (disabledVaults.Count == 0)
        {
            DisabledVaultsByConnection.Remove(connectionId);
        }
    }
}
