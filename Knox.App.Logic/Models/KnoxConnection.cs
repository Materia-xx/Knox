using System;
using System.Text.Json.Serialization;

namespace Knox.App.Logic.Models;

/// <summary>
/// A single Entra registration the user has registered with the app. Knox.App is
/// NOT multi-tenant: each connection points at the user's OWN single-tenant Entra
/// app registration. Unlike the WPF app (one client/tenant in settings), the MAUI
/// app stores a LIST of these so the user can switch between directories/apps.
///
/// This type is MAUI-app-only and intentionally lives outside Knox.Core (the WPF
/// app stays single-connection).
/// </summary>
public sealed class KnoxConnection
{
    /// <summary>Stable id used as the dictionary/selection key. Generated once.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>User-facing label, e.g. "Work" or "Personal".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Entra application (client) id for the user's own registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Directory (tenant) id to authenticate against.</summary>
    public string TenantId { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(TenantId);
}
