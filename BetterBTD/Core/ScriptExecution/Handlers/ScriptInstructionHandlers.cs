using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Models.GameElements;
using BetterBTD.Core.ScriptExecution;
using BetterBTD.Core.Config;
using BetterBTD.Services;
using OpenCvSharp;
using System.Windows.Input;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public interface IScriptInstructionHandler
{
    ScriptCommandType CommandType { get; }

    Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}

public abstract class ScriptInstructionHandlerBase : IScriptInstructionHandler
{
    public abstract ScriptCommandType CommandType { get; }

    public abstract Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}

public sealed class ScriptInstructionHandlerRegistry
{
    private static readonly Lazy<ScriptInstructionHandlerRegistry> InstanceHolder = new(() => new ScriptInstructionHandlerRegistry());

    private readonly Dictionary<ScriptCommandType, IScriptInstructionHandler> _handlers = [];

    private ScriptInstructionHandlerRegistry()
    {
        Register(new PlaceMonkeyInstructionHandler());
        Register(new UpgradeMonkeyInstructionHandler());
        Register(new SwitchMonkeyTargetInstructionHandler());
        Register(new SetMonkeyAbilityInstructionHandler());
        Register(new SellMonkeyInstructionHandler());
        Register(new PlaceHeroInventoryInstructionHandler());
        Register(new ActivateAbilityInstructionHandler());
        Register(new MouseClickInstructionHandler());
        Register(new NextRoundInstructionHandler());
        Register(new WaitInstructionHandler());
        Register(new ModifyMonkeyCoordinateInstructionHandler());
        Register(new CommentInstructionHandler());
    }

    public static ScriptInstructionHandlerRegistry Instance => InstanceHolder.Value;

    public void Register(IScriptInstructionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.CommandType] = handler;
    }

    public IScriptInstructionHandler GetRequiredHandler(ScriptCommandType commandType)
    {
        if (_handlers.TryGetValue(commandType, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No instruction handler was registered for '{commandType}'.");
    }
}

public sealed class PlaceMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.PlaceMonkey;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        var selectionCode = ScriptEditorInstructionService.NormalizePlaceSelectionCode(instruction.SelectedMonkeyTower);
        var requestedCoordinate = new WpfPoint(instruction.PositionX, instruction.PositionY);
        var placementHotkey = ScriptExecutionKeyBindingResolver.ResolvePlacementHotkey(selectionCode);

        await ScriptInstructionHandlerSupport.CancelPlacementModeIfActiveAsync(context, cancellationToken).ConfigureAwait(false);

        if (ScriptEditorInstructionService.TryParseHeroSelection(selectionCode, out _))
        {
            var precheckSnapshot = await ScriptExecutionOperations
                .CaptureRequiredSnapshotAsync(context, "PlaceMonkeyHeroPrecheck", cancellationToken)
                .ConfigureAwait(false);

            if (precheckSnapshot.CanPlaceHero == false)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "PlaceMonkeyHeroPrecheck",
                    "The configured hero is not currently available for placement.");
            }
        }

        await ScriptExecutionOperations.RetryAsync(
            context,
            new ScriptRetryOptions
            {
                MaxAttempts = 3,
                DelayBetweenAttemptsMilliseconds = 150,
                Description = $"Place '{selectionCode}'"
            },
            async (attempt, token) =>
            {
                await ScriptInstructionHandlerSupport.CancelPlacementModeIfActiveAsync(context, token).ConfigureAwait(false);
                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "PlaceMonkeyPrepare",
                    $"Placement attempt {attempt}: moving mouse to requested coordinate.",
                    token).ConfigureAwait(false);

                context.RuntimeServices.Input.MoveMouseToScriptCoordinate(requestedCoordinate);

                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "PlaceMonkeySelect",
                    $"Placement attempt {attempt}: sending hotkey for '{selectionCode}'.",
                    token).ConfigureAwait(false);

                context.RuntimeServices.Input.PressHotkey(placementHotkey);

                await ScriptExecutionOperations.WaitUntilAsync(
                    context,
                    new ScriptWaitOptions
                    {
                        TimeoutMilliseconds = 1000,
                        PollIntervalMilliseconds = 100,
                        Description = "placement mode active"
                    },
                    async innerToken =>
                    {
                        var snapshot = await context.RuntimeServices.GameStageState
                            .CaptureSnapshotAsync(innerToken)
                            .ConfigureAwait(false);
                        return ScriptInstructionHandlerSupport.IsPlacementModeActive(snapshot);
                    },
                    token).ConfigureAwait(false);

                foreach (var placementCoordinate in ScriptInstructionHandlerSupport.BuildPlacementSearchCoordinates(requestedCoordinate))
                {
                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "PlaceMonkeyClick",
                        $"Trying placement click at {ScriptInstructionHandlerSupport.FormatPoint(placementCoordinate)}.",
                        token).ConfigureAwait(false);

                    context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(placementCoordinate, clickCount: 1);

                    GameStageStateSnapshot? postClickSnapshot = null;
                    try
                    {
                        await ScriptExecutionOperations.WaitUntilAsync(
                            context,
                            new ScriptWaitOptions
                            {
                                TimeoutMilliseconds = 400,
                                PollIntervalMilliseconds = 75,
                                Description = "placement mode exit"
                            },
                            async innerToken =>
                            {
                                postClickSnapshot = await context.RuntimeServices.GameStageState
                                    .CaptureSnapshotAsync(innerToken)
                                    .ConfigureAwait(false);
                                return postClickSnapshot?.IsPlacingMonkey == false;
                            },
                            token).ConfigureAwait(false);
                    }
                    catch (ScriptExecutionException ex) when (ScriptInstructionHandlerSupport.IsWaitTimeout(ex))
                    {
                    }

                    if (postClickSnapshot?.IsPlacingMonkey == false)
                    {
                        var monkeyDocument = context.TaskFlow.MonkeyObjectsByBindingId.GetValueOrDefault(instruction.MonkeyBindingId);
                        var runtimeState = context.State.UpsertMonkeyState(
                            instruction.MonkeyBindingId,
                            string.IsNullOrWhiteSpace(instruction.MonkeyObjectId)
                                ? monkeyDocument?.ObjectId ?? string.Empty
                                : instruction.MonkeyObjectId,
                            selectionCode,
                            monkeyDocument?.PlacementOrder ?? 0);
                        runtimeState.LastKnownCoordinate = placementCoordinate;

                        await ScriptExecutionOperations.CheckpointAsync(
                            context,
                            "PlaceMonkeyPlaced",
                            $"Placed '{selectionCode}' at {ScriptInstructionHandlerSupport.FormatPoint(placementCoordinate)}.",
                            token).ConfigureAwait(false);

                        return true;
                    }
                }

                await ScriptInstructionHandlerSupport.CancelPlacementModeIfActiveAsync(context, token).ConfigureAwait(false);
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "PlaceMonkeyClick",
                    $"Failed to place '{selectionCode}' near {ScriptInstructionHandlerSupport.FormatPoint(requestedCoordinate)} after offset search.",
                    attempt);
            },
            static success => success,
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpgradeMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.UpgradeMonkey;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyTarget",
                "Upgrade instruction is missing the target monkey binding ID.");
        }

        if (!context.State.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyTarget",
                $"Target monkey binding '{instruction.TargetMonkeyBindingId}' does not exist in runtime state.");
        }

        if (ScriptInstructionHandlerSupport.IsHeroObjectKey(monkeyState.ObjectId) ||
            ScriptInstructionHandlerSupport.IsHeroObjectKey(instruction.TargetMonkeyObjectId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyHeroUnsupported",
                "Hero upgrades are not yet supported because the runtime cannot verify hero upgrade success.");
        }

        if (monkeyState.LastKnownCoordinate is null)
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyCoordinate",
                $"Target monkey '{monkeyState.ObjectId}' does not have a known runtime coordinate.");
        }

        if (!Enum.TryParse<UpgradePathType>(instruction.UpgradePath, true, out var upgradePath))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "UpgradeMonkeyPath",
                $"Unsupported upgrade path '{instruction.UpgradePath}'.");
        }

        var targetCoordinate = monkeyState.LastKnownCoordinate.Value;
        var upgradeHotkey = ScriptExecutionKeyBindingResolver.ResolveUpgradeHotkey(upgradePath);
        var upgradeCount = Math.Max(1, instruction.UpgradeCount);

        for (var upgradeIndex = 1; upgradeIndex <= upgradeCount; upgradeIndex++)
        {
            var panelSnapshot = await ScriptInstructionHandlerSupport
                .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, cancellationToken)
                .ConfigureAwait(false);
            var panelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(panelSnapshot);
            if (!panelSide.HasValue)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyPanel",
                    "Failed to detect the upgrade panel for the selected monkey.");
            }

            var currentLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                panelSnapshot,
                panelSide.Value,
                upgradePath);
            if (!currentLevel.HasValue)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyPanel",
                    $"Failed to read the current '{instruction.UpgradePath}' path level.");
            }

            if (currentLevel.Value >= 5)
            {
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "UpgradeMonkeyLevelCap",
                    $"The '{instruction.UpgradePath}' path is already at level 5.");
            }

            var expectedLevel = currentLevel.Value + 1;

            await ScriptExecutionOperations.RetryAsync(
                context,
                new ScriptRetryOptions
                {
                    MaxAttempts = 3,
                    DelayBetweenAttemptsMilliseconds = 150,
                    Description = $"Upgrade '{monkeyState.ObjectId}' {instruction.UpgradePath} to level {expectedLevel}"
                },
                async (attempt, token) =>
                {
                    var beforePressSnapshot = await ScriptInstructionHandlerSupport
                        .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, token)
                        .ConfigureAwait(false);
                    var visiblePanelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(beforePressSnapshot);
                    if (!visiblePanelSide.HasValue)
                    {
                        throw ScriptInstructionHandlerSupport.CreateExecutionException(
                            context,
                            "UpgradeMonkeyPanel",
                            "Failed to restore the upgrade panel before sending the upgrade hotkey.",
                            attempt);
                    }

                    var beforePressLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                        beforePressSnapshot,
                        visiblePanelSide.Value,
                        upgradePath);
                    if (!beforePressLevel.HasValue)
                    {
                        throw ScriptInstructionHandlerSupport.CreateExecutionException(
                            context,
                            "UpgradeMonkeyPanel",
                            $"Failed to read the '{instruction.UpgradePath}' path level before upgrading.",
                            attempt);
                    }

                    if (beforePressLevel.Value >= expectedLevel)
                    {
                        return true;
                    }

                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "UpgradeMonkeyPress",
                        $"Upgrade {upgradeIndex}/{upgradeCount}, attempt {attempt}: sending '{instruction.UpgradePath}' upgrade hotkey.",
                        token).ConfigureAwait(false);

                    context.RuntimeServices.Input.PressHotkey(upgradeHotkey);

                    await ScriptExecutionOperations.WaitUntilAsync(
                        context,
                        new ScriptWaitOptions
                        {
                            TimeoutMilliseconds = 900,
                            PollIntervalMilliseconds = 100,
                            Description = $"upgrade path reach level {expectedLevel}"
                        },
                        async innerToken =>
                        {
                            var afterPressSnapshot = await context.RuntimeServices.GameStageState
                                .CaptureSnapshotAsync(innerToken)
                                .ConfigureAwait(false);
                            if (afterPressSnapshot is null)
                            {
                                return false;
                            }

                            var afterPressPanelSide = ScriptInstructionHandlerSupport.ResolveVisibleUpgradePanelSide(afterPressSnapshot);
                            if (!afterPressPanelSide.HasValue)
                            {
                                return false;
                            }

                            var afterPressLevel = ScriptInstructionHandlerSupport.GetUpgradeLevel(
                                afterPressSnapshot,
                                afterPressPanelSide.Value,
                                upgradePath);
                            return afterPressLevel.HasValue && afterPressLevel.Value >= expectedLevel;
                        },
                        token).ConfigureAwait(false);

                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "UpgradeMonkeySucceeded",
                        $"Upgrade {upgradeIndex}/{upgradeCount}: '{instruction.UpgradePath}' reached level {expectedLevel}.",
                        token).ConfigureAwait(false);

                    return true;
                },
                static success => success,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class SwitchMonkeyTargetInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SwitchMonkeyTarget;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SwitchMonkeyTargetTarget",
                "Switch target instruction is missing the target monkey binding ID.");
        }

        if (!context.State.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SwitchMonkeyTargetTarget",
                $"Target monkey binding '{instruction.TargetMonkeyBindingId}' does not exist in runtime state.");
        }

        if (monkeyState.LastKnownCoordinate is null)
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SwitchMonkeyTargetCoordinate",
                $"Target monkey '{monkeyState.ObjectId}' does not have a known runtime coordinate.");
        }

        if (!Enum.TryParse<SwitchDirectionType>(instruction.SwitchDirection, true, out var switchDirection))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SwitchMonkeyTargetDirection",
                $"Unsupported switch direction '{instruction.SwitchDirection}'.");
        }

        var targetCoordinate = monkeyState.LastKnownCoordinate.Value;
        var switchHotkey = ScriptExecutionKeyBindingResolver.ResolveSwitchTargetHotkey(switchDirection);
        var switchCount = Math.Max(1, instruction.SwitchCount);

        await ScriptInstructionHandlerSupport
            .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, cancellationToken)
            .ConfigureAwait(false);

        await ScriptInstructionHandlerSupport
            .PressHotkeyRepeatedAsync(
                context,
                switchHotkey,
                switchCount,
                "SwitchMonkeyTargetPress",
                $"Switching '{monkeyState.ObjectId}' targeting {switchDirection} {switchCount} time(s).",
                cancellationToken)
            .ConfigureAwait(false);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SwitchMonkeyTargetSucceeded",
            $"Switched '{monkeyState.ObjectId}' targeting {switchDirection} {switchCount} time(s).",
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SetMonkeyAbilityInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SetMonkeyAbility;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SetMonkeyAbilityTarget",
                "Set monkey ability instruction is missing the target monkey binding ID.");
        }

        if (!context.State.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SetMonkeyAbilityTarget",
                $"Target monkey binding '{instruction.TargetMonkeyBindingId}' does not exist in runtime state.");
        }

        if (monkeyState.LastKnownCoordinate is null)
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SetMonkeyAbilityCoordinate",
                $"Target monkey '{monkeyState.ObjectId}' does not have a known runtime coordinate.");
        }

        if (!Enum.TryParse<MonkeyAbilityType>(instruction.SelectedAbility, true, out var abilityType))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SetMonkeyAbilityType",
                $"Unsupported monkey ability '{instruction.SelectedAbility}'.");
        }

        var targetCoordinate = monkeyState.LastKnownCoordinate.Value;
        var abilityHotkey = ScriptExecutionKeyBindingResolver.ResolveMonkeyAbilityHotkey(abilityType);

        await ScriptInstructionHandlerSupport
            .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, cancellationToken)
            .ConfigureAwait(false);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SetMonkeyAbilityPress",
            $"Sending monkey ability '{abilityType}' for '{monkeyState.ObjectId}'.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(abilityHotkey);

        if (instruction.RequiresAbilityCoordinate)
        {
            var abilityCoordinate = new WpfPoint(instruction.AbilityCoordinateX, instruction.AbilityCoordinateY);

            await ScriptExecutionOperations.DelayAsync(
                context,
                60,
                "SetMonkeyAbilityTargetingDelay",
                cancellationToken).ConfigureAwait(false);

            await ScriptExecutionOperations.CheckpointAsync(
                context,
                "SetMonkeyAbilityClick",
                $"Clicking ability coordinate {ScriptInstructionHandlerSupport.FormatPoint(abilityCoordinate)}.",
                cancellationToken).ConfigureAwait(false);

            context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(abilityCoordinate, clickCount: 1);
        }

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SetMonkeyAbilitySucceeded",
            instruction.RequiresAbilityCoordinate
                ? $"Applied monkey ability '{abilityType}' for '{monkeyState.ObjectId}' with target coordinate."
                : $"Applied monkey ability '{abilityType}' for '{monkeyState.ObjectId}'.",
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SellMonkeyInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.SellMonkey;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SellMonkeyTarget",
                "Sell monkey instruction is missing the target monkey binding ID.");
        }

        if (!context.State.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SellMonkeyTarget",
                $"Target monkey binding '{instruction.TargetMonkeyBindingId}' does not exist in runtime state.");
        }

        if (monkeyState.LastKnownCoordinate is null)
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "SellMonkeyCoordinate",
                $"Target monkey '{monkeyState.ObjectId}' does not have a known runtime coordinate.");
        }

        var targetCoordinate = monkeyState.LastKnownCoordinate.Value;
        var sellHotkey = ScriptExecutionKeyBindingResolver.ResolveSellHotkey();

        await ScriptInstructionHandlerSupport
            .EnsureUpgradePanelVisibleAsync(context, targetCoordinate, cancellationToken)
            .ConfigureAwait(false);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SellMonkeyPress",
            $"Selling '{monkeyState.ObjectId}'.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(sellHotkey);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SellMonkeySucceeded",
            $"Sent sell hotkey for '{monkeyState.ObjectId}'.",
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PlaceHeroInventoryInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.PlaceHeroInventory;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (!Enum.TryParse<InventoryType>(instruction.SelectedInventoryItem, true, out var inventoryType))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "PlaceHeroInventoryType",
                $"Unsupported hero inventory '{instruction.SelectedInventoryItem}'.");
        }

        var heroHotkey = ScriptExecutionKeyBindingResolver.ResolveHeroHotkey();
        var inventoryHotkey = ScriptExecutionKeyBindingResolver.ResolveHeroInventoryHotkey(inventoryType);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "PlaceHeroInventoryHero",
            "Opening hero panel.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(heroHotkey);

        await ScriptExecutionOperations.DelayAsync(
            context,
            60,
            "PlaceHeroInventoryHeroDelay",
            cancellationToken).ConfigureAwait(false);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "PlaceHeroInventorySelect",
            $"Selecting hero inventory '{inventoryType}'.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(inventoryHotkey);

        if (instruction.RequiresAbilityCoordinate)
        {
            var targetCoordinate = new WpfPoint(instruction.PositionX, instruction.PositionY);

            await ScriptExecutionOperations.DelayAsync(
                context,
                60,
                "PlaceHeroInventoryTargetingDelay",
                cancellationToken).ConfigureAwait(false);

            await ScriptExecutionOperations.CheckpointAsync(
                context,
                "PlaceHeroInventoryClick",
                $"Clicking inventory target coordinate {ScriptInstructionHandlerSupport.FormatPoint(targetCoordinate)}.",
                cancellationToken).ConfigureAwait(false);

            context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(targetCoordinate, clickCount: 1);
        }

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "PlaceHeroInventorySucceeded",
            instruction.RequiresAbilityCoordinate
                ? $"Used hero inventory '{inventoryType}' with target coordinate."
                : $"Used hero inventory '{inventoryType}'.",
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ActivateAbilityInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.ActivateAbility;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        if (!Enum.TryParse<ActivatedAbilityType>(instruction.SelectedActivatedAbility, true, out var abilityType))
        {
            throw ScriptInstructionHandlerSupport.CreateExecutionException(
                context,
                "ActivateAbilityType",
                $"Unsupported activated ability '{instruction.SelectedActivatedAbility}'.");
        }

        var abilityHotkey = ScriptExecutionKeyBindingResolver.ResolveActivatedAbilityHotkey(abilityType);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "ActivateAbilityPress",
            $"Activating global ability '{abilityType}'.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(abilityHotkey);

        if (instruction.RequiresAbilityCoordinate)
        {
            var targetCoordinate = new WpfPoint(instruction.AbilityCoordinateX, instruction.AbilityCoordinateY);

            await ScriptExecutionOperations.DelayAsync(
                context,
                60,
                "ActivateAbilityTargetingDelay",
                cancellationToken).ConfigureAwait(false);

            await ScriptExecutionOperations.CheckpointAsync(
                context,
                "ActivateAbilityClick",
                $"Clicking activated ability target coordinate {ScriptInstructionHandlerSupport.FormatPoint(targetCoordinate)}.",
                cancellationToken).ConfigureAwait(false);

            context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(targetCoordinate, clickCount: 1);
        }

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "ActivateAbilitySucceeded",
            instruction.RequiresAbilityCoordinate
                ? $"Activated global ability '{abilityType}' with target coordinate."
                : $"Activated global ability '{abilityType}'.",
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MouseClickInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.MouseClick;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        var coordinate = new WpfPoint(instruction.PositionX, instruction.PositionY);
        var clickCount = Math.Max(1, instruction.ClickCount);
        var clickIntervalMilliseconds = Math.Max(0, instruction.ClickIntervalMilliseconds);

        for (var index = 0; index < clickCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScriptExecutionOperations.CheckpointAsync(
                context,
                "MouseClick",
                $"Executing click {index + 1}/{clickCount}.",
                cancellationToken).ConfigureAwait(false);
            context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(coordinate, clickCount: 1);

            if (index < clickCount - 1 && clickIntervalMilliseconds > 0)
            {
                await ScriptExecutionOperations.DelayAsync(
                    context,
                    clickIntervalMilliseconds,
                    "MouseClickInterval",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public sealed class NextRoundInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.NextRound;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        var nextRoundAction = string.IsNullOrWhiteSpace(instruction.NextRoundAction)
            ? "PlayFastForward"
            : instruction.NextRoundAction.Trim();
        var nextRoundHotkey = ScriptExecutionKeyBindingResolver.ResolveNextRoundHotkey(nextRoundAction);

        switch (nextRoundAction)
        {
            case "SendNextRound":
            {
                var sendCount = Math.Max(1, instruction.NextRoundSendCount);

                await ScriptInstructionHandlerSupport
                    .PressHotkeyRepeatedAsync(
                        context,
                        nextRoundHotkey,
                        sendCount,
                        "NextRoundSend",
                        $"Sending next round {sendCount} time(s).",
                        cancellationToken)
                    .ConfigureAwait(false);

                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "NextRoundSucceeded",
                    $"Sent next round {sendCount} time(s).",
                    cancellationToken).ConfigureAwait(false);
                break;
            }
            case "PlayFastForward":
            {
                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "NextRoundPress",
                    "Toggling play/fast forward.",
                    cancellationToken).ConfigureAwait(false);

                context.RuntimeServices.Input.PressHotkey(nextRoundHotkey);

                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "NextRoundSucceeded",
                    "Sent play/fast forward hotkey.",
                    cancellationToken).ConfigureAwait(false);
                break;
            }
            default:
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "NextRoundAction",
                    $"Unsupported next round action '{instruction.NextRoundAction}'.");
        }
    }
}

public sealed class WaitInstructionHandler : ScriptInstructionHandlerBase
{
    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;
    private const int IndefiniteWaitTimeoutMilliseconds = int.MaxValue;
    private const int WaitPollIntervalMilliseconds = 100;

    public override ScriptCommandType CommandType => ScriptCommandType.Wait;

    public override async Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        var instruction = context.Step.Instruction;
        var waitMode = instruction.WaitMode;

        switch (waitMode)
        {
            case nameof(WaitModeType.Time):
                await ScriptExecutionOperations.DelayAsync(
                    context,
                    instruction.WaitTimeMilliseconds,
                    "WaitTime",
                    cancellationToken).ConfigureAwait(false);
                break;
            case nameof(WaitModeType.Gold):
                await ScriptExecutionOperations.WaitUntilAsync(
                    context,
                    new ScriptWaitOptions
                    {
                        TimeoutMilliseconds = IndefiniteWaitTimeoutMilliseconds,
                        PollIntervalMilliseconds = WaitPollIntervalMilliseconds,
                        Description = $"gold >= {instruction.WaitGoldAmount}"
                    },
                    async token =>
                    {
                        var gold = await context.RuntimeServices.GameStageState
                            .GetGoldAsync(token)
                            .ConfigureAwait(false);
                        return gold.HasValue && gold.Value >= instruction.WaitGoldAmount;
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
            case nameof(WaitModeType.Round):
                await ScriptExecutionOperations.WaitUntilAsync(
                    context,
                    new ScriptWaitOptions
                    {
                        TimeoutMilliseconds = IndefiniteWaitTimeoutMilliseconds,
                        PollIntervalMilliseconds = WaitPollIntervalMilliseconds,
                        Description = $"round >= {instruction.WaitRoundCount}"
                    },
                    async token =>
                    {
                        var round = await context.RuntimeServices.GameStageState
                            .GetRoundAsync(token)
                            .ConfigureAwait(false);
                        return round.HasValue && round.Value >= instruction.WaitRoundCount;
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
            case nameof(WaitModeType.CoordinateColor):
            {
                if (!TryParseRgbHex(instruction.WaitColorHex, out var expectedR, out var expectedG, out var expectedB))
                {
                    throw ScriptInstructionHandlerSupport.CreateExecutionException(
                        context,
                        "WaitColorHex",
                        $"Unsupported wait color '{instruction.WaitColorHex}'. Expected format '#RRGGBB'.");
                }

                var targetCoordinate = new WpfPoint(instruction.WaitColorCoordinateX, instruction.WaitColorCoordinateY);
                await ScriptExecutionOperations.WaitUntilAsync(
                    context,
                    new ScriptWaitOptions
                    {
                        TimeoutMilliseconds = IndefiniteWaitTimeoutMilliseconds,
                        PollIntervalMilliseconds = WaitPollIntervalMilliseconds,
                        Description = $"color at {ScriptInstructionHandlerSupport.FormatPoint(targetCoordinate)} matches {instruction.WaitColorHex}"
                    },
                    token => WaitForCoordinateColorMatchAsync(
                        context,
                        targetCoordinate,
                        expectedR,
                        expectedG,
                        expectedB,
                        instruction.WaitColorTolerance,
                        token),
                    cancellationToken).ConfigureAwait(false);
                break;
            }
            default:
                throw ScriptInstructionHandlerSupport.CreateExecutionException(
                    context,
                    "WaitMode",
                    $"Unsupported wait mode '{waitMode}'.");
        }
    }

    private static Task<bool> WaitForCoordinateColorMatchAsync(
        ScriptInstructionExecutionContext context,
        WpfPoint scriptCoordinate,
        int expectedR,
        int expectedG,
        int expectedB,
        int tolerance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.RuntimeServices.Capture.TryCaptureFrame(out _, out var frame))
        {
            return Task.FromResult(false);
        }

        using (frame)
        {
            if (frame.Empty())
            {
                return Task.FromResult(false);
            }

            var x = ScaleScriptCoordinate(scriptCoordinate.X, ReferenceWidth, frame.Width);
            var y = ScaleScriptCoordinate(scriptCoordinate.Y, ReferenceHeight, frame.Height);
            var (actualR, actualG, actualB) = ReadPixel(frame, x, y);

            var isMatch =
                Math.Abs(actualR - expectedR) <= tolerance &&
                Math.Abs(actualG - expectedG) <= tolerance &&
                Math.Abs(actualB - expectedB) <= tolerance;

            return Task.FromResult(isMatch);
        }
    }

    private static (int R, int G, int B) ReadPixel(Mat frame, int x, int y)
    {
        return frame.Channels() switch
        {
            1 => ReadGrayPixel(frame, x, y),
            3 => ReadBgrPixel(frame, x, y),
            4 => ReadBgraPixel(frame, x, y),
            _ => throw new NotSupportedException($"Unsupported frame channel count '{frame.Channels()}'.")
        };
    }

    private static (int R, int G, int B) ReadGrayPixel(Mat frame, int x, int y)
    {
        var value = frame.At<byte>(y, x);
        return (value, value, value);
    }

    private static (int R, int G, int B) ReadBgrPixel(Mat frame, int x, int y)
    {
        var value = frame.At<Vec3b>(y, x);
        return (value.Item2, value.Item1, value.Item0);
    }

    private static (int R, int G, int B) ReadBgraPixel(Mat frame, int x, int y)
    {
        var value = frame.At<Vec4b>(y, x);
        return (value.Item2, value.Item1, value.Item0);
    }

    private static int ScaleScriptCoordinate(double coordinate, int referenceSize, int actualSize)
    {
        if (actualSize <= 0)
        {
            return 0;
        }

        var scaled = (int)Math.Round(coordinate / referenceSize * actualSize);
        return Math.Clamp(scaled, 0, actualSize - 1);
    }

    private static bool TryParseRgbHex(string? value, out int r, out int g, out int b)
    {
        r = 0;
        g = 0;
        b = 0;

        var text = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        if (!int.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out r) ||
            !int.TryParse(text.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g) ||
            !int.TryParse(text.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b))
        {
            r = 0;
            g = 0;
            b = 0;
            return false;
        }

        return true;
    }
}

public sealed class ModifyMonkeyCoordinateInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.ModifyMonkeyCoordinate;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Placeholder for updating runtime coordinates of an existing monkey binding.
        return Task.CompletedTask;
    }
}

public sealed class CommentInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.Comment;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        // Comments do not change runtime state and are intentionally ignored.
        return Task.CompletedTask;
    }
}

internal enum UpgradePanelSide
{
    Left,
    Right
}

internal static class ScriptInstructionHandlerSupport
{
    private const int RepeatedHotkeyIntervalMilliseconds = 60;

    private static readonly (double OffsetX, double OffsetY)[] PlacementOffsets =
    [
        (0d, 0d),
        (4d, 0d),
        (-4d, 0d),
        (0d, 4d),
        (0d, -4d),
        (8d, 0d),
        (-8d, 0d),
        (0d, 8d),
        (0d, -8d),
        (6d, 6d),
        (-6d, 6d),
        (6d, -6d),
        (-6d, -6d)
    ];

    private static readonly (double OffsetX, double OffsetY)[] SelectionOffsets =
    [
        (0d, 0d),
        (3d, 0d),
        (-3d, 0d),
        (0d, 3d),
        (0d, -3d),
        (6d, 0d),
        (-6d, 0d),
        (0d, 6d),
        (0d, -6d)
    ];

    public static IEnumerable<WpfPoint> BuildPlacementSearchCoordinates(WpfPoint requestedCoordinate)
    {
        return BuildOffsetCoordinates(requestedCoordinate, PlacementOffsets);
    }

    public static bool IsPlacementModeActive(GameStageStateSnapshot? snapshot)
    {
        return snapshot?.IsPlacingMonkey == true;
    }

    public static UpgradePanelSide? ResolveVisibleUpgradePanelSide(GameStageStateSnapshot? snapshot)
    {
        if (snapshot?.RightUpgradePanel.IsVisible == true)
        {
            return UpgradePanelSide.Right;
        }

        if (snapshot?.LeftUpgradePanel.IsVisible == true)
        {
            return UpgradePanelSide.Left;
        }

        return null;
    }

    public static int? GetUpgradeLevel(
        GameStageStateSnapshot snapshot,
        UpgradePanelSide panelSide,
        UpgradePathType upgradePath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var panelState = panelSide == UpgradePanelSide.Right
            ? snapshot.RightUpgradePanel
            : snapshot.LeftUpgradePanel;

        return upgradePath switch
        {
            UpgradePathType.Top => panelState.TopPathLevel,
            UpgradePathType.Middle => panelState.MiddlePathLevel,
            UpgradePathType.Bottom => panelState.BottomPathLevel,
            _ => null
        };
    }

    public static bool IsHeroObjectKey(string? objectKey)
    {
        return !string.IsNullOrWhiteSpace(objectKey) &&
               objectKey.StartsWith("Hero:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWaitTimeout(ScriptExecutionException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.Equals(exception.Checkpoint, "WaitTimedOut", StringComparison.Ordinal);
    }

    public static string FormatPoint(WpfPoint point)
    {
        return $"({point.X:0.##}, {point.Y:0.##})";
    }

    public static ScriptExecutionException CreateExecutionException(
        ScriptInstructionExecutionContext context,
        string checkpoint,
        string message,
        int attempt = 0,
        Exception? innerException = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint);

        return new ScriptExecutionException(
            message,
            context.Step.Index,
            context.Step.CommandType.ToString(),
            checkpoint,
            attempt,
            innerException);
    }

    public static async Task CancelPlacementModeIfActiveAsync(
        ScriptInstructionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var snapshot = await context.RuntimeServices.GameStageState
            .CaptureSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!IsPlacementModeActive(snapshot))
        {
            return;
        }

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "PlaceMonkeyCancel",
            "Placement mode is already active. Sending Escape to reset it.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressKey(KeyId.Escape);

        try
        {
            await ScriptExecutionOperations.WaitUntilAsync(
                context,
                new ScriptWaitOptions
                {
                    TimeoutMilliseconds = 700,
                    PollIntervalMilliseconds = 100,
                    Description = "placement mode reset"
                },
                async innerToken =>
                {
                    var currentSnapshot = await context.RuntimeServices.GameStageState
                        .CaptureSnapshotAsync(innerToken)
                        .ConfigureAwait(false);
                    return currentSnapshot?.IsPlacingMonkey == false;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ScriptExecutionException ex) when (IsWaitTimeout(ex))
        {
        }
    }

    public static async Task<GameStageStateSnapshot> EnsureUpgradePanelVisibleAsync(
        ScriptInstructionExecutionContext context,
        WpfPoint targetCoordinate,
        CancellationToken cancellationToken)
    {
        return await ScriptExecutionOperations.RetryAsync(
            context,
            new ScriptRetryOptions
            {
                MaxAttempts = 3,
                DelayBetweenAttemptsMilliseconds = 150,
                Description = $"Open upgrade panel at {FormatPoint(targetCoordinate)}"
            },
            async (attempt, token) =>
            {
                foreach (var selectionCoordinate in BuildOffsetCoordinates(targetCoordinate, SelectionOffsets))
                {
                    await ScriptExecutionOperations.CheckpointAsync(
                        context,
                        "UpgradeMonkeySelect",
                        $"Selection attempt {attempt}: clicking {FormatPoint(selectionCoordinate)}.",
                        token).ConfigureAwait(false);

                    context.RuntimeServices.Input.ClickMouseAtScriptCoordinate(selectionCoordinate, clickCount: 1);

                    GameStageStateSnapshot? visibleSnapshot = null;
                    try
                    {
                        await ScriptExecutionOperations.WaitUntilAsync(
                            context,
                            new ScriptWaitOptions
                            {
                                TimeoutMilliseconds = 700,
                                PollIntervalMilliseconds = 100,
                                Description = "upgrade panel visible"
                            },
                            async innerToken =>
                            {
                                visibleSnapshot = await context.RuntimeServices.GameStageState
                                    .CaptureSnapshotAsync(innerToken)
                                    .ConfigureAwait(false);
                                return ResolveVisibleUpgradePanelSide(visibleSnapshot).HasValue;
                            },
                            token).ConfigureAwait(false);
                    }
                    catch (ScriptExecutionException ex) when (IsWaitTimeout(ex))
                    {
                    }

                    if (ResolveVisibleUpgradePanelSide(visibleSnapshot).HasValue)
                    {
                        return visibleSnapshot!;
                    }
                }

                throw CreateExecutionException(
                    context,
                    "UpgradeMonkeySelect",
                    $"Failed to open the upgrade panel near {FormatPoint(targetCoordinate)}.",
                    attempt);
            },
            snapshot => ResolveVisibleUpgradePanelSide(snapshot).HasValue,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task PressHotkeyRepeatedAsync(
        ScriptInstructionExecutionContext context,
        HotkeyBinding hotkey,
        int repeatCount,
        string checkpoint,
        string description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint);

        var effectiveRepeatCount = Math.Max(1, repeatCount);
        var effectiveDescription = string.IsNullOrWhiteSpace(description)
            ? $"Sending hotkey '{hotkey.DisplayName}'."
            : description;
        var modifierKeys = ExpandModifierKeys(hotkey.Modifiers);

        try
        {
            if (modifierKeys.Count > 0)
            {
                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    $"{checkpoint}ModifiersDown",
                    $"{effectiveDescription} Holding modifiers '{hotkey.DisplayName}'.",
                    cancellationToken).ConfigureAwait(false);

                foreach (var modifierKey in modifierKeys)
                {
                    context.RuntimeServices.Input.KeyDown(modifierKey);
                }
            }

            for (var index = 0; index < effectiveRepeatCount; index++)
            {
                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    checkpoint,
                    $"{effectiveDescription} Press {index + 1}/{effectiveRepeatCount}.",
                    cancellationToken).ConfigureAwait(false);

                context.RuntimeServices.Input.PressKey(hotkey.Key);

                if (index < effectiveRepeatCount - 1)
                {
                    await ScriptExecutionOperations.DelayAsync(
                        context,
                        RepeatedHotkeyIntervalMilliseconds,
                        $"{checkpoint}Interval",
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            for (var index = modifierKeys.Count - 1; index >= 0; index--)
            {
                context.RuntimeServices.Input.KeyUp(modifierKeys[index]);
            }
        }
    }

    private static IEnumerable<WpfPoint> BuildOffsetCoordinates(
        WpfPoint baseCoordinate,
        IReadOnlyList<(double OffsetX, double OffsetY)> offsets)
    {
        foreach (var (offsetX, offsetY) in offsets)
        {
            yield return new WpfPoint(baseCoordinate.X + offsetX, baseCoordinate.Y + offsetY);
        }
    }

    private static List<KeyId> ExpandModifierKeys(ModifierKeys modifiers)
    {
        var keys = new List<KeyId>(4);

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            keys.Add(KeyId.LeftCtrl);
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            keys.Add(KeyId.LeftShift);
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            keys.Add(KeyId.LeftAlt);
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            keys.Add(KeyId.LeftWin);
        }

        return keys;
    }
}
