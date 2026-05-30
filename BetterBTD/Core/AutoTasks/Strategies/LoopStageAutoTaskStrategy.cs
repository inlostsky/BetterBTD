using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;
using BetterBTD.Services.MyScripts;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class LoopStageAutoTaskStrategy : IAutoTaskStrategy
{
    private const int DefaultWaitDelayMs = 500;
    private readonly ScriptDocumentService _scriptDocumentService;

    public LoopStageAutoTaskStrategy()
        : this(ScriptDocumentService.Instance)
    {
    }

    internal LoopStageAutoTaskStrategy(
        ScriptDocumentService scriptDocumentService)
    {
        _scriptDocumentService = scriptDocumentService ?? throw new ArgumentNullException(nameof(scriptDocumentService));
    }

    public AutoTaskKind Kind => AutoTaskKind.LoopStage;

    public Task<AutoTaskDecision> DecideNextAsync(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        var skippedDecision = TryHandleSkippedStage(state);
        if (skippedDecision is not null)
        {
            return Task.FromResult(skippedDecision);
        }

        ResetForNextLoopIfNeeded(state, snapshot);
        RecoverScriptLifecycleIfNeeded(state, snapshot);

        if (state.HasPendingScriptOutcome)
        {
            return Task.FromResult(DecideAfterScriptExecution(state, snapshot));
        }

        var preloadDecision = TryPreloadScriptContext(state, cancellationToken);
        if (preloadDecision is not null)
        {
            return Task.FromResult(preloadDecision);
        }

        return Task.FromResult(snapshot.State switch
        {
            GameUiStateId.InLevel => TryBuildStartScriptDecision(state),
            GameUiStateId.Loading => AutoTaskDecision.Wait(
                "Waiting for the configured stage to finish loading.",
                DefaultWaitDelayMs,
                AutoTaskPhase.WaitingForLevelLoad),
            GameUiStateId.MainMenu => AutoTaskDecision.Navigate(
                "Open the configured stage flow from the main menu.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.PreparingStage
                    : AutoTaskPhase.NavigatingToStage),
            _ => AutoTaskDecision.Navigate(
                "Advance the configured stage navigation flow.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.AdvancingObjective
                    : AutoTaskPhase.NavigatingToStage)
        });
    }

    private AutoTaskDecision? TryPreloadScriptContext(
        AutoTaskRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (state.TryGetProperty<BlackBorderAutoTaskScriptContext>(
                BlackBorderAutoTaskStateKeys.ResolvedScriptContext,
                out _))
        {
            return null;
        }

        var filePath = state.Request.PreferredScriptPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return AutoTaskDecision.Fail("Loop-stage script ID is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var scriptDocument = _scriptDocumentService.LoadCompatible(filePath).Document;
        var target = new StageEntryTarget
        {
            Map = ParseEnum(scriptDocument.Metadata.Map, GameMapType.MonkeyMeadow),
            Difficulty = ParseEnum(scriptDocument.Metadata.Difficulty, StageDifficulty.Easy),
            Mode = ParseEnum(scriptDocument.Metadata.Mode, StageMode.Standard)
        };
        var context = new BlackBorderAutoTaskScriptContext
        {
            Category = InferCategoryFromMap(target.Map),
            Target = target,
            Hero = ParseEnum(scriptDocument.Metadata.Hero, HeroType.Quincy),
            FilePath = filePath
        };

        state.SetProperty(BlackBorderAutoTaskStateKeys.ResolvedScriptContext, context);
        state.SetProperty(BlackBorderAutoTaskStateKeys.HeroSelected, false);
        state.SetProperty(BlackBorderAutoTaskStateKeys.MapLocateAttempts, 0);
        state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
        return null;
    }

    private static AutoTaskDecision TryBuildStartScriptDecision(AutoTaskRuntimeState state)
    {
        if (!state.TryGetProperty<BlackBorderAutoTaskScriptContext>(
                BlackBorderAutoTaskStateKeys.ResolvedScriptContext,
                out var context))
        {
            return AutoTaskDecision.Fail("Loop-stage script metadata was not loaded before entering the stage.");
        }

        var runState = GetScriptRunState(state);
        if (runState == BlackBorderAutoTaskScriptRunState.Running)
        {
            return AutoTaskDecision.Wait(
                "Configured stage script is already running for the current loop.",
                DefaultWaitDelayMs,
                AutoTaskPhase.ExecutingScript);
        }

        if (runState == BlackBorderAutoTaskScriptRunState.FinishedCurrentStage)
        {
            return AutoTaskDecision.Wait(
                "Configured stage script already finished for the current loop. Waiting for the result flow.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult);
        }

        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.Running);
        return AutoTaskDecision.StartScript(
            new AutoTaskScriptQuery
            {
                Kind = AutoTaskKind.LoopStage,
                StageTarget = context.Target,
                PreferredFilePath = context.FilePath,
                Description = "Resolve the configured loop-stage script for execution."
            },
            "Configured stage entry completed. Start the resolved script.",
            AutoTaskPhase.ExecutingScript);
    }

    private static AutoTaskDecision DecideAfterScriptExecution(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot)
    {
        state.ClearPendingScriptOutcome();
        state.ClearActiveScript();
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.FinishedCurrentStage);

        return snapshot.State switch
        {
            GameUiStateId.InLevel or GameUiStateId.Loading => AutoTaskDecision.Wait(
                "Configured stage script already finished for the current loop. Waiting for the result UI.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult),
            GameUiStateId.Defeat => AutoTaskDecision.Navigate(
                "Configured stage ended in defeat. Return to the main menu and retry.",
                AutoTaskPhase.AdvancingObjective),
            _ => AutoTaskDecision.Navigate(
                "Configured stage script completed. Continue the result flow and start the next loop.",
                AutoTaskPhase.AdvancingObjective)
        };
    }

    private static AutoTaskDecision? TryHandleSkippedStage(AutoTaskRuntimeState state)
    {
        var skipRequested = state.TryGetProperty<bool>(
                                BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested,
                                out var shouldSkip) &&
                            shouldSkip;
        if (!skipRequested)
        {
            return null;
        }

        state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
        return AutoTaskDecision.Fail("The configured stage could not be located after repeated attempts. Check the bound script metadata.");
    }

    private static void ResetForNextLoopIfNeeded(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        if (GetScriptRunState(state) != BlackBorderAutoTaskScriptRunState.FinishedCurrentStage ||
            snapshot.State != GameUiStateId.MainMenu)
        {
            return;
        }

        state.SetProperty(BlackBorderAutoTaskStateKeys.HeroSelected, false);
        state.SetProperty(BlackBorderAutoTaskStateKeys.MapLocateAttempts, 0);
        state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
        state.RemoveProperty(BlackBorderAutoTaskStateKeys.ResolvedScriptContext);
        state.ClearActiveScript();
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
    }

    private static void RecoverScriptLifecycleIfNeeded(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        if (GetScriptRunState(state) != BlackBorderAutoTaskScriptRunState.Running ||
            !ShouldResetScriptLifecycle(snapshot.State))
        {
            return;
        }

        state.ClearActiveScript();
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
    }

    private static bool ShouldResetScriptLifecycle(GameUiStateId state)
    {
        return state is
            GameUiStateId.MainMenu or
            GameUiStateId.MapCategorySelect or
            GameUiStateId.MapGrid or
            GameUiStateId.DifficultySelect or
            GameUiStateId.EasyModeSelect or
            GameUiStateId.MediumModeSelect or
            GameUiStateId.HardModeSelect or
            GameUiStateId.ModeSelect or
            GameUiStateId.HeroSelect or
            GameUiStateId.Returnable;
    }

    private static BlackBorderMapCategory InferCategoryFromMap(GameMapType map)
    {
        var definition = GameElementCatalog.Maps.FirstOrDefault(item => item.Type == map);
        return definition?.Tier switch
        {
            MapDifficultyTier.Beginner => BlackBorderMapCategory.Beginner,
            MapDifficultyTier.Intermediate => BlackBorderMapCategory.Intermediate,
            MapDifficultyTier.Advanced => BlackBorderMapCategory.Advanced,
            MapDifficultyTier.Expert => BlackBorderMapCategory.Expert,
            _ => BlackBorderMapCategory.Beginner
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static BlackBorderAutoTaskScriptRunState GetScriptRunState(AutoTaskRuntimeState state)
    {
        return state.TryGetProperty<BlackBorderAutoTaskScriptRunState>(
            BlackBorderAutoTaskStateKeys.ScriptRunState,
            out var runState)
            ? runState
            : BlackBorderAutoTaskScriptRunState.NotStarted;
    }

    private static void SetScriptRunState(
        AutoTaskRuntimeState state,
        BlackBorderAutoTaskScriptRunState runState)
    {
        state.SetProperty(BlackBorderAutoTaskStateKeys.ScriptRunState, runState);
    }
}
