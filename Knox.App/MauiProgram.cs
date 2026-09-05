using Knox.App.Logic.Storage;
using Knox.App.Services;
using Knox.App.ViewModels;
using Knox.App.Views;
using Microsoft.Extensions.Logging;

namespace Knox.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		RegisterServices(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void RegisterServices(IServiceCollection services)
	{
		// Storage (SecureStorage-backed, encrypted).
		services.AddSingleton<ISecureStore, MauiSecureStore>();
		services.AddSingleton<ConnectionStore>();
		services.AddSingleton<AppConfigStore>();

		// Long-lived app services.
		services.AddSingleton<AppSession>();
		services.AddSingleton<VaultCacheService>();
		services.AddSingleton<ClipboardService>();
		services.AddSingleton<BiometricService>();
		services.AddSingleton<LockController>();

		// Shell.
		services.AddSingleton<AppShell>();

		// View-models (fresh per navigation).
		services.AddTransient<ConnectionsViewModel>();
		services.AddTransient<ConnectionEditViewModel>();
		services.AddTransient<BrowseViewModel>();
		services.AddTransient<SecretViewModel>();
		services.AddTransient<SettingsViewModel>();
		services.AddTransient<LockViewModel>();

		// Pages.
		services.AddTransient<ConnectionsPage>();
		services.AddTransient<ConnectionEditPage>();
		services.AddTransient<BrowsePage>();
		services.AddTransient<SecretPage>();
		services.AddTransient<SettingsPage>();
		services.AddTransient<LockPage>();
	}
}
