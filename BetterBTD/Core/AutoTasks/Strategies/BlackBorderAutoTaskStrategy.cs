using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.MyScripts;
using BetterBTD.Services.MyScripts;
using BetterBTD.Services.Tasks.AutoTasks;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class BlackBorderAutoTaskStrategy : IAutoTaskStrategy
{
    private const int DefaultWaitDelayMs = 500;

    private readonly ManagedAutoTaskScriptResolver _scriptResolver;
    private readonly ScriptDocumentService _scriptDocumentService;

    public BlackBorderAutoTaskStrategy()
        : this(ManagedAutoTaskScriptResolver.Instance, ScriptDocumentService.Instance)
    {
    }

    internal BlackBorderAutoTaskStrategy(
        ManagedAutoTaskScriptResolver scriptResolver,
        ScriptDocumentService scriptDocumentService)
    {
        _scriptResolver = scriptResolver ?? throw new ArgumentNullException(nameof(scriptResolver));
        _scriptDocumentService = scriptDocumentService ?? throw new ArgumentNullException(nameof(scriptDocumentService));
    }

    public AutoTaskKind Kind => AutoTaskKind.BlackBorder;

    public async Task<AutoTaskDecision> DecideNextAsync(
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        EnsureTaskQueueInitialized(state);
        AdvanceCurrentTaskIfNeeded(state, snapshot);
        RecoverScriptLifecycleIfNeeded(state, snapshot);

        if (!TryGetCurrentTask(state, out _))
        {
            return snapshot.State == GameUiStateId.MainMenu
                ? AutoTaskDecision.Complete("Black border task queue completed.")
                : AutoTaskDecision.Navigate(
                    "Black border task queue completed. Return to the main menu.",
                    AutoTaskPhase.AdvancingObjective);
        }

        if (state.HasPendingScriptOutcome)
        {
            return DecideAfterScriptExecution(state, snapshot);
        }

        var preloadDecision = await TryPreloadCurrentScriptContextAsync(state, cancellationToken).ConfigureAwait(false);
        if (preloadDecision is not null)
        {
            return preloadDecision;
        }

        return snapshot.State switch
        {
            GameUiStateId.InLevel => TryBuildStartScriptDecision(state),
            GameUiStateId.Loading => AutoTaskDecision.Wait(
                "Waiting for the black border stage to finish loading.",
                DefaultWaitDelayMs,
                AutoTaskPhase.WaitingForLevelLoad),
            GameUiStateId.MainMenu => AutoTaskDecision.Navigate(
                "Open the black border stage flow from the main menu.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.PreparingStage
                    : AutoTaskPhase.NavigatingToStage),
            _ => AutoTaskDecision.Navigate(
                "Advance the black border navigation flow.",
                state.Phase == AutoTaskPhase.AdvancingObjective
                    ? AutoTaskPhase.AdvancingObjective
                    : AutoTaskPhase.NavigatingToStage)
        };
    }

    private async Task<AutoTaskDecision?> TryPreloadCurrentScriptContextAsync(
        AutoTaskRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (state.TryGetProperty<BlackBorderAutoTaskScriptContext>(
                BlackBorderAutoTaskStateKeys.ResolvedScriptContext,
                out _))
        {
            return null;
        }

        while (TryGetCurrentTask(state, out var currentTask))
        {
            var query = BuildScriptQuery(currentTask);
            var resolution = await _scriptResolver.ResolveAsync(query, state, cancellationToken).ConfigureAwait(false);
            if (!resolution.IsResolved || string.IsNullOrWhiteSpace(resolution.FilePath))
            {
                AdvanceToNextTask(state);
                continue;
            }

            var scriptDocument = _scriptDocumentService.LoadCompatible(resolution.FilePath).Document;
            var context = new BlackBorderAutoTaskScriptContext
            {
                Category = currentTask.Category,
                Target = currentTask.Target,
                Hero = ParseEnum(scriptDocument.Metadata.Hero, HeroType.Quincy),
                FilePath = resolution.FilePath
            };

            state.RecordScriptResolution(resolution);
            state.SetProperty(BlackBorderAutoTaskStateKeys.ResolvedScriptContext, context);
            state.SetProperty(BlackBorderAutoTaskStateKeys.HeroSelected, false);
            state.SetProperty(BlackBorderAutoTaskStateKeys.MapLocateAttempts, 0);
            state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
            SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
            return null;
        }

        return AutoTaskDecision.Navigate(
            "No black border script bindings remain in the current queue. Return to the main menu.",
            AutoTaskPhase.AdvancingObjective);
    }

    private static AutoTaskDecision TryBuildStartScriptDecision(AutoTaskRuntimeState state)
    {
        if (!state.TryGetProperty<BlackBorderAutoTaskScriptContext>(
                BlackBorderAutoTaskStateKeys.ResolvedScriptContext,
                out var context))
        {
            return AutoTaskDecision.Navigate(
                "No black border script is loaded for the current stage. Exit the stage.",
                AutoTaskPhase.AdvancingObjective);
        }

        var runState = GetScriptRunState(state);
        if (runState == BlackBorderAutoTaskScriptRunState.Running)
        {
            return AutoTaskDecision.Wait(
                "Black border script is already running for the current stage.",
                DefaultWaitDelayMs,
                AutoTaskPhase.ExecutingScript);
        }

        if (runState == BlackBorderAutoTaskScriptRunState.FinishedCurrentStage)
        {
            return AutoTaskDecision.Wait(
                "Black border script already finished for the current stage. Waiting for the result flow.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult);
        }

        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.Running);
        return AutoTaskDecision.StartScript(
            new AutoTaskScriptQuery
            {
                Kind = AutoTaskKind.BlackBorder,
                StageTarget = context.Target,
                PreferredFilePath = context.FilePath,
                SlotId = ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(
                    context.Target.Map,
                    context.Target.Difficulty,
                    context.Target.Mode),
                RequiredTags = ["black-border"],
                Description = "Resolve the preloaded black border script for execution."
            },
            "Black border stage entry completed. Start the resolved script.",
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
                "Black border script already finished for the current stage. Waiting for the result UI.",
                DefaultWaitDelayMs,
                AutoTaskPhase.SettlingResult),
            GameUiStateId.Defeat => AutoTaskDecision.Navigate(
                "Black border stage ended in defeat. Return to the main menu and continue the queue.",
                AutoTaskPhase.AdvancingObjective),
            _ => AutoTaskDecision.Navigate(
                "Black border script completed. Continue the result flow.",
                AutoTaskPhase.AdvancingObjective)
        };
    }

    private static AutoTaskScriptQuery BuildScriptQuery(BlackBorderAutoTaskStageTask task)
    {
        return new AutoTaskScriptQuery
        {
            Kind = AutoTaskKind.BlackBorder,
            StageTarget = task.Target,
            SlotId = ManagedScriptSlotIdFactory.CreateBlackBorderSlotId(
                task.Target.Map,
                task.Target.Difficulty,
                task.Target.Mode),
            RequiredTags = ["black-border"],
            Description = "Resolve a black-border script for the current queued stage."
        };
    }

    private static void EnsureTaskQueueInitialized(AutoTaskRuntimeState state)
    {
        if (state.TryGetProperty<IReadOnlyList<BlackBorderAutoTaskStageTask>>(
                BlackBorderAutoTaskStateKeys.TaskQueue,
                out _))
        {
            return;
        }

        var (category, selectedMapCode) = ParseScope(state.Request);
        var queue = BuildTaskQueue(category, selectedMapCode);
        state.SetProperty(BlackBorderAutoTaskStateKeys.TaskQueue, queue);
        state.SetProperty(BlackBorderAutoTaskStateKeys.CurrentTaskIndex, 0);
        state.SetProperty(BlackBorderAutoTaskStateKeys.MapLocateAttempts, 0);
        state.SetProperty(BlackBorderAutoTaskStateKeys.HeroSelected, false);
        state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
    }

    private static void AdvanceCurrentTaskIfNeeded(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        var skipRequested = state.TryGetProperty<bool>(
                                BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested,
                                out var shouldSkip) &&
                            shouldSkip;
        if (skipRequested)
        {
            AdvanceToNextTask(state);
            return;
        }

        if (GetScriptRunState(state) == BlackBorderAutoTaskScriptRunState.FinishedCurrentStage &&
            snapshot.State == GameUiStateId.MainMenu)
        {
            AdvanceToNextTask(state);
        }
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

    private static void AdvanceToNextTask(AutoTaskRuntimeState state)
    {
        var currentIndex = state.TryGetProperty<int>(BlackBorderAutoTaskStateKeys.CurrentTaskIndex, out var storedIndex)
            ? storedIndex
            : 0;

        state.SetProperty(BlackBorderAutoTaskStateKeys.CurrentTaskIndex, currentIndex + 1);
        state.RemoveProperty(BlackBorderAutoTaskStateKeys.ResolvedScriptContext);
        state.SetProperty(BlackBorderAutoTaskStateKeys.HeroSelected, false);
        state.SetProperty(BlackBorderAutoTaskStateKeys.MapLocateAttempts, 0);
        state.SetProperty(BlackBorderAutoTaskStateKeys.SkipCurrentTaskRequested, false);
        state.ClearActiveScript();
        SetScriptRunState(state, BlackBorderAutoTaskScriptRunState.NotStarted);
    }

    private static bool TryGetCurrentTask(AutoTaskRuntimeState state, out BlackBorderAutoTaskStageTask task)
    {
        task = null!;

        if (!state.TryGetProperty<IReadOnlyList<BlackBorderAutoTaskStageTask>>(
                BlackBorderAutoTaskStateKeys.TaskQueue,
                out var queue))
        {
            return false;
        }

        var currentIndex = state.TryGetProperty<int>(BlackBorderAutoTaskStateKeys.CurrentTaskIndex, out var storedIndex)
            ? storedIndex
            : 0;
        if (currentIndex < 0 || currentIndex >= queue.Count)
        {
            return false;
        }

        task = queue[currentIndex];
        return true;
    }

    private static IReadOnlyList<BlackBorderAutoTaskStageTask> BuildTaskQueue(
        BlackBorderMapCategory category,
        string selectedMapCode)
    {
        var maps = !string.Equals(selectedMapCode, "__all__", StringComparison.OrdinalIgnoreCase) &&
                   Enum.TryParse<GameMapType>(selectedMapCode, ignoreCase: true, out var selectedMap)
            ? GameElementCatalog.Maps.Where(map => map.Type == selectedMap)
            : BlackBorderTaskCatalog.GetMapsByCategory(category);

        return maps
            .SelectMany(
                map => BlackBorderTaskCatalog.Difficulties,
                (map, difficulty) => new { map.Type, Difficulty = difficulty })
            .SelectMany(
                item => BlackBorderTaskCatalog.GetModesForDifficulty(item.Difficulty),
                (item, mode) => new BlackBorderAutoTaskStageTask
                {
                    Category = category,
                    Target = new StageEntryTarget
                    {
                        Map = item.Type,
                        Difficulty = item.Difficulty,
                        Mode = mode
                    }
                })
            .ToList();
    }

    private static (BlackBorderMapCategory Category, string SelectedMapCode) ParseScope(AutoTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = ParseEnum(GetScopeValue(request.VariantKey, "category"), InferCategoryFromMap(request.StageTarget.Map));
        var mapCode = GetScopeValue(request.VariantKey, "map");
        return (category, string.IsNullOrWhiteSpace(mapCode) ? "__all__" : mapCode);
    }

    private static string GetScopeValue(string variantKey, string key)
    {
        if (string.IsNullOrWhiteSpace(variantKey) || string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        foreach (var segment in variantKey.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 2 && string.Equals(pair[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return pair[1];
            }
        }

        return string.Empty;
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
