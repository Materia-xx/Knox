using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Knox.App.Converters;

/// <summary>Returns the logical negation of a bound bool (for IsVisible toggles).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
