using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BetterBTD.Helpers;
using BetterBTD.Models.GameElements;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Views.Converters;

public sealed class MonkeyTowerTypeToCascadingItemConverter : IValueConverter, IMultiValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ResolveCascadingItem(ResolveSelectionCode(value), GameElementCascadingItems.MonkeyTowerItems);
    }

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var selectionCode = values.Length > 0
            ? ResolveSelectionCode(values[0])
            : null;
        var items = values.Length > 1 && values[1] is IEnumerable<ICascadingItem> cascadingItems
            ? cascadingItems
            : GameElementCascadingItems.MonkeyTowerItems;

        return ResolveCascadingItem(selectionCode, items);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ICascadingItem item && item.Tag is string selectionCode)
        {
            return selectionCode;
        }

        return Binding.DoNothing;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        var selectionCode = value is ICascadingItem item && item.Tag is string code
            ? code
            : Binding.DoNothing;

        var result = targetTypes.Select(_ => Binding.DoNothing).ToArray();
        if (result.Length > 0)
        {
            result[0] = selectionCode;
        }

        return result;
    }

    private static string? ResolveSelectionCode(object? value)
    {
        return value switch
        {
            string code => code,
            MonkeyTowerType towerType => $"Tower:{towerType}",
            _ => null
        };
    }

    private static ICascadingItem? ResolveCascadingItem(string? selectionCode, IEnumerable<ICascadingItem> items)
    {
        if (string.IsNullOrWhiteSpace(selectionCode))
        {
            return null;
        }

        return items
            .SelectMany(category => category.Children ?? [])
            .FirstOrDefault(item =>
                item.Tag is string candidate &&
                string.Equals(candidate, selectionCode, StringComparison.OrdinalIgnoreCase));
    }
}
