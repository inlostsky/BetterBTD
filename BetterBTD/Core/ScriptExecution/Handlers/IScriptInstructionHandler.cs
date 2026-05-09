using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Core.ScriptExecution.Handlers;

public interface IScriptInstructionHandler
{
    ScriptCommandType CommandType { get; }

    Task HandleAsync(ScriptInstructionExecutionContext context, CancellationToken cancellationToken);
}
