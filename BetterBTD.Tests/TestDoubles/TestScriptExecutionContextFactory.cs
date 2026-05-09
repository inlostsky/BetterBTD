using BetterBTD.Core.ScriptExecution;
using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Models.ScriptEditor;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Tests.TestDoubles;

internal static class TestScriptExecutionContextFactory
{
    public static ScriptInstructionExecutionContext Create(
        ScriptInstructionDocument instruction,
        ScriptExecutionRuntimeServices runtimeServices,
        IReadOnlyList<ScriptMonkeyObjectDocument>? monkeyObjects = null,
        int stepIndex = 0,
        string sourceFilePath = "[Test Script]")
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(runtimeServices);

        if (!Enum.TryParse<ScriptCommandType>(instruction.CommandType, true, out var commandType))
        {
            throw new InvalidOperationException($"Unsupported script command type '{instruction.CommandType}'.");
        }

        monkeyObjects ??= [];

        var document = new ScriptDocument
        {
            MonkeyObjects = monkeyObjects.ToList(),
            Instructions = [instruction]
        };

        var taskFlow = new ScriptTaskFlow
        {
            SourceFilePath = sourceFilePath,
            Document = document,
            Steps =
            [
                new ScriptTaskFlowStep
                {
                    Index = stepIndex,
                    CommandType = commandType,
                    Instruction = instruction
                }
            ],
            MonkeyObjectsByBindingId = monkeyObjects
                .Where(x => !string.IsNullOrWhiteSpace(x.BindingId))
                .ToDictionary(x => x.BindingId, StringComparer.OrdinalIgnoreCase)
        };

        var state = new ScriptExecutionState();
        state.SeedMonkeyStates(monkeyObjects);

        var session = new ScriptExecutionSession(sourceFilePath);
        session.MarkStarted();
        session.EnterStep(stepIndex, commandType.ToString());

        return new ScriptInstructionExecutionContext
        {
            TaskFlow = taskFlow,
            Step = taskFlow.Steps[0],
            State = state,
            Options = new ScriptExecutionOptions
            {
                RequireCaptureService = false,
                RequireTargetWindow = false,
                RuntimeServices = runtimeServices
            },
            RuntimeServices = runtimeServices,
            ExecutionSession = session
        };
    }
}
