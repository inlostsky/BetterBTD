using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;
using BetterBTD.Services;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Core.ScriptExecution.Handlers;

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

        if (ScriptEditorInstructionService.IsHeroSelectionCode(selectionCode))
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
                    $"Placement attempt {attempt}: sending hotkey '{placementHotkey.DisplayName}' for '{selectionCode}'.",
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
