using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public sealed class CommentInstructionHandler : ScriptInstructionHandlerBase
{
    public override ScriptCommandType CommandType => ScriptCommandType.Comment;

    public override Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
