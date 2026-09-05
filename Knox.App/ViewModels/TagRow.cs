using Knox.App.Logic.Mvvm;
using Knox.App.Logic.Services;

namespace Knox.App.ViewModels;

/// <summary>
/// A bindable name/value tag row used by the secret editor's tag list. Exposes
/// <see cref="IsHttpLink"/> so the UI can render http/https values as tappable links.
/// </summary>
public sealed class TagRow : ObservableObject
{
    private string _name = string.Empty;
    private string _value = string.Empty;

    public TagRow(string name = "", string value = "")
    {
        _name = name;
        _value = value;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(IsHttpLink));
            }
        }
    }

    public bool IsHttpLink => LinkDetector.IsHttpLink(_value);
}
