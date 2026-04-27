using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BetterBTD.Helpers;
using BetterBTD.Models.GameElements;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Views.Converters;

public sealed class MapTypeToCascadingItemConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not GameMapType mapType)
        {
            return null;
        }

        foreach (var tier in GameElementCascadingItems.MapItems)
        {
            var found = tier.Children?.FirstOrDefault(item => item.Tag is GameMapType candidate && candidate == mapType);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICascadingItem item && item.Tag is GameMapType mapType)
        {
            return mapType;
        }

        return Binding.DoNothing;
    }
}
