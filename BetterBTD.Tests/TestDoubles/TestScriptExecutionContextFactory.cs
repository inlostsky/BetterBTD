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

        return Create(
            [instruction],
            currentStepPosition: 0,
            runtimeServices,
            monkeyObjects,
            startingStepIndex: stepIndex,
            sourceFilePath);
    }

    public static ScriptInstructionExecutionContext Create(
        IReadOnlyList<ScriptInstructionDocument> instructions,
        int currentStepPosition,
        ScriptExecutionRuntimeServices runtimeServices,
        IReadOnlyList<ScriptMonkeyObjectDocument>? monkeyObjects = null,
        int startingStepIndex = 0,
        string sourceFilePath = "[Test Script]")
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(runtimeServices);
        if (instructions.Count == 0)
        {
            throw new ArgumentException("At least one instruction is required.", nameof(instructions));
        }

        if (currentStepPosition < 0 || currentStepPosition >= instructions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentStepPosition));
        }

        var steps = new List<ScriptTaskFlowStep>(instructions.Count);
        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            if (!Enum.TryParse<ScriptCommandType>(instruction.CommandType, true, out var commandType))
            {
                throw new InvalidOperationException($"Unsupported script command type '{instruction.CommandType}'.");
            }

            steps.Add(new ScriptTaskFlowStep
            {
                Index = startingStepIndex + index,
                CommandType = commandType,
                Instruction = instruction
            });
        }

        monkeyObjects ??= [];

        var document = new ScriptDocument
        {
            MonkeyObjects = monkeyObjects.ToList(),
            Instructions = instructions.ToList()
        };

        var taskFlow = new ScriptTaskFlow
        {
            SourceFilePath = sourceFilePath,
            Document = document,
            Steps = steps,
            MonkeyObjectsByBindingId = monkeyObjects
                .Where(x => !string.IsNullOrWhiteSpace(x.BindingId))
                .ToDictionary(x => x.BindingId, StringComparer.OrdinalIgnoreCase)
        };

        var state = new ScriptExecutionState();
        state.SeedMonkeyStates(monkeyObjects);

        var session = new ScriptExecutionSession(sourceFilePath);
        session.MarkStarted();
        session.EnterStep(taskFlow.Steps[currentStepPosition].Index, taskFlow.Steps[currentStepPosition].CommandType.ToString());

        return new ScriptInstructionExecutionContext
        {
            TaskFlow = taskFlow,
            Step = taskFlow.Steps[currentStepPosition],
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
