namespace Knox.App.Services;

/// <summary>
/// Returns the MSAL redirect details the user must register in their own Entra app
/// registration, per platform. Centralizes the logic previously inlined in
/// MainPage. See Knox.App/Auth/README.md for the full Android background.
/// </summary>
public static class RedirectUriProvider
{
    public static string GetForCurrentPlatform()
    {
#if WINDOWS
        // Unpackaged Windows uses the system-browser loopback flow.
        return "http://localhost";
#elif ANDROID
        // Android: msauth://<package>/<signing-cert-hash>, computed at runtime.
        return Knox.App.MsalAndroidRedirect.GetRedirectUri();
#else
        return "http://localhost";
#endif
    }

    /// <summary>
    /// The app package name to register in the Entra Android platform config.
    /// Empty on non-Android platforms (where package/signature don't apply).
    /// </summary>
    public static string GetPackageName()
    {
#if ANDROID
        return Knox.App.MsalAndroidRedirect.GetPackageName();
#else
        return string.Empty;
#endif
    }

    /// <summary>
    /// The Base64 SHA-1 signing-cert hash to register in the Entra Android platform
    /// config. Empty on non-Android platforms.
    /// </summary>
    public static string GetSignatureHash()
    {
#if ANDROID
        return Knox.App.MsalAndroidRedirect.GetSignatureHashValue();
#else
        return string.Empty;
#endif
    }
}
