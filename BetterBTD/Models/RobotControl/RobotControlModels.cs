using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Models.RobotControl;

public enum RobotTaskRunState
{
    Stopped,
    Starting,
    Listening,
    BusyWithUiAutomation,
    BusyWithRobotAction,
    Stopping
}

public enum RobotActionExecutionStatus
{
    Rejected,
    Running,
    Completed,
    Failed,
    Cancelled,
    TimedOut
}

public enum RobotActionParameterType
{
    String,
    Integer,
    Boolean,
    Enum
}

public static class RobotActionErrorCodes
{
    public const string Ok = "Ok";
    public const string Busy = "Busy";
    public const string InvalidAction = "InvalidAction";
    public const string InvalidParameter = "InvalidParameter";
    public const string InvalidGameState = "InvalidGameState";
    public const string UiAutomationRequired = "UiAutomationRequired";
    public const string NotImplemented = "NotImplemented";
    public const string TaskNotRunning = "TaskNotRunning";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string TimedOut = "TimedOut";
}

public sealed class RobotActionParameterDescriptor
{
    public required string Key { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public RobotActionParameterType Type { get; init; } = RobotActionParameterType.String;

    public bool IsRequired { get; init; } = true;

    public IReadOnlyList<string> AllowedValues { get; init; } = Array.Empty<string>();
}

public sealed class RobotActionMetadata
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<RobotActionParameterDescriptor> Parameters { get; init; } =
        Array.Empty<RobotActionParameterDescriptor>();

    public IReadOnlyList<GameUiStateId> AllowedUiStates { get; init; } = Array.Empty<GameUiStateId>();

    public int TimeoutMs { get; init; } = 15000;
}

public sealed class RobotActionRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RobotActionPrecheckResult
{
    public bool CanExecute { get; init; }

    public string Code { get; init; } = RobotActionErrorCodes.Ok;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Data { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public static RobotActionPrecheckResult Success(string message = "")
    {
        return new RobotActionPrecheckResult
        {
            CanExecute = true,
            Code = RobotActionErrorCodes.Ok,
            Message = message
        };
    }

    public static RobotActionPrecheckResult Reject(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        return new RobotActionPrecheckResult
        {
            CanExecute = false,
            Code = string.IsNullOrWhiteSpace(code) ? RobotActionErrorCodes.Failed : code,
            Message = message ?? string.Empty,
            Data = data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class RobotActionResult
{
    public required RobotActionExecutionStatus Status { get; init; }

    public string Code { get; init; } = RobotActionErrorCodes.Ok;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Data { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public static RobotActionResult Completed(
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        return new RobotActionResult
        {
            Status = RobotActionExecutionStatus.Completed,
            Code = RobotActionErrorCodes.Ok,
            Message = message ?? string.Empty,
            Data = data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }

    public static RobotActionResult Failed(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        return new RobotActionResult
        {
            Status = RobotActionExecutionStatus.Failed,
            Code = string.IsNullOrWhiteSpace(code) ? RobotActionErrorCodes.Failed : code,
            Message = message ?? string.Empty,
            Data = data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class RobotActionProgress
{
    public string OperationId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public RobotActionExecutionStatus Status { get; init; } = RobotActionExecutionStatus.Running;

    public double? ProgressPercent { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RobotGameStateSnapshot
{
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public GameUiStateId UiState { get; init; } = GameUiStateId.Unknown;

    public double Confidence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public static RobotGameStateSnapshot From(GameUiSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new RobotGameStateSnapshot
            {
                UiState = GameUiStateId.Unknown,
                Confidence = 0d,
                Summary = "No game UI state has been captured yet."
            };
        }

        return new RobotGameStateSnapshot
        {
            CapturedAt = snapshot.CapturedAt,
            UiState = snapshot.State,
            Confidence = snapshot.Confidence,
            Summary = snapshot.Summary
        };
    }
}

public sealed class RobotOperationSnapshot
{
    public string OperationId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public RobotActionExecutionStatus Status { get; set; } = RobotActionExecutionStatus.Running;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Code { get; set; } = RobotActionErrorCodes.Ok;

    public string Message { get; set; } = string.Empty;

    public double? ProgressPercent { get; set; }

    public RobotGameStateSnapshot GameState { get; set; } = new();

    public RobotOperationSnapshot Clone()
    {
        return new RobotOperationSnapshot
        {
            OperationId = OperationId,
            Action = Action,
            Status = Status,
            StartedAt = StartedAt,
            LastUpdatedAt = LastUpdatedAt,
            Code = Code,
            Message = Message,
            ProgressPercent = ProgressPercent,
            GameState = GameState
        };
    }
}

public sealed class RobotTaskStatusSnapshot
{
    public bool IsRunning { get; init; }

    public RobotTaskRunState RunState { get; init; } = RobotTaskRunState.Stopped;

    public string ListeningUrl { get; init; } = string.Empty;

    public RobotOperationSnapshot? CurrentOperation { get; init; }

    public RobotActionResponse? LastResult { get; init; }

    public RobotGameStateSnapshot GameState { get; init; } = new();

    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RobotActionResponse
{
    public string RequestId { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public bool Accepted { get; init; }

    public RobotActionExecutionStatus Status { get; init; } = RobotActionExecutionStatus.Rejected;

    public string Code { get; init; } = RobotActionErrorCodes.Ok;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Data { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public RobotGameStateSnapshot State { get; init; } = new();
}
