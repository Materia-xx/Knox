using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Application = Android.App.Application;

namespace Knox.App;

/// <summary>
/// Android app-unlock using the in-SDK <see cref="KeyguardManager"/> confirm-device-credential
/// flow. Deliberately avoids AndroidX BiometricPrompt (a Google/Jetpack package) to honor the
/// Knox Microsoft-only dependency policy. The system credential screen still offers fingerprint
/// on devices that have one enrolled, falling back to the device PIN/pattern/password.
///
/// The confirm intent is started with <c>StartActivityForResult</c>; the result is delivered
/// asynchronously to <see cref="MainActivity.OnActivityResult"/>, which forwards it to
/// <see cref="HandleResult"/> to complete the awaiting task. See Knox.App/Auth/README.md.
/// </summary>
internal static class AndroidDeviceCredential
{
    /// <summary>Request code used for the confirm-device-credential activity result.</summary>
    internal const int RequestCode = 0x4B4E; // "KN"

    private static TaskCompletionSource<bool>? _pending;

    /// <summary>True when the device has a secure lock screen (PIN/pattern/password/biometric).</summary>
    internal static bool IsDeviceSecure()
    {
        var km = GetKeyguardManager();
        return km is not null && OperatingSystem.IsAndroidVersionAtLeast(23) && km.IsDeviceSecure;
    }

    /// <summary>
    /// Shows the system confirm-credential screen. Returns true if the user authenticated,
    /// false if they cancelled/failed or no activity/secure lock screen is available.
    /// </summary>
    internal static Task<bool> AuthenticateAsync(string title, string description)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var km = GetKeyguardManager();
        if (activity is null || km is null || !OperatingSystem.IsAndroidVersionAtLeast(23) || !km.IsDeviceSecure)
        {
            return Task.FromResult(false);
        }

#pragma warning disable CA1422 // CreateConfirmDeviceCredentialIntent is the no-package option; BiometricPrompt is a forbidden dependency.
        var intent = km.CreateConfirmDeviceCredentialIntent(title, description);
#pragma warning restore CA1422
        if (intent is null)
        {
            return Task.FromResult(false);
        }

        // Replace any stale pending prompt so we never leak an un-completed task.
        _pending?.TrySetResult(false);
        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        activity.StartActivityForResult(intent, RequestCode);
        return _pending.Task;
    }

    /// <summary>Completes the awaiting <see cref="AuthenticateAsync"/> from OnActivityResult.</summary>
    internal static bool HandleResult(int requestCode, Result resultCode)
    {
        if (requestCode != RequestCode)
        {
            return false;
        }

        var tcs = _pending;
        _pending = null;
        tcs?.TrySetResult(resultCode == Result.Ok);
        return true;
    }

    private static KeyguardManager? GetKeyguardManager()
    {
        var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? (Context?)Application.Context;
        return context?.GetSystemService(Context.KeyguardService) as KeyguardManager;
    }
}
