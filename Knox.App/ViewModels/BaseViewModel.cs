using Knox.App.Logic.Mvvm;

namespace Knox.App.ViewModels;

/// <summary>
/// Shared base for Knox.App view-models: adds a bindable <see cref="Title"/> and a
/// <see cref="IsBusy"/> flag (with its inverse <see cref="IsNotBusy"/>) for driving
/// loading indicators and disabling controls during async work.
/// </summary>
public abstract class BaseViewModel : ObservableObject
{
    private string _title = string.Empty;
    private bool _isBusy;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !_isBusy;
}
