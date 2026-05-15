using BetterBTD.Models.ScriptExecution;
using BetterBTD.Services;
using BetterBTD.Core.ScriptExecution.Handlers;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Core.ScriptExecution;

internal readonly record struct ScriptExecutionContextBuildSummary(
    int ReplayedStepCount,
    int MutatedStepCount);

internal static class ScriptExecutionRuntimeContextBuilder
{
    public static ScriptExecutionContextBuildSummary Build(
        ScriptTaskFlow taskFlow,
        ScriptExecutionState state,
        int startStepIndex)
    {
        ArgumentNullException.ThrowIfNull(taskFlow);
        ArgumentNullException.ThrowIfNull(state);

        if (taskFlow.Steps.Count == 0 || startStepIndex <= 0)
        {
            return new ScriptExecutionContextBuildSummary(0, 0);
        }

        var replayedStepCount = 0;
        var mutatedStepCount = 0;

        foreach (var step in taskFlow.Steps)
        {
            if (step.Index >= startStepIndex)
            {
                break;
            }

            replayedStepCount++;
            if (TryApplyRuntimeState(taskFlow, state, step))
            {
                mutatedStepCount++;
            }
        }

        return new ScriptExecutionContextBuildSummary(replayedStepCount, mutatedStepCount);
    }

    private static bool TryApplyRuntimeState(
        ScriptTaskFlow taskFlow,
        ScriptExecutionState state,
        ScriptTaskFlowStep step)
    {
        return step.CommandType switch
        {
            Models.ScriptEditor.ScriptCommandType.PlaceMonkey => ApplyPlaceMonkeyState(taskFlow, state, step),
            Models.ScriptEditor.ScriptCommandType.UpgradeMonkey => ApplyUpgradeMonkeyState(state, step),
            Models.ScriptEditor.ScriptCommandType.ModifyMonkeyCoordinate => ApplyModifiedCoordinateState(state, step),
            Models.ScriptEditor.ScriptCommandType.SellMonkey => ApplySoldMonkeyState(state, step),
            _ => false
        };
    }

    private static bool ApplyPlaceMonkeyState(
        ScriptTaskFlow taskFlow,
        ScriptExecutionState state,
        ScriptTaskFlowStep step)
    {
        var instruction = step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.MonkeyBindingId))
        {
            return false;
        }

        var monkeyDocument = taskFlow.MonkeyObjectsByBindingId.GetValueOrDefault(instruction.MonkeyBindingId);
        var runtimeState = state.UpsertMonkeyState(
            instruction.MonkeyBindingId,
            string.IsNullOrWhiteSpace(instruction.MonkeyObjectId)
                ? monkeyDocument?.ObjectId ?? string.Empty
                : instruction.MonkeyObjectId,
            ScriptEditorInstructionService.NormalizePlaceSelectionCode(instruction.SelectedMonkeyTower),
            monkeyDocument?.PlacementOrder ?? 0);
        runtimeState.ResetExpectedUpgradeLevels();
        runtimeState.LastKnownCoordinate = new WpfPoint(instruction.PositionX, instruction.PositionY);
        return true;
    }

    private static bool ApplyUpgradeMonkeyState(
        ScriptExecutionState state,
        ScriptTaskFlowStep step)
    {
        var instruction = step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId) ||
            !state.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            return false;
        }

        var targetObjectKey = string.IsNullOrWhiteSpace(instruction.TargetMonkeyObjectId)
            ? monkeyState.ObjectId
            : instruction.TargetMonkeyObjectId;
        if (ScriptInstructionHandlerSupport.IsHeroObjectKey(targetObjectKey) ||
            !Enum.TryParse<Models.ScriptEditor.UpgradePathType>(instruction.UpgradePath, true, out var upgradePath))
        {
            return false;
        }

        monkeyState.ApplyExpectedUpgrade(upgradePath, instruction.UpgradeCount);
        return true;
    }

    private static bool ApplyModifiedCoordinateState(
        ScriptExecutionState state,
        ScriptTaskFlowStep step)
    {
        var instruction = step.Instruction;
        if (string.IsNullOrWhiteSpace(instruction.TargetMonkeyBindingId) ||
            !state.TryGetMonkeyState(instruction.TargetMonkeyBindingId, out var monkeyState))
        {
            return false;
        }

        monkeyState.LastKnownCoordinate = new WpfPoint(instruction.PositionX, instruction.PositionY);
        return true;
    }

    private static bool ApplySoldMonkeyState(
        ScriptExecutionState state,
        ScriptTaskFlowStep step)
    {
        var bindingId = step.Instruction.TargetMonkeyBindingId;
        return !string.IsNullOrWhiteSpace(bindingId) && state.RemoveMonkeyState(bindingId);
    }
}
