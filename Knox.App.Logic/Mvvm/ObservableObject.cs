using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Knox.App.Logic.Mvvm;

/// <summary>
/// Minimal hand-rolled MVVM base implementing <see cref="INotifyPropertyChanged"/>.
///
/// Deliberately NOT using CommunityToolkit.Mvvm: the Knox dependency policy is to
/// minimize packages and prefer built-in framework types. This gives us the MVVM
/// pattern (bindable properties + commands) with zero extra dependencies.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises
    /// <see cref="PropertyChanged"/> only when the value actually changed.
    /// Returns true when a change occurred.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
