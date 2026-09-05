using Knox.App.ViewModels;

namespace Knox.App.Views;

public partial class BrowsePage : ContentPage
{
    private readonly BrowseViewModel _vm;

    public BrowsePage(BrowseViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Let Shell finish its initial Windows navigation before redirecting to
        // the connections page when no connections have been configured yet.
        await Task.Yield();
        await _vm.OnAppearingAsync();
    }
}
