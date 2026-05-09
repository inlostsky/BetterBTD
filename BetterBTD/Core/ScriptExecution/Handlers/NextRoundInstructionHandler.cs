using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

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
                    $"Toggling play/fast forward with hotkey '{nextRoundHotkey.DisplayName}'.",
                    cancellationToken).ConfigureAwait(false);

                context.RuntimeServices.Input.PressHotkey(nextRoundHotkey);

                await ScriptExecutionOperations.CheckpointAsync(
                    context,
                    "NextRoundSucceeded",
                    $"Sent play/fast forward hotkey '{nextRoundHotkey.DisplayName}'.",
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
