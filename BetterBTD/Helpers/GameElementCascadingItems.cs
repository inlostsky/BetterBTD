using System.Collections.Generic;
using System.Linq;
using BetterBTD.Models.GameElements;
using BetterBTD.Services;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Helpers;

public static class GameElementCascadingItems
{
    public static IReadOnlyList<ICascadingItem> MapItems => BuildMapItems();

    public static IReadOnlyList<ICascadingItem> MonkeyTowerItems => BuildMonkeyTowerItems();

    private static IReadOnlyList<ICascadingItem> BuildMapItems()
    {
        var localization = LocalizationService.Instance;

        return GameElementCatalog.Maps
            .GroupBy(m => m.Tier)
            .Select(group => (ICascadingItem)new CascadingItem(
                localization.T($"GameElements.MapTier.{group.Key}"),
                group.Select(map => (ICascadingItem)new CascadingItem(localization.T(map.NameKey))
                {
                    Tag = map.Type
                })))
            .ToList();
    }

    private static IReadOnlyList<ICascadingItem> BuildMonkeyTowerItems()
    {
        var localization = LocalizationService.Instance;

        var monkeyCategories = GameElementCatalog.MonkeyTowers
            .GroupBy(t => t.Category)
            .Select(group => (ICascadingItem)new CascadingItem(
                localization.T($"GameElements.MonkeyCategory.{group.Key}"),
                group.Select(tower => (ICascadingItem)new CascadingItem(localization.T(tower.NameKey))
                {
                    Tag = $"Tower:{tower.Type}"
                })))
            .ToList();

        monkeyCategories.Add(new CascadingItem(
            localization.T("GameElements.MonkeyCategory.Hero"),
            GameElementCatalog.Heroes.Select(hero => (ICascadingItem)new CascadingItem(localization.T(hero.NameKey))
            {
                Tag = $"Hero:{hero.Type}"
            })));

        return monkeyCategories;
    }
}
