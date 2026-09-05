using Knox.App.ViewModels;

namespace Knox.App.Views;

public partial class SecretPage : ContentPage
{
    public SecretPage(SecretViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
