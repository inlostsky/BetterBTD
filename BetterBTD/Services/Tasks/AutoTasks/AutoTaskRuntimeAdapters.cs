using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Core.ScriptExecution;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.ScriptExecution;

namespace BetterBTD.Services.Tasks.AutoTasks;

public sealed class UnresolvedAutoTaskScriptResolver : IAutoTaskScriptResolver
{
    private static readonly Lazy<UnresolvedAutoTaskScriptResolver> InstanceHolder =
        new(() => new UnresolvedAutoTaskScriptResolver());

    private UnresolvedAutoTaskScriptResolver()
    {
    }

    public static UnresolvedAutoTaskScriptResolver Instance => InstanceHolder.Value;

    public Task<AutoTaskScriptResolution> ResolveAsync(
        AutoTaskScriptQuery query,
        AutoTaskRuntimeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(state);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new AutoTaskScriptResolution
        {
            IsResolved = false,
            Query = query,
            Message = "Auto-task script resolution has not been implemented yet."
        });
    }
}

public sealed class ScriptTaskFlowAutoTaskScriptExecutorAdapter : IAutoTaskScriptExecutor
{
    private static readonly Lazy<ScriptTaskFlowAutoTaskScriptExecutorAdapter> InstanceHolder =
        new(() => new ScriptTaskFlowAutoTaskScriptExecutorAdapter());

    private readonly ScriptTaskFlowExecutor _scriptTaskFlowExecutor;

    private ScriptTaskFlowAutoTaskScriptExecutorAdapter()
        : this(ScriptTaskFlowExecutor.Instance)
    {
    }

    internal ScriptTaskFlowAutoTaskScriptExecutorAdapter(ScriptTaskFlowExecutor scriptTaskFlowExecutor)
    {
        _scriptTaskFlowExecutor = scriptTaskFlowExecutor ?? throw new ArgumentNullException(nameof(scriptTaskFlowExecutor));
    }

    public static ScriptTaskFlowAutoTaskScriptExecutorAdapter Instance => InstanceHolder.Value;

    public bool IsRunning => _scriptTaskFlowExecutor.IsRunning;

    public bool RequestPause()
    {
        return _scriptTaskFlowExecutor.RequestPause();
    }

    public bool Resume()
    {
        return _scriptTaskFlowExecutor.Resume();
    }

    public Task<ScriptExecutionResult> ExecuteAsync(
        string filePath,
        ScriptExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        return _scriptTaskFlowExecutor.ExecuteAsync(filePath, options, cancellationToken);
    }
}

public static class AutoTaskRuntimeServiceFactory
{
    public static AutoTaskRuntimeServices CreateDefault()
    {
        return new AutoTaskRuntimeServices
        {
            GameUiState = GameUiStateService.Instance,
            Navigator = GameUiNavigator.Instance,
            UiActionExecutor = GameUiActionExecutor.Instance,
            ScriptResolver = UnresolvedAutoTaskScriptResolver.Instance,
            ScriptExecutor = ScriptTaskFlowAutoTaskScriptExecutorAdapter.Instance
        };
    }
}
