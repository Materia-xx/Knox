using System;
using System.Threading.Tasks;

namespace Knox.App.Services;

/// <summary>Outcome of a biometric / device-credential prompt.</summary>
public enum BiometricOutcome
{
    /// <summary>User authenticated (biometric or device PIN/pattern fallback).</summary>
    Success,

    /// <summary>User was prompted but failed/cancelled. Keep the app locked.</summary>
    Failed,

    /// <summary>The device has no biometric/credential capability configured.</summary>
    Unavailable,

    /// <summary>Platform support isn't wired up yet (see Android TODO below).</summary>
    NotConfigured,
}

/// <summary>
/// Prompts for biometric / device-credential authentication to unlock the app
/// (bank-app style). Uses platform APIs directly to avoid a third-party package:
///   * Windows: Windows.Security.Credentials.UI.UserConsentVerifier (Microsoft, in-SDK).
///   * Android: in-SDK KeyguardManager confirm-device-credential (see
///     AndroidDeviceCredential). We deliberately do NOT use AndroidX BiometricPrompt
///     because androidx.biometric is a Google/Jetpack package, not Microsoft-authored,
///     which conflicts with the Knox Microsoft-only dependency policy. The system
///     credential screen still offers fingerprint when one is enrolled, falling back
///     to the device PIN/pattern/password.
/// </summary>
public sealed class BiometricService
{
    public async Task<BiometricOutcome> AuthenticateAsync(string reason)
    {
#if WINDOWS
        try
        {
            var availability = await Windows.Security.Credentials.UI.UserConsentVerifier
                .CheckAvailabilityAsync();
            if (availability != Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
            {
                return BiometricOutcome.Unavailable;
            }

            var result = await Windows.Security.Credentials.UI.UserConsentVerifier
                .RequestVerificationAsync(reason);
            return result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified
                ? BiometricOutcome.Success
                : BiometricOutcome.Failed;
        }
        catch (Exception ex)
        {
            // Unpackaged desktop apps can throw here (UserConsentVerifier historically
            // wants package identity / an HWND). Treat as unavailable so the app stays
            // usable; revisit if we ever ship a packaged (MSIX) Windows build.
            Knox.AuthLog.Error("Biometric(Windows)", ex);
            return BiometricOutcome.Unavailable;
        }
#elif ANDROID
        try
        {
            // No secure lock screen configured -> nothing to prompt with.
            if (!AndroidDeviceCredential.IsDeviceSecure())
            {
                return BiometricOutcome.Unavailable;
            }

            var ok = await AndroidDeviceCredential.AuthenticateAsync("Unlock Knox", reason);
            return ok ? BiometricOutcome.Success : BiometricOutcome.Failed;
        }
        catch (Exception ex)
        {
            Knox.AuthLog.Error("Biometric(Android)", ex);
            return BiometricOutcome.Unavailable;
        }
#else
        await Task.CompletedTask;
        return BiometricOutcome.Unavailable;
#endif
    }
}
