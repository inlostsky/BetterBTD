using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public abstract class ScriptInstructionHandlerBase : IScriptInstructionHandler
{
    public abstract ScriptCommandType CommandType { get; }

    public abstract Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}
