using System;
using Knox.App.ViewModels;

namespace Knox.App.Views;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _vm;
    private bool _dismissed;

    public LockPage(LockViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Unlocked += OnUnlocked;
    }

    private async void OnUnlocked(object? sender, EventArgs e)
    {
        // Unlocked can be raised more than once (auto-prompt on appearing, the
        // retry button, and re-activation all call PromptAsync). Only pop once,
        // and only if this page is actually still on the modal stack — otherwise
        // PopModalAsync throws "Modal Stack is Empty".
        if (_dismissed)
        {
            return;
        }

        _dismissed = true;
        _vm.Unlocked -= OnUnlocked;

        var nav = Navigation;
        if (nav is not null && nav.ModalStack.Contains(this))
        {
            await nav.PopModalAsync();
        }
    }

    public Task<bool> PromptAsync() => _vm.PromptAsync();

    // Block Android hardware back from dismissing the lock screen.
    protected override bool OnBackButtonPressed() => true;
}
