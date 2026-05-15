namespace BetterBTD.Models.ScriptEditor;

public readonly record struct ScriptUpgradeLevelState(int Top, int Middle, int Bottom)
{
    public static ScriptUpgradeLevelState Empty { get; } = new(0, 0, 0);

    public int GetLevel(UpgradePathType upgradePath)
    {
        return upgradePath switch
        {
            UpgradePathType.Top => Top,
            UpgradePathType.Middle => Middle,
            UpgradePathType.Bottom => Bottom,
            _ => 0
        };
    }

    public ScriptUpgradeLevelState SetLevel(UpgradePathType upgradePath, int level)
    {
        var normalizedLevel = Math.Max(0, level);
        return upgradePath switch
        {
            UpgradePathType.Top => this with { Top = normalizedLevel },
            UpgradePathType.Middle => this with { Middle = normalizedLevel },
            UpgradePathType.Bottom => this with { Bottom = normalizedLevel },
            _ => this
        };
    }

    public ScriptUpgradeLevelState ApplyUpgrade(UpgradePathType upgradePath, int upgradeCount)
    {
        return SetLevel(upgradePath, GetLevel(upgradePath) + Math.Max(1, upgradeCount));
    }

    public string ToDisplayString()
    {
        return $"{Top}{Middle}{Bottom}";
    }
}
