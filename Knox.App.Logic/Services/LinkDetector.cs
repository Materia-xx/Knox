using System;

namespace Knox.App.Logic.Services;

/// <summary>
/// Detects tag values that are http/https links so the browse/secret UI can render
/// them as tappable links (a WPF-app TODO we fold into the MAUI app). Pure and
/// BVT-testable.
/// </summary>
public static class LinkDetector
{
    /// <summary>
    /// True when <paramref name="value"/> is a well-formed absolute http/https URL.
    /// </summary>
    public static bool IsHttpLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
