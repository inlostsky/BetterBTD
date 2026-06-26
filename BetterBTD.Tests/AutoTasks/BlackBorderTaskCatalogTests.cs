using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;

namespace BetterBTD.Tests.AutoTasks;

public sealed class BlackBorderTaskCatalogTests
{
    [Fact]
    public void GetModesForDifficulty_Easy_UsesUnlockTopologyOrder()
    {
        Assert.Equal(
            [
                StageMode.Standard,
                StageMode.PrimaryOnly,
                StageMode.Deflation
            ],
            BlackBorderTaskCatalog.GetModesForDifficulty(StageDifficulty.Easy));
    }

    [Fact]
    public void GetModesForDifficulty_Medium_UsesUnlockTopologyOrder()
    {
        Assert.Equal(
            [
                StageMode.Standard,
                StageMode.MilitaryOnly,
                StageMode.Apopalypse,
                StageMode.Reverse
            ],
            BlackBorderTaskCatalog.GetModesForDifficulty(StageDifficulty.Medium));
    }

    [Fact]
    public void GetModesForDifficulty_Hard_UsesUnlockTopologyOrder()
    {
        Assert.Equal(
            [
                StageMode.Standard,
                StageMode.MagicOnly,
                StageMode.DoubleHpMoabs,
                StageMode.HalfCash,
                StageMode.AlternateBloonsRounds,
                StageMode.Impoppable,
                StageMode.CHIMPS
            ],
            BlackBorderTaskCatalog.GetModesForDifficulty(StageDifficulty.Hard));
    }
}
