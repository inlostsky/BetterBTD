using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

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
            $"Selling '{monkeyState.ObjectId}' with hotkey '{sellHotkey.DisplayName}'.",
            cancellationToken).ConfigureAwait(false);

        context.RuntimeServices.Input.PressHotkey(sellHotkey);

        await ScriptExecutionOperations.CheckpointAsync(
            context,
            "SellMonkeySucceeded",
            $"Sent sell hotkey for '{monkeyState.ObjectId}'.",
            cancellationToken).ConfigureAwait(false);
    }
}
