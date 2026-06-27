namespace Knox.App;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
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
