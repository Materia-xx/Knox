namespace Knox.App;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();

		// Pre-fill the auth fields from shared settings if we've signed in before.
		try
		{
			var settings = Knox.KnoxSettings.Current;
			TenantEntry.Text = settings.TenantId;
			ClientEntry.Text = settings.ClientId;
		}
		catch
		{
			// Settings not available yet; leave the fields empty.
		}
	}

	private async void OnSignInClicked(object? sender, EventArgs e)
	{
		var tenantId = TenantEntry.Text?.Trim();
		var clientId = ClientEntry.Text?.Trim();

		if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId))
		{
			AuthStatusLabel.Text = "Enter both Tenant ID and Client ID first.";
			return;
		}

		SignInBtn.IsEnabled = false;
		AuthStatusLabel.Text = "Signing in\u2026";

		// Each platform needs its own redirect URI:
		//  - Windows (unpackaged): system-browser loopback -> http://localhost
		//  - Android: system browser (CustomTabs) returning via the
		//    msauth://<package>/<signing-cert-hash> redirect. This is keyed to
		//    the app's package + signing cert, NOT the client id, so the app
		//    stays generic: the user supplies any tenant/client id at runtime.
		//    The user must add this exact redirect URI to their own Entra app
		//    registration, so we log it (and show it on failure) below.
		string? redirectUri = null;
#if WINDOWS
		redirectUri = "http://localhost";
#elif ANDROID
		redirectUri = Knox.App.MsalAndroidRedirect.GetRedirectUri();
		Knox.AuthLog.Write($"Android redirect URI (register this in Entra): {redirectUri}");
#endif

		try
		{
			// Persist what we used so it's pre-filled next launch (shared settings).
			var settings = Knox.KnoxSettings.Current;
			settings.TenantId = tenantId;
			settings.ClientId = clientId;
			Knox.KnoxSettings.Save(settings);

			var vaults = await Knox.KeyVaultDiscovery.ListAccessibleVaultsAsync(
				clientId, tenantId, GetAuthParent, redirectUri);

			AuthStatusLabel.Text = vaults.Count == 0
				? "Signed in \u2014 but no Key Vaults were found for this account."
				: $"Signed in \u2014 {vaults.Count} Key Vault(s):\n" + string.Join("\n", vaults);
		}
		catch (Exception ex)
		{
			Knox.AuthLog.Error("OnSignInClicked", ex);
			AuthStatusLabel.Text = $"Sign-in failed: {ex.Message}";
#if ANDROID
			if (!string.IsNullOrEmpty(redirectUri))
			{
				AuthStatusLabel.Text +=
					$"\n\nIf this is a redirect error, add this redirect URI to your Entra app registration (Mobile/Android):\n{redirectUri}";
			}
#endif
		}
		finally
		{
			SignInBtn.IsEnabled = true;
		}
	}

	// Supplies the platform-specific parent for MSAL interactive auth.
	private object? GetAuthParent()
	{
#if ANDROID
		return Platform.CurrentActivity;
#elif WINDOWS
		var window = Application.Current?.Windows?.FirstOrDefault();
		if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
		{
			return WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
		}
		return null;
#else
		return null;
#endif
	}

	private void OnCheckCoreClicked(object? sender, EventArgs e)
	{
		try
		{
			// Exercise the shared Knox.Core library. KnoxSettings lives in the
			// netstandard2.0 Knox.Core project and is shared with the WPF app.
			var settings = Knox.KnoxSettings.Current;
			CoreStatusLabel.Text =
				$"Knox.Core loaded \u2014 IdleMinutesClose={settings.IdleMinutesClose}, " +
				$"ClientId={(string.IsNullOrWhiteSpace(settings.ClientId) ? "(unset)" : settings.ClientId)}";
		}
		catch (Exception ex)
		{
			CoreStatusLabel.Text = $"Knox.Core call failed: {ex.Message}";
		}
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Tapped {count} time \u2014 it works!";
		else
			CounterBtn.Text = $"Tapped {count} times \u2014 it works!";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
