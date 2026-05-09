using BetterBTD.Core.Config;
using BetterBTD.Services;

namespace BetterBTD.Tests.TestDoubles;

internal sealed class KeyBindingOverrideScope : IDisposable
{
    private readonly HotkeyBinding _originalHeroBinding;
    private readonly Dictionary<string, HotkeyBinding> _originalTowerBindings;

    public KeyBindingOverrideScope(HotkeyBinding? heroBinding = null)
    {
        var keyBindings = ConfigurationService.Instance.Current.KeyBindings;
        _originalHeroBinding = Clone(keyBindings.General.Hero);
        _originalTowerBindings = new Dictionary<string, HotkeyBinding>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(TowerPlacementBindings.DartMonkey)] = Clone(keyBindings.TowerPlacement.DartMonkey),
            [nameof(TowerPlacementBindings.BoomerangMonkey)] = Clone(keyBindings.TowerPlacement.BoomerangMonkey),
            [nameof(TowerPlacementBindings.BombShooter)] = Clone(keyBindings.TowerPlacement.BombShooter),
            [nameof(TowerPlacementBindings.TackShooter)] = Clone(keyBindings.TowerPlacement.TackShooter)
        };

        if (heroBinding is not null)
        {
            keyBindings.General.Hero = Clone(heroBinding);
        }
    }

    public void Dispose()
    {
        var keyBindings = ConfigurationService.Instance.Current.KeyBindings;
        keyBindings.General.Hero = Clone(_originalHeroBinding);
        keyBindings.TowerPlacement.DartMonkey = Clone(_originalTowerBindings[nameof(TowerPlacementBindings.DartMonkey)]);
        keyBindings.TowerPlacement.BoomerangMonkey = Clone(_originalTowerBindings[nameof(TowerPlacementBindings.BoomerangMonkey)]);
        keyBindings.TowerPlacement.BombShooter = Clone(_originalTowerBindings[nameof(TowerPlacementBindings.BombShooter)]);
        keyBindings.TowerPlacement.TackShooter = Clone(_originalTowerBindings[nameof(TowerPlacementBindings.TackShooter)]);
    }

    private static HotkeyBinding Clone(HotkeyBinding binding)
    {
        return new HotkeyBinding
        {
            Modifiers = binding.Modifiers,
            Key = binding.Key
        };
    }
}
