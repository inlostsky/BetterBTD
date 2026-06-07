using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.RobotControl;
using BetterBTD.Services.Tasks.AutoTasks;

namespace BetterBTD.Core.RobotControl;

public sealed class RobotTaskCoordinator
{
    private static readonly Lazy<RobotTaskCoordinator> InstanceHolder = new(() => new RobotTaskCoordinator());

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly IRobotActionRegistry _actionRegistry;
    private readonly IReadOnlyList<IRobotUiAutomationRule> _uiAutomationRules;
    private readonly IGameUiStateService _gameUiStateService;
    private readonly GameCaptureService _gameCaptureService;
    private readonly ScriptInputSimulationService _inputSimulationService;
    private readonly CoordinateTransformService _coordinateTransformService;

    private RobotTaskRunState _runState = RobotTaskRunState.Stopped;
    private CancellationTokenSource? _runtimeCancellationSource;
    private RobotOperationSnapshot? _currentOperation;
    private RobotActionResponse? _lastResult;
    private GameUiSnapshot? _lastUiSnapshot;
    private string _listeningUrl = string.Empty;
    private long _operationSequence;

    public RobotTaskCoordinator()
        : this(
            RobotActionRegistry.Instance,
            RobotUiAutomationRuleRegistry.Instance.Rules,
            GameUiStateService.Instance,
            GameCaptureService.Instance,
            ScriptInputSimulationService.Instance,
            CoordinateTransformService.Instance)
    {
    }

    internal RobotTaskCoordinator(
        IRobotActionRegistry actionRegistry,
        IReadOnlyList<IRobotUiAutomationRule> uiAutomationRules,
        IGameUiStateService gameUiStateService,
        GameCaptureService gameCaptureService,
        ScriptInputSimulationService inputSimulationService,
        CoordinateTransformService coordinateTransformService)
    {
        _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
        _uiAutomationRules = uiAutomationRules ?? throw new ArgumentNullException(nameof(uiAutomationRules));
        _gameUiStateService = gameUiStateService ?? throw new ArgumentNullException(nameof(gameUiStateService));
        _gameCaptureService = gameCaptureService ?? throw new ArgumentNullException(nameof(gameCaptureService));
        _inputSimulationService = inputSimulationService ?? throw new ArgumentNullException(nameof(inputSimulationService));
        _coordinateTransformService = coordinateTransformService ?? throw new ArgumentNullException(nameof(coordinateTransformService));
    }

    public static RobotTaskCoordinator Instance => InstanceHolder.Value;

    public event EventHandler<RobotTaskStatusSnapshot>? StatusChanged;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _runState != RobotTaskRunState.Stopped;
            }
        }
    }

    public IReadOnlyList<RobotActionMetadata> GetActionMetadata()
    {
        return _actionRegistry.GetMetadata();
    }

    public RobotTaskStatusSnapshot GetStatusSnapshot()
    {
        lock (_syncRoot)
        {
            return BuildStatusSnapshotLocked();
        }
    }

    public void Start(string listeningUrl)
    {
        lock (_syncRoot)
        {
            _runtimeCancellationSource?.Cancel();
            _runtimeCancellationSource?.Dispose();
            _runtimeCancellationSource = new CancellationTokenSource();
            _runState = RobotTaskRunState.Listening;
            _listeningUrl = listeningUrl ?? string.Empty;
            _currentOperation = null;
            _lastUiSnapshot = null;
        }

        PublishStatusChanged();
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            if (_runState == RobotTaskRunState.Stopped)
            {
                return;
            }

            _runState = RobotTaskRunState.Stopping;
            _runtimeCancellationSource?.Cancel();
            _currentOperation = null;
        }

        PublishStatusChanged();

        lock (_syncRoot)
        {
            _runtimeCancellationSource?.Dispose();
            _runtimeCancellationSource = null;
            _runState = RobotTaskRunState.Stopped;
            _listeningUrl = string.Empty;
        }

        PublishStatusChanged();
    }

    public async Task<bool> TryRunUiAutomationAsync(CancellationToken cancellationToken = default)
    {
        var runtimeToken = GetRequiredRuntimeTokenOrDefault(cancellationToken);
        if (!await _operationGate.WaitAsync(0, runtimeToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            if (!IsListening())
            {
                return false;
            }

            var snapshot = await CaptureSnapshotAsync(runtimeToken).ConfigureAwait(false);
            var rule = FindUiAutomationRule(snapshot);
            if (rule is null)
            {
                return false;
            }

            var operationId = CreateOperationId();
            var operation = CreateOperationSnapshot(
                operationId,
                rule.Key,
                "Running UI automation rule.",
                snapshot);

            SetCurrentOperation(RobotTaskRunState.BusyWithUiAutomation, operation);

            var context = CreateActionContext(operationId, snapshot);
            var progress = new Progress<RobotActionProgress>(UpdateProgress);
            RobotActionResult result;
            try
            {
                result = await rule.ExecuteAsync(context, progress, runtimeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = RobotActionResult.Failed(RobotActionErrorCodes.Cancelled, "UI automation was cancelled.");
            }
            catch (Exception ex)
            {
                result = RobotActionResult.Failed(RobotActionErrorCodes.Failed, ex.Message);
            }

            CompleteCurrentOperation(rule.Key, operationId, requestId: string.Empty, accepted: true, result, snapshot);
            return true;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<RobotActionResponse> ExecuteActionAsync(
        string actionKey,
        RobotActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedActionKey = actionKey?.Trim() ?? string.Empty;
        var operationId = CreateOperationId();

        if (!IsRuntimeActive())
        {
            return BuildRejectedResponse(
                request.RequestId,
                operationId,
                normalizedActionKey,
                RobotActionErrorCodes.TaskNotRunning,
                "Robot task is not running.",
                RobotGameStateSnapshot.From(GetLastUiSnapshot()));
        }

        if (!_actionRegistry.TryGetAction(normalizedActionKey, out var action))
        {
            return BuildRejectedResponse(
                request.RequestId,
                operationId,
                normalizedActionKey,
                RobotActionErrorCodes.InvalidAction,
                $"Robot action '{normalizedActionKey}' is not registered.",
                RobotGameStateSnapshot.From(GetLastUiSnapshot()));
        }

        var runtimeToken = GetRequiredRuntimeTokenOrDefault(cancellationToken);
        if (!await _operationGate.WaitAsync(0, runtimeToken).ConfigureAwait(false))
        {
            return BuildRejectedResponse(
                request.RequestId,
                operationId,
                action.Key,
                RobotActionErrorCodes.Busy,
                "Robot task is busy and does not queue action requests.",
                RobotGameStateSnapshot.From(GetLastUiSnapshot()));
        }

        try
        {
            var snapshot = await CaptureSnapshotAsync(runtimeToken).ConfigureAwait(false);
            if (FindUiAutomationRule(snapshot) is { } uiAutomationRule)
            {
                return BuildRejectedResponse(
                    request.RequestId,
                    operationId,
                    action.Key,
                    RobotActionErrorCodes.UiAutomationRequired,
                    $"Current UI state requires high-priority automation rule '{uiAutomationRule.Key}'.",
                    RobotGameStateSnapshot.From(snapshot));
            }

            var context = CreateActionContext(operationId, snapshot);
            var precheck = await action.CheckAsync(context, request, runtimeToken).ConfigureAwait(false);
            if (!precheck.CanExecute)
            {
                return BuildRejectedResponse(
                    request.RequestId,
                    operationId,
                    action.Key,
                    precheck.Code,
                    precheck.Message,
                    RobotGameStateSnapshot.From(snapshot),
                    precheck.Data);
            }

            SetCurrentOperation(
                RobotTaskRunState.BusyWithRobotAction,
                CreateOperationSnapshot(operationId, action.Key, precheck.Message, snapshot));

            var timeoutMs = Math.Max(1000, action.Metadata.TimeoutMs);
            using var timeoutCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(runtimeToken);
            timeoutCancellationSource.CancelAfter(timeoutMs);

            var progress = new Progress<RobotActionProgress>(UpdateProgress);
            RobotActionResult result;
            try
            {
                result = await action
                    .ExecuteAsync(context, request, progress, timeoutCancellationSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!runtimeToken.IsCancellationRequested)
            {
                result = new RobotActionResult
                {
                    Status = RobotActionExecutionStatus.TimedOut,
                    Code = RobotActionErrorCodes.TimedOut,
                    Message = $"Action '{action.Key}' timed out after {timeoutMs} ms."
                };
            }
            catch (OperationCanceledException)
            {
                result = new RobotActionResult
                {
                    Status = RobotActionExecutionStatus.Cancelled,
                    Code = RobotActionErrorCodes.Cancelled,
                    Message = $"Action '{action.Key}' was cancelled."
                };
            }
            catch (Exception ex)
            {
                result = RobotActionResult.Failed(RobotActionErrorCodes.Failed, ex.Message);
            }

            return CompleteCurrentOperation(action.Key, operationId, request.RequestId, accepted: true, result, snapshot);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<GameUiSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _gameUiStateService.CaptureSnapshotAsync(cancellationToken).ConfigureAwait(false);
        lock (_syncRoot)
        {
            _lastUiSnapshot = snapshot;
        }

        PublishStatusChanged();
        return snapshot;
    }

    private IRobotUiAutomationRule? FindUiAutomationRule(GameUiSnapshot snapshot)
    {
        return _uiAutomationRules.FirstOrDefault(rule => rule.CanHandle(snapshot));
    }

    private RobotActionContext CreateActionContext(string operationId, GameUiSnapshot snapshot)
    {
        return new RobotActionContext
        {
            OperationId = operationId,
            CurrentSnapshot = snapshot,
            GameUiState = _gameUiStateService,
            GameCapture = _gameCaptureService,
            InputSimulation = _inputSimulationService,
            CoordinateTransform = _coordinateTransformService
        };
    }

    private CancellationToken GetRequiredRuntimeTokenOrDefault(CancellationToken fallback)
    {
        lock (_syncRoot)
        {
            return _runtimeCancellationSource?.Token ?? fallback;
        }
    }

    private bool IsListening()
    {
        lock (_syncRoot)
        {
            return _runState == RobotTaskRunState.Listening;
        }
    }

    private bool IsRuntimeActive()
    {
        lock (_syncRoot)
        {
            return _runState is
                RobotTaskRunState.Listening or
                RobotTaskRunState.BusyWithRobotAction or
                RobotTaskRunState.BusyWithUiAutomation;
        }
    }

    private GameUiSnapshot? GetLastUiSnapshot()
    {
        lock (_syncRoot)
        {
            return _lastUiSnapshot;
        }
    }

    private void SetCurrentOperation(RobotTaskRunState runState, RobotOperationSnapshot operation)
    {
        lock (_syncRoot)
        {
            _runState = runState;
            _currentOperation = operation;
        }

        PublishStatusChanged();
    }

    private void UpdateProgress(RobotActionProgress progress)
    {
        lock (_syncRoot)
        {
            if (_currentOperation is null ||
                !string.Equals(_currentOperation.OperationId, progress.OperationId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentOperation.Status = progress.Status;
            _currentOperation.Message = progress.Message;
            _currentOperation.ProgressPercent = progress.ProgressPercent;
            _currentOperation.LastUpdatedAt = progress.Timestamp;
        }

        PublishStatusChanged();
    }

    private RobotActionResponse CompleteCurrentOperation(
        string actionKey,
        string operationId,
        string requestId,
        bool accepted,
        RobotActionResult result,
        GameUiSnapshot snapshot)
    {
        var response = new RobotActionResponse
        {
            RequestId = requestId,
            OperationId = operationId,
            Action = actionKey,
            Accepted = accepted,
            Status = result.Status,
            Code = result.Code,
            Message = result.Message,
            Data = result.Data,
            State = RobotGameStateSnapshot.From(snapshot)
        };

        lock (_syncRoot)
        {
            if (_currentOperation is not null &&
                string.Equals(_currentOperation.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
            {
                _currentOperation.Status = result.Status;
                _currentOperation.Code = result.Code;
                _currentOperation.Message = result.Message;
                _currentOperation.ProgressPercent ??= 100d;
                _currentOperation.LastUpdatedAt = DateTimeOffset.UtcNow;
            }

            _lastResult = response;
            _currentOperation = null;
            if (_runState != RobotTaskRunState.Stopped && _runState != RobotTaskRunState.Stopping)
            {
                _runState = RobotTaskRunState.Listening;
            }
        }

        PublishStatusChanged();
        return response;
    }

    private RobotActionResponse BuildRejectedResponse(
        string requestId,
        string operationId,
        string action,
        string code,
        string message,
        RobotGameStateSnapshot state,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var response = new RobotActionResponse
        {
            RequestId = requestId,
            OperationId = operationId,
            Action = action,
            Accepted = false,
            Status = RobotActionExecutionStatus.Rejected,
            Code = code,
            Message = message,
            Data = data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            State = state
        };

        lock (_syncRoot)
        {
            _lastResult = response;
        }

        PublishStatusChanged();
        return response;
    }

    private RobotOperationSnapshot CreateOperationSnapshot(
        string operationId,
        string action,
        string message,
        GameUiSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        return new RobotOperationSnapshot
        {
            OperationId = operationId,
            Action = action,
            Status = RobotActionExecutionStatus.Running,
            StartedAt = now,
            LastUpdatedAt = now,
            Message = message ?? string.Empty,
            GameState = RobotGameStateSnapshot.From(snapshot)
        };
    }

    private string CreateOperationId()
    {
        var sequence = Interlocked.Increment(ref _operationSequence);
        return $"robot-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{sequence:D6}";
    }

    private RobotTaskStatusSnapshot BuildStatusSnapshotLocked()
    {
        return new RobotTaskStatusSnapshot
        {
            IsRunning = _runState != RobotTaskRunState.Stopped,
            RunState = _runState,
            ListeningUrl = _listeningUrl,
            CurrentOperation = _currentOperation?.Clone(),
            LastResult = _lastResult,
            GameState = RobotGameStateSnapshot.From(_lastUiSnapshot),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private void PublishStatusChanged()
    {
        RobotTaskStatusSnapshot snapshot;
        lock (_syncRoot)
        {
            snapshot = BuildStatusSnapshotLocked();
        }

        StatusChanged?.Invoke(this, snapshot);
    }
}
