using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BetterBTD.Helpers;
using BetterBTD.Models.GameElements;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Views.Converters;

public sealed class MonkeyTowerTypeToCascadingItemConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selectionCode = value switch
        {
            string code => code,
            MonkeyTowerType towerType => $"Tower:{towerType}",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(selectionCode))
        {
            return null;
        }

        foreach (var category in GameElementCascadingItems.MonkeyTowerItems)
        {
            var found = category.Children?.FirstOrDefault(item => item.Tag is string candidate && string.Equals(candidate, selectionCode, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICascadingItem item && item.Tag is string selectionCode)
        {
            return selectionCode;
        }

        return Binding.DoNothing;
    }
}
