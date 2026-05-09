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
    public async Task HandleAsync_HeroSelection_UsesGeneralHeroHotkeyAndUpdatesMonkeyState()
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
                new GameStageStateSnapshot { CanPlaceHero = true },
                new GameStageStateSnapshot { IsPlacingMonkey = false },
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

        var pressedHotkey = Assert.Single(input.PressedHotkeys);
        Assert.Equal(expectedHeroHotkey.Modifiers, pressedHotkey.Modifiers);
        Assert.Equal(expectedHeroHotkey.Key, pressedHotkey.Key);

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
}
