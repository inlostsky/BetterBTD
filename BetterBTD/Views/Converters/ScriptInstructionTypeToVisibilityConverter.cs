using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BetterBTD.Models.ScriptEditor;

namespace BetterBTD.Views.Converters;

[ValueConversion(typeof(ScriptCommandType), typeof(Visibility))]
public sealed class ScriptInstructionTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ScriptCommandType type || parameter is not string expected)
        {
            return Visibility.Collapsed;
        }

        return string.Equals(type.ToString(), expected, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
