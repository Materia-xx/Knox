using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Identity.Client;

namespace Knox.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);

		// Forward MSAL's interactive-auth result from its AuthenticationActivity
		// back into MSAL so the awaiting AcquireTokenInteractive call completes.
		// MSAL starts AuthenticationActivity via StartActivityForResult from this
		// activity; MAUI doesn't wire this up automatically. Without it, sign-in
		// hangs on "Signing in..." even though the browser redirect returned.
		AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);

		// Forward the app-unlock (confirm-device-credential) result to complete the
		// awaiting BiometricService.AuthenticateAsync task. Same reason as MSAL:
		// StartActivityForResult results aren't surfaced to MAUI automatically.
		AndroidDeviceCredential.HandleResult(requestCode, resultCode);
	}
}
