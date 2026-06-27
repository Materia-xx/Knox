namespace Knox.Android;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
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
