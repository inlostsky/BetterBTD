using BetterBTD.Helpers;
using BetterBTD.Views.Converters;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Tests.Services;

public sealed class GameElementCascadingItemsTests
{
    [Fact]
    public void MonkeyTowerItems_ReusesCurrentLanguageItemsAndExposesSelectionTags()
    {
        var first = GameElementCascadingItems.MonkeyTowerItems;
        var second = GameElementCascadingItems.MonkeyTowerItems;

        Assert.Same(first, second);
        Assert.Contains(first, category =>
            category.Children?.Any(child => string.Equals(child.Tag as string, "Tower:DartMonkey", StringComparison.Ordinal)) == true);
    }

    [Fact]
    public void MonkeyTowerConverter_UsesProvidedItemsSourceForSelectedCascadingItem()
    {
        var items = GameElementCascadingItems.MonkeyTowerItems;
        var expected = items
            .SelectMany(category => category.Children ?? [])
            .Single(child => string.Equals(child.Tag as string, "Tower:DartMonkey", StringComparison.Ordinal));
        var converter = new MonkeyTowerTypeToCascadingItemConverter();

        var converted = converter.Convert(
            ["Tower:DartMonkey", items],
            typeof(ICascadingItem),
            null,
            culture: null!);

        Assert.Same(expected, converted);
    }
}
