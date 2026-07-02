using System;
using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;

namespace Knox.App
{
    /// <summary>
    /// Computes the MSAL Android redirect URI for this app at runtime from the
    /// APK's own signing certificate, in the form:
    ///
    ///     msauth://&lt;package&gt;/&lt;base64(SHA-1(signing cert))&gt;
    ///
    /// This redirect is keyed to the app's package name + signing certificate,
    /// NOT to any client id, so Knox stays generic: an end user registers their
    /// own single-tenant Entra app and adds THIS redirect URI to it. The same
    /// value works for every client/tenant id the user wires up because it only
    /// depends on how the Knox APK itself is signed.
    ///
    /// Computing it at runtime (instead of hardcoding) means it automatically
    /// matches whichever keystore signed the build (debug today, a release
    /// keystore later), and lets us surface the exact value the user must
    /// register. The matching value must also be declared as a BrowserTabActivity
    /// intent-filter in AndroidManifest.xml (that one is necessarily static).
    /// </summary>
    internal static class MsalAndroidRedirect
    {
        // Fallback for the current debug keystore, used only if the runtime
        // signature lookup fails for some reason. Must match the manifest.
        private const string DebugFallback =
            "msauth://com.materia.knox/i1RyQCxx81KoXGthqy+pYMvLqjs=";

        public static string GetRedirectUri()
        {
            try
            {
                var ctx = Android.App.Application.Context;
                var pkg = ctx.PackageName ?? "com.materia.knox";
                var hash = GetSignatureHash(ctx, pkg);
                if (!string.IsNullOrEmpty(hash))
                {
                    return $"msauth://{pkg}/{hash}";
                }
            }
            catch (Exception ex)
            {
                AuthLog.Error("MsalAndroidRedirect.GetRedirectUri", ex);
            }

            return DebugFallback;
        }

        private static string? GetSignatureHash(Context ctx, string pkg)
        {
            var pm = ctx.PackageManager;
            if (pm == null) return null;

            byte[]? certBytes = null;

#pragma warning disable CA1416 // SigningInfo requires API 28+, which we target
            var info = pm.GetPackageInfo(pkg, PackageInfoFlags.SigningCertificates);
            var signers = info?.SigningInfo?.GetApkContentsSigners();
            if (signers != null && signers.Length > 0)
            {
                certBytes = signers[0].ToByteArray();
            }
#pragma warning restore CA1416

            if (certBytes == null) return null;

            using var sha1 = SHA1.Create();
            var digest = sha1.ComputeHash(certBytes);
            return Convert.ToBase64String(digest);
        }
    }
}
