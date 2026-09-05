namespace Knox.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Detail pages navigated to via GoToAsync (not flyout items).
		Routing.RegisterRoute(nameof(Views.ConnectionEditPage), typeof(Views.ConnectionEditPage));
		Routing.RegisterRoute(nameof(Views.SecretPage), typeof(Views.SecretPage));
	}
}
