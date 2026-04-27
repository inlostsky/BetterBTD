using System;
using BetterBTD.Models.GameElements;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterBTD.Models.ScriptEditor;

public enum ScriptCommandType
{
    PlaceMonkey,
    UpgradeMonkey,
    SwitchMonkeyTarget,
    SetMonkeyAbility,
    SellMonkey,
    PlaceHeroInventory,
    ActivateAbility,
    NextRound
}

public enum UpgradePathType
{
    Top,
    Middle,
    Bottom
}

public enum SwitchDirectionType
{
    Right,
    Left
}

public enum MonkeyAbilityType
{
    Ability1,
    Ability2
}

public sealed class ScriptInstructionTemplate : ObservableObject
{
    private string _displayName = string.Empty;
    private string _description = string.Empty;

    public required ScriptCommandType Type { get; init; }

    public required string NameKey { get; init; }

    public required string DescriptionKey { get; init; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }
}

public sealed class ScriptInstructionInstance : ObservableObject
{
    private string _displayName = string.Empty;
    private string _description = string.Empty;
    private string _selectedMonkeyTower = "Tower:DartMonkey";
    private string _monkeyObjectId = string.Empty;
    private string _targetMonkeyObjectId = string.Empty;
    private string _selectedInventoryItem = string.Empty;
    private string _selectedActivatedAbility = string.Empty;
    private string _nextRoundAction = "PlayFastForward";
    private int _nextRoundSendCount = 1;
    private double _positionX;
    private double _positionY;
    private UpgradePathType _upgradePath = UpgradePathType.Top;
    private int _upgradeCount = 1;
    private SwitchDirectionType _switchDirection = SwitchDirectionType.Right;
    private int _switchCount = 1;
    private MonkeyAbilityType _selectedAbility = MonkeyAbilityType.Ability1;
    private bool _requiresAbilityCoordinate;
    private double _abilityCoordinateX;
    private double _abilityCoordinateY;
    private string _notes = string.Empty;

    public ScriptInstructionInstance(ScriptCommandType type, string nameKey, string descriptionKey)
    {
        Type = type;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
    }

    public ScriptCommandType Type { get; }

    public string NameKey { get; }

    public string DescriptionKey { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SelectedMonkeyTower
    {
        get => _selectedMonkeyTower;
        set => SetProperty(ref _selectedMonkeyTower, value);
    }

    public string MonkeyObjectId
    {
        get => _monkeyObjectId;
        set => SetProperty(ref _monkeyObjectId, value);
    }

    public string TargetMonkeyObjectId
    {
        get => _targetMonkeyObjectId;
        set
        {
            if (!SetProperty(ref _targetMonkeyObjectId, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowUpgradePathSelector));
        }
    }

    public string SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set => SetProperty(ref _selectedInventoryItem, value);
    }

    public string SelectedActivatedAbility
    {
        get => _selectedActivatedAbility;
        set => SetProperty(ref _selectedActivatedAbility, value);
    }

    public string NextRoundAction
    {
        get => _nextRoundAction;
        set
        {
            if (!SetProperty(ref _nextRoundAction, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowNextRoundSendCount));
        }
    }

    public bool ShowNextRoundSendCount =>
        Type == ScriptCommandType.NextRound && string.Equals(NextRoundAction, "SendNextRound", StringComparison.OrdinalIgnoreCase);

    public int NextRoundSendCount
    {
        get => _nextRoundSendCount;
        set => SetProperty(ref _nextRoundSendCount, value < 1 ? 1 : (value > 500 ? 500 : value));
    }

    public bool ShowUpgradePathSelector =>
        Type == ScriptCommandType.UpgradeMonkey && !TargetMonkeyObjectId.StartsWith("Hero:", StringComparison.OrdinalIgnoreCase);

    public double PositionX
    {
        get => _positionX;
        set => SetProperty(ref _positionX, value);
    }

    public double PositionY
    {
        get => _positionY;
        set => SetProperty(ref _positionY, value);
    }

    public UpgradePathType UpgradePath
    {
        get => _upgradePath;
        set => SetProperty(ref _upgradePath, value);
    }

    public int UpgradeCount
    {
        get => _upgradeCount;
        set => SetProperty(ref _upgradeCount, value < 1 ? 1 : (value > 5 ? 5 : value));
    }

    public SwitchDirectionType SwitchDirection
    {
        get => _switchDirection;
        set => SetProperty(ref _switchDirection, value);
    }

    public int SwitchCount
    {
        get => _switchCount;
        set => SetProperty(ref _switchCount, value < 1 ? 1 : (value > 3 ? 3 : value));
    }

    public MonkeyAbilityType SelectedAbility
    {
        get => _selectedAbility;
        set => SetProperty(ref _selectedAbility, value);
    }

    public bool RequiresAbilityCoordinate
    {
        get => _requiresAbilityCoordinate;
        set => SetProperty(ref _requiresAbilityCoordinate, value);
    }

    public double AbilityCoordinateX
    {
        get => _abilityCoordinateX;
        set => SetProperty(ref _abilityCoordinateX, value);
    }

    public double AbilityCoordinateY
    {
        get => _abilityCoordinateY;
        set => SetProperty(ref _abilityCoordinateY, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }
}
