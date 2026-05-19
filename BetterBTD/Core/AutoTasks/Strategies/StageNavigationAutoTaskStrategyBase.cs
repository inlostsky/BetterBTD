using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Core.AutoTasks.Strategies;

public abstract class StageNavigationAutoTaskStrategyBase : IAutoTaskStrategy
{
    protected const int DefaultWaitDelayMs = 500;

    public abstract AutoTaskKind Kind { get; }

    public virtual Task<AutoTaskDecision> DecideNextAsync(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        if (state.HasPendingScriptOutcome)
        {
            return Task.FromResult(DecideAfterScriptExecution(state, snapshot));
        }

        return state.Phase switch
        {
            AutoTaskPhase.PreparingStage or
            AutoTaskPhase.NavigatingToStage or
            AutoTaskPhase.WaitingForLevelLoad => Task.FromResult(DecideStageEntry(state, snapshot)),
            AutoTaskPhase.SettlingResult => Task.FromResult(DecideAfterScriptExecution(state, snapshot)),
            AutoTaskPhase.AdvancingObjective => Task.FromResult(DecideObjectiveAdvancement(state, snapshot)),
            AutoTaskPhase.Completed => Task.FromResult(AutoTaskDecision.Complete("Auto task has already completed.")),
            AutoTaskPhase.Failed => Task.FromResult(AutoTaskDecision.Fail("Auto task has already failed.")),
            _ => Task.FromResult(AutoTaskDecision.Wait("Waiting for next state update.", DefaultWaitDelayMs))
        };
    }

    protected virtual AutoTaskDecision DecideStageEntry(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        if (snapshot.State == GameUiStateId.InLevel)
        {
            return AutoTaskDecision.StartScript(
                BuildScriptQuery(state),
                "Stage entry completed. Resolve and start the task script.");
        }

        if (snapshot.State == GameUiStateId.Loading)
        {
            return AutoTaskDecision.Wait(
                "Waiting for the stage to finish loading.",
                DefaultWaitDelayMs,
                AutoTaskPhase.WaitingForLevelLoad);
        }

        return AutoTaskDecision.Navigate(
            "Navigate toward the target stage.",
            AutoTaskPhase.NavigatingToStage);
    }

    protected abstract AutoTaskScriptQuery BuildScriptQuery(AutoTaskRuntimeState state);

    protected abstract AutoTaskDecision DecideAfterScriptExecution(AutoTaskRuntimeState state, GameUiSnapshot snapshot);

    protected virtual AutoTaskDecision DecideObjectiveAdvancement(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        return AutoTaskDecision.Fail(
            $"{Kind.ToKey()} objective advancement has not been implemented yet.");
    }
}
