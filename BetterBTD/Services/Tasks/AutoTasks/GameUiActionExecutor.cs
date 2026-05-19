using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Services.Tasks.Input;

namespace BetterBTD.Services.Tasks.AutoTasks;

public sealed class GameUiActionExecutor : IGameUiActionExecutor
{
    private static readonly Lazy<GameUiActionExecutor> InstanceHolder = new(() => new GameUiActionExecutor());

    private readonly ScriptInputSimulationService _inputSimulationService;
    private readonly IGameUiElementLocator _elementLocator;

    private GameUiActionExecutor()
        : this(ScriptInputSimulationService.Instance, UnimplementedGameUiElementLocator.Instance)
    {
    }

    internal GameUiActionExecutor(
        ScriptInputSimulationService inputSimulationService,
        IGameUiElementLocator elementLocator)
    {
        _inputSimulationService = inputSimulationService ?? throw new ArgumentNullException(nameof(inputSimulationService));
        _elementLocator = elementLocator ?? throw new ArgumentNullException(nameof(elementLocator));
    }

    public static GameUiActionExecutor Instance => InstanceHolder.Value;

    public Task<GameUiActionExecutionResult> ExecuteAsync(
        GameUiNavigationStep step,
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        if (step.ActionKind is GameUiActionKind.None or GameUiActionKind.Wait)
        {
            return Task.FromResult(new GameUiActionExecutionResult
            {
                Succeeded = true,
                Message = step.Description,
                RecommendedDelayMs = step.PostActionDelayMs
            });
        }

        if (!_elementLocator.TryLocateScriptPoint(
            step.ActionKind,
            state.Request.StageTarget,
            snapshot,
            out var scriptPoint,
            out var failureMessage))
        {
            return Task.FromResult(new GameUiActionExecutionResult
            {
                Succeeded = false,
                Message = string.IsNullOrWhiteSpace(failureMessage)
                    ? $"No locator is available for action '{step.ActionKind}'."
                    : failureMessage,
                RecommendedDelayMs = step.PostActionDelayMs
            });
        }

        _inputSimulationService.PrepareTargetWindowForInput();
        _inputSimulationService.ClickMouseAtScriptCoordinate(scriptPoint);

        return Task.FromResult(new GameUiActionExecutionResult
        {
            Succeeded = true,
            Message = step.Description,
            RecommendedDelayMs = step.PostActionDelayMs
        });
    }
}

internal sealed class UnimplementedGameUiElementLocator : IGameUiElementLocator
{
    private static readonly Lazy<UnimplementedGameUiElementLocator> InstanceHolder =
        new(() => new UnimplementedGameUiElementLocator());

    private UnimplementedGameUiElementLocator()
    {
    }

    public static UnimplementedGameUiElementLocator Instance => InstanceHolder.Value;

    public bool TryLocateScriptPoint(
        GameUiActionKind actionKind,
        StageEntryTarget target,
        GameUiSnapshot snapshot,
        out System.Windows.Point scriptPoint,
        out string failureMessage)
    {
        scriptPoint = default;
        failureMessage = $"Locator for UI action '{actionKind}' is not implemented yet.";
        return false;
    }
}
