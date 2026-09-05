using Knox.App.ViewModels;

namespace Knox.App.Views;

public partial class ConnectionEditPage : ContentPage
{
    public ConnectionEditPage(ConnectionEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
