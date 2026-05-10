using BetterBTD.Core.Config;
using BetterBTD.Core.ScriptExecution.Handlers;
using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Tests.TestDoubles;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Tests.ScriptExecution.Handlers;

public sealed class MonkeyPanelInteractionHandlerTests
{
    [Fact]
    public async Task SwitchMonkeyTarget_OpensPanelThenSwitchesAndClosesWithEscape()
    {
        var input = new RecordingScriptInputService();
        var gameStageState = new QueueGameStageStateService(
        [
            new GameStageStateSnapshot
            {
                RightUpgradePanel = new GameStageUpgradePanelState
                {
                    IsVisible = true
                }
            }
        ]);
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = input,
            GameStageState = gameStageState
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.SwitchMonkeyTarget.ToString(),
            TargetMonkeyBindingId = "dart-bind",
            SwitchDirection = SwitchDirectionType.Right.ToString(),
            SwitchCount = 1
        };

        var monkeyObjects = new[]
        {
            new ScriptMonkeyObjectDocument
            {
                BindingId = "dart-bind",
                ObjectId = "Tower:DartMonkey",
                SelectionCode = "DartMonkey",
                PlacementOrder = 1
            }
        };

        var context = TestScriptExecutionContextFactory.Create(instruction, runtimeServices, monkeyObjects);
        context.State.UpsertMonkeyState("dart-bind", "Tower:DartMonkey", "DartMonkey", 1).LastKnownCoordinate =
            new WpfPoint(120, 240);

        var handler = new SwitchMonkeyTargetInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(new WpfPoint(120, 240), click.Coordinate);
        Assert.Equal([KeyId.Tab, KeyId.Escape], input.PressedKeys);
        Assert.Equal(1, gameStageState.CaptureSnapshotCallCount);
    }

    [Fact]
    public async Task SetMonkeyAbility_OpensPanelThenUsesAbilityAndClosesWithEscape()
    {
        var input = new RecordingScriptInputService();
        var gameStageState = new QueueGameStageStateService(
        [
            new GameStageStateSnapshot
            {
                RightUpgradePanel = new GameStageUpgradePanelState
                {
                    IsVisible = true
                }
            }
        ]);
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = input,
            GameStageState = gameStageState
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.SetMonkeyAbility.ToString(),
            TargetMonkeyBindingId = "dart-bind",
            SelectedAbility = MonkeyAbilityType.Ability1.ToString(),
            RequiresAbilityCoordinate = false
        };

        var monkeyObjects = new[]
        {
            new ScriptMonkeyObjectDocument
            {
                BindingId = "dart-bind",
                ObjectId = "Tower:DartMonkey",
                SelectionCode = "DartMonkey",
                PlacementOrder = 1
            }
        };

        var context = TestScriptExecutionContextFactory.Create(instruction, runtimeServices, monkeyObjects);
        context.State.UpsertMonkeyState("dart-bind", "Tower:DartMonkey", "DartMonkey", 1).LastKnownCoordinate =
            new WpfPoint(120, 240);

        var handler = new SetMonkeyAbilityInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(new WpfPoint(120, 240), click.Coordinate);
        var abilityHotkey = Assert.Single(input.PressedHotkeys);
        Assert.Equal(KeyId.PageDown, abilityHotkey.Key);
        Assert.Equal([KeyId.Escape], input.PressedKeys);
        Assert.Equal(1, gameStageState.CaptureSnapshotCallCount);
    }
}
