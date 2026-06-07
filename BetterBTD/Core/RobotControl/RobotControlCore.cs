using System.Text.Json;
using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.GameElements;
using BetterBTD.Models.RobotControl;

namespace BetterBTD.Core.RobotControl;

public static class RobotTaskConstants
{
    public const string TaskKey = "robot";
    public const string DefaultListenUrl = "http://127.0.0.1:18766/";
}

public sealed class RobotTaskRuntimeOptions
{
    public string ListenUrl { get; init; } = RobotTaskConstants.DefaultListenUrl;

    public int UiAutomationPollIntervalMs { get; init; } = 300;
}

public sealed class RobotActionContext
{
    public required string OperationId { get; init; }

    public required GameUiSnapshot CurrentSnapshot { get; init; }

    public required IGameUiStateService GameUiState { get; init; }

    public required GameCaptureService GameCapture { get; init; }

    public required ScriptInputSimulationService InputSimulation { get; init; }

    public required CoordinateTransformService CoordinateTransform { get; init; }
}

public interface IRobotGameAction
{
    string Key { get; }

    RobotActionMetadata Metadata { get; }

    Task<RobotActionPrecheckResult> CheckAsync(
        RobotActionContext context,
        RobotActionRequest request,
        CancellationToken cancellationToken = default);

    Task<RobotActionResult> ExecuteAsync(
        RobotActionContext context,
        RobotActionRequest request,
        IProgress<RobotActionProgress> progress,
        CancellationToken cancellationToken = default);
}

public interface IRobotUiAutomationRule
{
    int Priority { get; }

    string Key { get; }

    bool CanHandle(GameUiSnapshot snapshot);

    Task<RobotActionResult> ExecuteAsync(
        RobotActionContext context,
        IProgress<RobotActionProgress> progress,
        CancellationToken cancellationToken = default);
}

public interface IRobotActionRegistry
{
    IReadOnlyList<RobotActionMetadata> GetMetadata();

    bool TryGetAction(string key, out IRobotGameAction action);
}

public sealed class RobotActionRegistry : IRobotActionRegistry
{
    private static readonly Lazy<RobotActionRegistry> InstanceHolder =
        new(() => new RobotActionRegistry(RobotGameActionCatalog.CreateDefaultActions()));

    private readonly IReadOnlyDictionary<string, IRobotGameAction> _actions;

    internal RobotActionRegistry(IEnumerable<IRobotGameAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions = actions
            .GroupBy(static action => action.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public static RobotActionRegistry Instance => InstanceHolder.Value;

    public IReadOnlyList<RobotActionMetadata> GetMetadata()
    {
        return _actions.Values
            .Select(static action => action.Metadata)
            .OrderBy(static metadata => metadata.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryGetAction(string key, out IRobotGameAction action)
    {
        action = null!;
        return !string.IsNullOrWhiteSpace(key) && _actions.TryGetValue(key.Trim(), out action!);
    }
}

public sealed class RobotUiAutomationRuleRegistry
{
    private static readonly Lazy<RobotUiAutomationRuleRegistry> InstanceHolder =
        new(() => new RobotUiAutomationRuleRegistry([]));

    private readonly IReadOnlyList<IRobotUiAutomationRule> _rules;

    internal RobotUiAutomationRuleRegistry(IEnumerable<IRobotUiAutomationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules
            .OrderByDescending(static rule => rule.Priority)
            .ToArray();
    }

    public static RobotUiAutomationRuleRegistry Instance => InstanceHolder.Value;

    public IReadOnlyList<IRobotUiAutomationRule> Rules => _rules;
}

public static class RobotGameActionCatalog
{
    public const string CreateMultiplayerRoom = "create_multiplayer_room";
    public const string JoinMultiplayerRoom = "join_multiplayer_room";
    public const string SelectHero = "select_hero";
    public const string StartChallenge = "start_challenge";
    public const string SendMoney = "send_money";
    public const string DisableAutoStart = "disable_auto_start";
    public const string StartNextRound = "start_next_round";

    public static IReadOnlyList<IRobotGameAction> CreateDefaultActions()
    {
        return
        [
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = CreateMultiplayerRoom,
                DisplayName = "Create multiplayer room",
                Description = "Create a multiplayer room and return the room code.",
                Parameters =
                [
                    EnumParameter("map", "Map", Enum.GetNames<GameMapType>()),
                    EnumParameter("difficulty", "Difficulty", Enum.GetNames<StageDifficulty>()),
                    EnumParameter("mode", "Mode", Enum.GetNames<StageMode>())
                ],
                TimeoutMs = 30000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = JoinMultiplayerRoom,
                DisplayName = "Join multiplayer room",
                Description = "Join an existing multiplayer room by room code.",
                Parameters =
                [
                    StringParameter("roomCode", "Room code")
                ],
                TimeoutMs = 30000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = SelectHero,
                DisplayName = "Select hero",
                Description = "Select the specified hero.",
                Parameters =
                [
                    EnumParameter("hero", "Hero", Enum.GetNames<HeroType>())
                ],
                TimeoutMs = 15000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = StartChallenge,
                DisplayName = "Start challenge",
                Description = "Start the current challenge.",
                TimeoutMs = 10000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = SendMoney,
                DisplayName = "Send money",
                Description = "Send money to the specified player.",
                Parameters =
                [
                    EnumParameter("player", "Player", ["p1", "p2", "p3", "p4"])
                ],
                TimeoutMs = 10000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = DisableAutoStart,
                DisplayName = "Disable auto start",
                Description = "Disable automatic round start.",
                TimeoutMs = 10000
            }),
            new PlaceholderRobotGameAction(new RobotActionMetadata
            {
                Key = StartNextRound,
                DisplayName = "Start next round",
                Description = "Start the next round.",
                TimeoutMs = 10000
            })
        ];
    }

    private static RobotActionParameterDescriptor StringParameter(string key, string displayName)
    {
        return new RobotActionParameterDescriptor
        {
            Key = key,
            DisplayName = displayName,
            Type = RobotActionParameterType.String,
            IsRequired = true
        };
    }

    private static RobotActionParameterDescriptor EnumParameter(
        string key,
        string displayName,
        IReadOnlyList<string> allowedValues)
    {
        return new RobotActionParameterDescriptor
        {
            Key = key,
            DisplayName = displayName,
            Type = RobotActionParameterType.Enum,
            IsRequired = true,
            AllowedValues = allowedValues
        };
    }
}

internal sealed class PlaceholderRobotGameAction : IRobotGameAction
{
    public PlaceholderRobotGameAction(RobotActionMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public string Key => Metadata.Key;

    public RobotActionMetadata Metadata { get; }

    public Task<RobotActionPrecheckResult> CheckAsync(
        RobotActionContext context,
        RobotActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (Metadata.AllowedUiStates.Count > 0 &&
            !Metadata.AllowedUiStates.Contains(context.CurrentSnapshot.State))
        {
            return Task.FromResult(RobotActionPrecheckResult.Reject(
                RobotActionErrorCodes.InvalidGameState,
                $"Current UI state '{context.CurrentSnapshot.State}' cannot execute action '{Key}'.",
                new Dictionary<string, object?>
                {
                    ["currentUiState"] = context.CurrentSnapshot.State.ToString(),
                    ["requiredUiStates"] = Metadata.AllowedUiStates.Select(static state => state.ToString()).ToArray()
                }));
        }

        foreach (var parameter in Metadata.Parameters.Where(static parameter => parameter.IsRequired))
        {
            if (!RobotActionParameterReader.TryGetString(request.Parameters, parameter.Key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return Task.FromResult(RobotActionPrecheckResult.Reject(
                    RobotActionErrorCodes.InvalidParameter,
                    $"Required parameter '{parameter.Key}' is missing or empty."));
            }

            if (parameter.AllowedValues.Count > 0 &&
                !parameter.AllowedValues.Any(allowed =>
                    string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(RobotActionPrecheckResult.Reject(
                    RobotActionErrorCodes.InvalidParameter,
                    $"Parameter '{parameter.Key}' value '{value}' is not supported.",
                    new Dictionary<string, object?>
                    {
                        ["parameter"] = parameter.Key,
                        ["allowedValues"] = parameter.AllowedValues
                    }));
            }
        }

        return Task.FromResult(RobotActionPrecheckResult.Success($"Action '{Key}' passed precheck."));
    }

    public Task<RobotActionResult> ExecuteAsync(
        RobotActionContext context,
        RobotActionRequest request,
        IProgress<RobotActionProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        cancellationToken.ThrowIfCancellationRequested();

        var message = $"Action '{Key}' is registered but its execution logic is not implemented yet.";
        progress.Report(new RobotActionProgress
        {
            OperationId = context.OperationId,
            Action = Key,
            Status = RobotActionExecutionStatus.Failed,
            Message = message,
            ProgressPercent = 100d
        });

        return Task.FromResult(RobotActionResult.Failed(
            RobotActionErrorCodes.NotImplemented,
            message));
    }
}

internal static class RobotActionParameterReader
{
    public static bool TryGetString(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out string value)
    {
        value = string.Empty;

        if (parameters.Count == 0 ||
            !parameters.TryGetValue(key, out var rawValue) ||
            rawValue is null)
        {
            return false;
        }

        switch (rawValue)
        {
            case string stringValue:
                value = stringValue.Trim();
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } jsonElement:
                value = jsonElement.GetString()?.Trim() ?? string.Empty;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonElement:
                value = jsonElement.ToString();
                return true;
            case JsonElement { ValueKind: JsonValueKind.True }:
                value = bool.TrueString;
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                value = bool.FalseString;
                return true;
            default:
                value = rawValue.ToString()?.Trim() ?? string.Empty;
                return value.Length > 0;
        }
    }
}
