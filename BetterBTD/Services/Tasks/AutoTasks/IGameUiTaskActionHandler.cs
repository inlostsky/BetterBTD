using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Services.Tasks.AutoTasks;

internal interface IGameUiTaskActionHandler
{
    AutoTaskKind Kind { get; }

    Task<GameUiActionExecutionResult> ExecuteAsync(
        GameUiNavigationStep step,
        AutoTaskRuntimeState state,
        GameUiSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
