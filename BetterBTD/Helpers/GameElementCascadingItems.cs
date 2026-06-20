using System.Collections.Generic;
using System.Linq;
using BetterBTD.Models.GameElements;
using BetterBTD.Services;
using Wpf.Ui.Violeta.Controls;

namespace BetterBTD.Helpers;

public static class GameElementCascadingItems
{
    private static string? _cachedMapItemsLanguageCode;
    private static IReadOnlyList<ICascadingItem>? _cachedMapItems;
    private static string? _cachedMonkeyTowerItemsLanguageCode;
    private static IReadOnlyList<ICascadingItem>? _cachedMonkeyTowerItems;

    public static IReadOnlyList<ICascadingItem> MapItems
    {
        get
        {
            var localization = LocalizationService.Instance;
            if (_cachedMapItems is null ||
                !string.Equals(_cachedMapItemsLanguageCode, localization.LanguageCode, System.StringComparison.OrdinalIgnoreCase))
            {
                _cachedMapItems = BuildMapItems(localization);
                _cachedMapItemsLanguageCode = localization.LanguageCode;
            }

            return _cachedMapItems;
        }
    }

    public static IReadOnlyList<ICascadingItem> MonkeyTowerItems
    {
        get
        {
            var localization = LocalizationService.Instance;
            if (_cachedMonkeyTowerItems is null ||
                !string.Equals(_cachedMonkeyTowerItemsLanguageCode, localization.LanguageCode, System.StringComparison.OrdinalIgnoreCase))
            {
                _cachedMonkeyTowerItems = BuildMonkeyTowerItems(localization);
                _cachedMonkeyTowerItemsLanguageCode = localization.LanguageCode;
            }

            return _cachedMonkeyTowerItems;
        }
    }

    private static IReadOnlyList<ICascadingItem> BuildMapItems(LocalizationService localization)
    {
        return GameElementCatalog.Maps
            .GroupBy(m => m.Tier)
            .Select(group => (ICascadingItem)new CascadingItem(
                localization.T($"GameElements.MapTier.{group.Key}"),
                group.Select(map => (ICascadingItem)new CascadingItem(localization.T(map.NameKey))
                {
                    Tag = map.Type
                }).ToList()))
            .ToList();
    }

    private static IReadOnlyList<ICascadingItem> BuildMonkeyTowerItems(LocalizationService localization)
    {
        var monkeyCategories = GameElementCatalog.MonkeyTowers
            .GroupBy(t => t.Category)
            .Select(group => (ICascadingItem)new CascadingItem(
                localization.T($"GameElements.MonkeyCategory.{group.Key}"),
                group.Select(tower => (ICascadingItem)new CascadingItem(localization.T(tower.NameKey))
                {
                    Tag = $"Tower:{tower.Type}"
                }).ToList()))
            .ToList();

        monkeyCategories.Add(new CascadingItem(
            localization.T("GameElements.MonkeyCategory.Hero"),
            GameElementCatalog.Heroes.Select(hero => (ICascadingItem)new CascadingItem(localization.T(hero.NameKey))
            {
                Tag = $"Hero:{hero.Type}"
            }).ToList()));

        return monkeyCategories;
    }
}
