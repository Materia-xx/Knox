using Knox.App.ViewModels;

namespace Knox.App.Views;

public partial class ConnectionsPage : ContentPage
{
    private readonly ConnectionsViewModel _vm;

    public ConnectionsPage(ConnectionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
