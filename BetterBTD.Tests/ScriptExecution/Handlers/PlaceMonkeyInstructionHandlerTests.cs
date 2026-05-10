using BetterBTD.Core.Config;
using BetterBTD.Core.ScriptExecution.Handlers;
using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Tests.TestDoubles;
using System.Windows.Input;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Tests.ScriptExecution.Handlers;

public sealed class PlaceMonkeyInstructionHandlerTests
{
    [Fact]
    public async Task HandleAsync_PlacementModeNotActiveYet_RepeatsHotkeyUntilPlacementModeAppears()
    {
        var input = new RecordingScriptInputService();
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = input,
            GameStageState = new QueueGameStageStateService(
            [
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = true },
                new GameStageStateSnapshot { IsPlacingMonkey = false }
            ])
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.PlaceMonkey.ToString(),
            SelectedMonkeyTower = "DartMonkey",
            MonkeyBindingId = "dart-bind",
            PositionX = 120,
            PositionY = 240
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
        var handler = new PlaceMonkeyInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(3, input.PressedHotkeys.Count);
        Assert.All(input.PressedHotkeys, hotkey => Assert.Equal(KeyId.Q, hotkey.Key));
        Assert.Equal(new[] { new WpfPoint(120, 240) }, input.MovedCoordinates);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(new WpfPoint(120, 240), click.Coordinate);
        Assert.Equal(1, click.ClickCount);
    }

    [Fact]
    public async Task HandleAsync_HeroSelection_IgnoresHeroAvailabilityCheckAndUsesHeroHotkey()
    {
        var expectedHeroHotkey = new HotkeyBinding
        {
            Modifiers = ModifierKeys.Alt,
            Key = KeyId.U
        };

        using var _ = new KeyBindingOverrideScope(expectedHeroHotkey);

        var input = new RecordingScriptInputService();
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = input,
            GameStageState = new QueueGameStageStateService(
            [
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { CanPlaceHero = false, IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = true },
                new GameStageStateSnapshot { IsPlacingMonkey = false }
            ])
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.PlaceMonkey.ToString(),
            SelectedMonkeyTower = "Hero:Geraldo",
            MonkeyBindingId = "hero-bind",
            PositionX = 120,
            PositionY = 240
        };

        var monkeyObjects = new[]
        {
            new ScriptMonkeyObjectDocument
            {
                BindingId = "hero-bind",
                ObjectId = "Hero:Geraldo",
                SelectionCode = "Hero:Geraldo",
                PlacementOrder = 1
            }
        };

        var context = TestScriptExecutionContextFactory.Create(instruction, runtimeServices, monkeyObjects);
        var handler = new PlaceMonkeyInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Single(input.PressedHotkeys);
        Assert.All(input.PressedHotkeys, pressedHotkey =>
        {
            Assert.Equal(expectedHeroHotkey.Modifiers, pressedHotkey.Modifiers);
            Assert.Equal(expectedHeroHotkey.Key, pressedHotkey.Key);
        });

        Assert.Equal(new[] { new WpfPoint(120, 240) }, input.MovedCoordinates);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(new WpfPoint(120, 240), click.Coordinate);
        Assert.Equal(1, click.ClickCount);

        Assert.True(context.State.TryGetMonkeyState("hero-bind", out var monkeyState));
        Assert.Equal("Hero:Geraldo", monkeyState.SelectionCode);
        Assert.Equal("Hero:Geraldo", monkeyState.ObjectId);
        Assert.NotNull(monkeyState.LastKnownCoordinate);
        Assert.Equal(120, monkeyState.LastKnownCoordinate!.Value.X);
        Assert.Equal(240, monkeyState.LastKnownCoordinate!.Value.Y);
    }

    [Fact]
    public async Task HandleAsync_PlacementDetectionDisabled_PressesAndClicksOnceWithoutStateVerification()
    {
        var input = new RecordingScriptInputService();
        var runtimeServices = new ScriptExecutionRuntimeServices
        {
            Capture = new NullScriptCaptureService(),
            Input = input,
            GameStageState = new QueueGameStageStateService(
            [
                new GameStageStateSnapshot { IsPlacingMonkey = false },
                new GameStageStateSnapshot { IsPlacingMonkey = false }
            ])
        };

        var instruction = new ScriptInstructionDocument
        {
            CommandType = ScriptCommandType.PlaceMonkey.ToString(),
            SelectedMonkeyTower = "DartMonkey",
            MonkeyBindingId = "dart-bind",
            MonkeyObjectId = "Tower:DartMonkey",
            PositionX = 120,
            PositionY = 240,
            PlacementDetectionEnabled = false,
            PlacementFailureAdjustmentEnabled = true
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
        var handler = new PlaceMonkeyInstructionHandler();

        await handler.HandleAsync(context, CancellationToken.None);

        Assert.Single(input.PressedHotkeys);
        Assert.Equal(KeyId.Q, input.PressedHotkeys[0].Key);
        Assert.Equal(new[] { new WpfPoint(120, 240) }, input.MovedCoordinates);

        var click = Assert.Single(input.Clicks);
        Assert.Equal(new WpfPoint(120, 240), click.Coordinate);
        Assert.Equal(1, click.ClickCount);

        Assert.True(context.State.TryGetMonkeyState("dart-bind", out var monkeyState));
        Assert.NotNull(monkeyState.LastKnownCoordinate);
        Assert.Equal(120, monkeyState.LastKnownCoordinate!.Value.X);
        Assert.Equal(240, monkeyState.LastKnownCoordinate!.Value.Y);
    }
}
