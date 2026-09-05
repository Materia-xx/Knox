using System;
using Knox.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Knox.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly LockController _lock;
	private IDispatcherTimer? _idleTimer;

	public App(IServiceProvider services, LockController lockController)
	{
		InitializeComponent();
		_services = services;
		_lock = lockController;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = _services.GetRequiredService<AppShell>();
		var window = new Window(shell);

#if WINDOWS
		window.Width = 440;
		window.Height = 720;
#endif

		// Lock on launch/return-to-foreground; relock when backgrounded so the
		// biometric gate re-prompts on the next activation.
		window.Activated += async (_, _) => await SafeEnsureUnlockedAsync();
		window.Resumed += async (_, _) => await SafeEnsureUnlockedAsync();
		window.Stopped += (_, _) => _lock.Relock();
		window.Deactivated += (_, _) => _lock.Relock();

		StartIdleTimer();

		return window;
	}

	private void StartIdleTimer()
	{
		if (_idleTimer is not null)
		{
			return;
		}

		_idleTimer = Dispatcher.CreateTimer();
		_idleTimer.Interval = TimeSpan.FromSeconds(30);
		_idleTimer.Tick += async (_, _) =>
		{
			try
			{
				await _lock.CheckIdleAsync();
			}
			catch
			{
				// Idle check is best-effort; never crash the app over it.
			}
		};
		_idleTimer.Start();
	}

	private async System.Threading.Tasks.Task SafeEnsureUnlockedAsync()
	{
		try
		{
			await _lock.EnsureUnlockedAsync();
		}
		catch
		{
			// Never block app startup if the lock overlay can't be shown.
		}
	}
}