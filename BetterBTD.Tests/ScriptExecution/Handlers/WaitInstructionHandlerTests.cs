using BetterBTD.Core.ScriptExecution.Handlers;
using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Tests.TestDoubles;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Tests.ScriptExecution.Handlers;

public sealed class WaitInstructionHandlerTests
{
    [Fact]
    public async Task HandleAsync_CoordinateColor_AcceptsHashPrefixedRgbHex()
    {
        var gameStageState = new RecordingCoordinateColorGameStageStateService();
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = new RecordingScriptInputService(),
            GameStageState = gameStageState
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.Wait.ToString(),
            WaitMode = WaitModeType.CoordinateColor.ToString(),
            WaitColorHex = "#12AbEf",
            WaitColorCoordinateX = 321,
            WaitColorCoordinateY = 654,
            WaitColorTolerance = 7
        };

        var context = TestScriptExecutionContextFactory.Create(instruction, runtimeServices);
        var handler = new WaitInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(new WpfPoint(321, 654), gameStageState.LastCoordinate);
        Assert.Equal(0x12, gameStageState.LastExpectedR);
        Assert.Equal(0xAB, gameStageState.LastExpectedG);
        Assert.Equal(0xEF, gameStageState.LastExpectedB);
        Assert.Equal(7, gameStageState.LastTolerance);
        Assert.Equal(1, gameStageState.IsCoordinateColorMatchCallCount);
    }
}

internal sealed class RecordingCoordinateColorGameStageStateService : IGameStageStateService
{
    public bool IsAvailable => true;

    public int IsCoordinateColorMatchCallCount { get; private set; }

    public WpfPoint LastCoordinate { get; private set; }

    public int LastExpectedR { get; private set; }

    public int LastExpectedG { get; private set; }

    public int LastExpectedB { get; private set; }

    public int LastTolerance { get; private set; }

    public Task<bool?> GetIsInLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<bool?>(true);

    public Task<int?> GetGoldAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(0);

    public Task<int?> GetRoundAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(0);

    public Task<bool?> GetRightUpgradeVisibleAsync(CancellationToken cancellationToken = default) => Task.FromResult<bool?>(false);

    public Task<int?> GetRightTopUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<int?> GetRightMiddleUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<int?> GetRightBottomUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<bool?> GetLeftUpgradeVisibleAsync(CancellationToken cancellationToken = default) => Task.FromResult<bool?>(false);

    public Task<int?> GetLeftTopUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<int?> GetLeftMiddleUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<int?> GetLeftBottomUpgradeLevelAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public Task<bool?> GetIsPlacingMonkeyAsync(CancellationToken cancellationToken = default) => Task.FromResult<bool?>(false);

    public Task<bool?> GetCanPlaceHeroAsync(CancellationToken cancellationToken = default) => Task.FromResult<bool?>(false);

    public Task<bool> IsCoordinateColorMatchAsync(
        WpfPoint scriptCoordinate,
        int expectedR,
        int expectedG,
        int expectedB,
        int tolerance,
        CancellationToken cancellationToken = default)
    {
        IsCoordinateColorMatchCallCount++;
        LastCoordinate = scriptCoordinate;
        LastExpectedR = expectedR;
        LastExpectedG = expectedG;
        LastExpectedB = expectedB;
        LastTolerance = tolerance;
        return Task.FromResult(true);
    }

    public Task<string> GetStageTargetAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

    public Task<GameStageStateSnapshot?> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<GameStageStateSnapshot?>(new GameStageStateSnapshot());
}
