using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class OdysseyAutoTaskStrategy : IAutoTaskStrategy
{
    private const int DefaultWaitDelayMs = 500;

    public AutoTaskKind Kind => AutoTaskKind.Odyssey;

    public Task<AutoTaskDecision> DecideNextAsync(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        EnsureScriptQueueInitialized(state);
        AdvanceStageProgressIfNeeded(state, snapshot);
        RecoverScriptLifecycleIfNeeded(state, snapshot);

        if (state.HasPendingScriptOutcome)
        {
            return Task.FromResult(DecideAfterScriptExecution(state, snapshot));
        }

        return Task.FromResult(snapshot.State switch
        {
            GameUiStateId.InLevel => TryBuildStartScriptDecision(state),
            GameUiStateId.OdysseyLoading => AutoTaskDecision.Wait(
                "Waiting for the Odyssey stage to finish loading.",
                DefaultWaitDelayMs,
                AutoTaskPhase.WaitingForLevelLoad),
            GameUiStateId.OdysseyStart => AutoTaskDecision.Navigate(
                "Start the Odyssey run.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.PreparingStage
                    : AutoTaskPhase.NavigatingToStage),
            GameUiStateId.OdysseyCrew => AutoTaskDecision.Navigate(
                "Confirm the Odyssey crew screen and enter the next stage.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.AdvancingObjective
                    : AutoTaskPhase.NavigatingToStage),
            GameUiStateId.OdysseyStageVictory or
            GameUiStateId.OdysseySettlement or
            GameUiStateId.OdysseyReward => AutoTaskDecision.Navigate(
                "Advance the Odyssey result flow.",
                AutoTaskPhase.AdvancingObjective),
            GameUiStateId.MainMenu => AutoTaskDecision.Fail(
                "Odyssey auto task expects the game to already be inside the Odyssey flow. Main-menu entry is not implemented yet."),
            _ => AutoTaskDecision.Wait(
                $"Waiting for a recognizable Odyssey UI state. Current state: {snapshot.State}.",
                DefaultWaitDelayMs)
        });
    }

    private static AutoTaskDecision TryBuildStartScriptDecision(AutoTaskRuntimeState state)
    {
        var runState = GetScriptRunState(state);
        if (runState == OdysseyAutoTaskScriptRunState.Running)
        {
            return AutoTaskDecision.Wait(
                "Odyssey script is already running for the current stage.",
                DefaultWaitDelayMs,
                AutoTaskPhase.ExecutingScript);
        }

        if (runState == OdysseyAutoTaskScriptRunState.FinishedCurrentStage)
        {
            return AutoTaskDecision.Wait(
                "Odyssey script already finished for the current stage. Waiting for the result flow.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult);
        }

        if (!TryGetCurrentScriptPath(state, out var scriptPath, out var stageIndex, out var scriptCount))
        {
            return AutoTaskDecision.Fail(
                $"Entered Odyssey stage {stageIndex + 1}, but only {scriptCount} script(s) are configured. Add more script IDs and retry.");
        }

        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.Running);
        return AutoTaskDecision.StartScript(
            new AutoTaskScriptQuery
            {
                Kind = AutoTaskKind.Odyssey,
                StageTarget = state.Request.StageTarget,
                PreferredFilePath = scriptPath,
                Description = "Resolve the configured Odyssey stage script for execution."
            },
            $"Entered Odyssey stage {stageIndex + 1}. Start the configured script.",
            AutoTaskPhase.ExecutingScript);
    }

    private static AutoTaskDecision DecideAfterScriptExecution(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot)
    {
        state.ClearPendingScriptOutcome();
        state.ClearActiveScript();
        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.FinishedCurrentStage);

        return snapshot.State switch
        {
            GameUiStateId.InLevel or GameUiStateId.OdysseyLoading => AutoTaskDecision.Wait(
                "Odyssey script finished for the current stage. Waiting for the result UI.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult),
            _ => AutoTaskDecision.Navigate(
                "Odyssey script completed. Continue the result flow.",
                AutoTaskPhase.AdvancingObjective)
        };
    }

    private static void EnsureScriptQueueInitialized(AutoTaskRuntimeState state)
    {
        if (state.TryGetProperty<IReadOnlyList<string>>(OdysseyAutoTaskStateKeys.ScriptQueue, out _))
        {
            return;
        }

        state.SetProperty(OdysseyAutoTaskStateKeys.ScriptQueue, state.Request.PreferredScriptPaths);
        state.SetProperty(OdysseyAutoTaskStateKeys.CurrentStageIndex, 0);
        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.NotStarted);
    }

    private static void AdvanceStageProgressIfNeeded(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        if (GetScriptRunState(state) != OdysseyAutoTaskScriptRunState.FinishedCurrentStage)
        {
            return;
        }

        switch (snapshot.State)
        {
            case GameUiStateId.OdysseyCrew:
            case GameUiStateId.OdysseyLoading:
                AdvanceToNextStage(state);
                break;
            case GameUiStateId.OdysseyStart:
                ResetToFirstStage(state);
                break;
            case GameUiStateId.InLevel when state.Phase == AutoTaskPhase.AdvancingObjective:
                AdvanceToNextStage(state);
                break;
        }
    }

    private static void RecoverScriptLifecycleIfNeeded(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        if (GetScriptRunState(state) != OdysseyAutoTaskScriptRunState.Running ||
            !ShouldResetScriptLifecycle(snapshot.State))
        {
            return;
        }

        state.ClearActiveScript();
        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.NotStarted);
    }

    private static bool ShouldResetScriptLifecycle(GameUiStateId state)
    {
        return state is GameUiStateId.OdysseyStart or GameUiStateId.OdysseyCrew or GameUiStateId.OdysseyLoading;
    }

    private static void AdvanceToNextStage(AutoTaskRuntimeState state)
    {
        var currentIndex = GetCurrentStageIndex(state);
        state.SetProperty(OdysseyAutoTaskStateKeys.CurrentStageIndex, currentIndex + 1);
        state.ClearActiveScript();
        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.NotStarted);
    }

    private static void ResetToFirstStage(AutoTaskRuntimeState state)
    {
        state.SetProperty(OdysseyAutoTaskStateKeys.CurrentStageIndex, 0);
        state.ClearActiveScript();
        SetScriptRunState(state, OdysseyAutoTaskScriptRunState.NotStarted);
    }

    private static bool TryGetCurrentScriptPath(
        AutoTaskRuntimeState state,
        out string scriptPath,
        out int stageIndex,
        out int scriptCount)
    {
        scriptPath = string.Empty;
        stageIndex = GetCurrentStageIndex(state);

        if (!state.TryGetProperty<IReadOnlyList<string>>(OdysseyAutoTaskStateKeys.ScriptQueue, out var scriptQueue))
        {
            scriptCount = 0;
            return false;
        }

        scriptCount = scriptQueue.Count;
        if (stageIndex < 0 || stageIndex >= scriptQueue.Count)
        {
            return false;
        }

        scriptPath = scriptQueue[stageIndex];
        return !string.IsNullOrWhiteSpace(scriptPath);
    }

    private static int GetCurrentStageIndex(AutoTaskRuntimeState state)
    {
        return state.TryGetProperty<int>(OdysseyAutoTaskStateKeys.CurrentStageIndex, out var currentIndex)
            ? currentIndex
            : 0;
    }

    private static OdysseyAutoTaskScriptRunState GetScriptRunState(AutoTaskRuntimeState state)
    {
        return state.TryGetProperty<OdysseyAutoTaskScriptRunState>(OdysseyAutoTaskStateKeys.ScriptRunState, out var runState)
            ? runState
            : OdysseyAutoTaskScriptRunState.NotStarted;
    }

    private static void SetScriptRunState(
        AutoTaskRuntimeState state,
        OdysseyAutoTaskScriptRunState runState)
    {
        state.SetProperty(OdysseyAutoTaskStateKeys.ScriptRunState, runState);
    }
}
