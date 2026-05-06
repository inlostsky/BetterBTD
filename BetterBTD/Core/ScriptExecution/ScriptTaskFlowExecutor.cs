using BetterBTD.Models.ScriptExecution;
using BetterBTD.Core.ScriptExecution.Runtime;
using BetterBTD.Services.ScriptExecution;
using BetterBTD.Core.ScriptExecution.Handlers;

namespace BetterBTD.Core.ScriptExecution;

public sealed class ScriptTaskFlowExecutor
{
    private static readonly Lazy<ScriptTaskFlowExecutor> InstanceHolder = new(() => new ScriptTaskFlowExecutor());

    private readonly object _syncRoot = new();
    private readonly ScriptTaskFlowService _scriptTaskFlowService;
    private readonly ScriptInstructionHandlerRegistry _handlerRegistry;

    private bool _isRunning;

    private ScriptTaskFlowExecutor()
    {
        _scriptTaskFlowService = ScriptTaskFlowService.Instance;
        _handlerRegistry = ScriptInstructionHandlerRegistry.Instance;
    }

    public static ScriptTaskFlowExecutor Instance => InstanceHolder.Value;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRunning;
            }
        }
    }

    public Task<ScriptExecutionResult> ExecuteAsync(
        string filePath,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var taskFlow = _scriptTaskFlowService.LoadFromFile(filePath);
        return ExecuteAsync(taskFlow, options, cancellationToken);
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(
        ScriptTaskFlow taskFlow,
        ScriptExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskFlow);

        options ??= new ScriptExecutionOptions();
        var runtimeServices = options.RuntimeServices ?? ScriptExecutionRuntimeServiceFactory.CreateDefault();
        var executedStepCount = 0;
        var lastCompletedStepIndex = -1;

        EnterRunningState();
        try
        {
            ValidateRuntimePrerequisites(options, runtimeServices);

            var state = new ScriptExecutionState();
            state.SeedMonkeyStates(taskFlow.Document.MonkeyObjects);

            foreach (var step in taskFlow.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var context = new ScriptInstructionExecutionContext
                {
                    TaskFlow = taskFlow,
                    Step = step,
                    State = state,
                    Options = options,
                    RuntimeServices = runtimeServices
                };

                var handler = _handlerRegistry.GetRequiredHandler(step.CommandType);
                await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

                executedStepCount++;
                lastCompletedStepIndex = step.Index;

                var instructionIntervalMs = ResolveInstructionInterval(step, options);
                if (instructionIntervalMs > 0)
                {
                    await Task.Delay(instructionIntervalMs, cancellationToken).ConfigureAwait(false);
                }
            }

            return new ScriptExecutionResult
            {
                Status = ScriptExecutionStatus.Completed,
                ExecutedStepCount = executedStepCount,
                LastCompletedStepIndex = lastCompletedStepIndex
            };
        }
        catch (OperationCanceledException)
        {
            return new ScriptExecutionResult
            {
                Status = ScriptExecutionStatus.Cancelled,
                ExecutedStepCount = executedStepCount,
                LastCompletedStepIndex = lastCompletedStepIndex
            };
        }
        catch (Exception ex)
        {
            return new ScriptExecutionResult
            {
                Status = ScriptExecutionStatus.Failed,
                ExecutedStepCount = executedStepCount,
                LastCompletedStepIndex = lastCompletedStepIndex,
                Exception = ex
            };
        }
        finally
        {
            ExitRunningState();
        }
    }

    private static int ResolveInstructionInterval(ScriptTaskFlowStep step, ScriptExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(options);

        if (options.OverrideInstructionIntervalMs.HasValue)
        {
            return Math.Max(0, options.OverrideInstructionIntervalMs.Value);
        }

        return Math.Max(0, step.Instruction.IntervalToNextInstructionMs);
    }

    private static void ValidateRuntimePrerequisites(
        ScriptExecutionOptions options,
        ScriptExecutionRuntimeServices runtimeServices)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runtimeServices);

        if (options.RequireCaptureService && !runtimeServices.Capture.IsRunning)
        {
            throw new InvalidOperationException("Game capture service must be running before executing a script task flow.");
        }

        if (options.RequireTargetWindow && !runtimeServices.Input.TryGetTargetWindowInfo(out _))
        {
            throw new InvalidOperationException("Target game window is not available for script execution.");
        }
    }

    private void EnterRunningState()
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Another script task flow is already running.");
            }

            _isRunning = true;
        }
    }

    private void ExitRunningState()
    {
        lock (_syncRoot)
        {
            _isRunning = false;
        }
    }
}
