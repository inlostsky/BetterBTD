using BetterBTD.Core.AutoTasks.Runtime;
using BetterBTD.Core.RobotControl;
using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.RobotControl;

namespace BetterBTD.Tests.RobotControl;

public sealed class RobotTaskCoordinatorTests
{
    [Fact]
    public void DefaultRegistry_ContainsExpectedRobotActions()
    {
        var actions = RobotActionRegistry.Instance.GetMetadata();

        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.CreateMultiplayerRoom);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.JoinMultiplayerRoom);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.SelectHero);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.StartChallenge);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.SendMoney);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.DisableAutoStart);
        Assert.Contains(actions, action => action.Key == RobotGameActionCatalog.StartNextRound);
    }

    [Fact]
    public async Task ExecuteActionAsync_Rejects_WhenRuntimeIsStopped()
    {
        var coordinator = CreateCoordinator(new TestRobotAction());

        var response = await coordinator.ExecuteActionAsync(
            "test",
            new RobotActionRequest { Action = "test" });

        Assert.False(response.Accepted);
        Assert.Equal(RobotActionErrorCodes.TaskNotRunning, response.Code);
    }

    [Fact]
    public async Task ExecuteActionAsync_ValidatesRegisteredActionParameters()
    {
        var coordinator = CreateCoordinator(RobotGameActionCatalog.CreateDefaultActions());
        coordinator.Start(RobotTaskConstants.DefaultListenUrl);

        try
        {
            var response = await coordinator.ExecuteActionAsync(
                RobotGameActionCatalog.CreateMultiplayerRoom,
                new RobotActionRequest
                {
                    Action = RobotGameActionCatalog.CreateMultiplayerRoom,
                    Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                });

            Assert.False(response.Accepted);
            Assert.Equal(RobotActionErrorCodes.InvalidParameter, response.Code);
            Assert.Contains("map", response.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            coordinator.Stop();
        }
    }

    [Fact]
    public async Task ExecuteActionAsync_RejectsImmediately_WhenAnotherActionIsRunning()
    {
        var action = new BlockingRobotAction();
        var coordinator = CreateCoordinator(action);
        coordinator.Start(RobotTaskConstants.DefaultListenUrl);

        try
        {
            var firstExecutionTask = coordinator.ExecuteActionAsync(
                action.Key,
                new RobotActionRequest { Action = action.Key });

            await action.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var secondResponse = await coordinator.ExecuteActionAsync(
                action.Key,
                new RobotActionRequest { Action = action.Key });

            Assert.False(secondResponse.Accepted);
            Assert.Equal(RobotActionErrorCodes.Busy, secondResponse.Code);

            action.Complete();
            var firstResponse = await firstExecutionTask;
            Assert.True(firstResponse.Accepted);
            Assert.Equal(RobotActionExecutionStatus.Completed, firstResponse.Status);
        }
        finally
        {
            coordinator.Stop();
        }
    }

    [Fact]
    public async Task ExecuteActionAsync_Rejects_WhenUiAutomationRuleMatchesCurrentState()
    {
        var action = new TestRobotAction();
        var rule = new MatchingUiAutomationRule();
        var coordinator = CreateCoordinator([action], [rule]);
        coordinator.Start(RobotTaskConstants.DefaultListenUrl);

        try
        {
            var response = await coordinator.ExecuteActionAsync(
                action.Key,
                new RobotActionRequest { Action = action.Key });

            Assert.False(response.Accepted);
            Assert.Equal(RobotActionErrorCodes.UiAutomationRequired, response.Code);
            Assert.Contains(rule.Key, response.Message);
        }
        finally
        {
            coordinator.Stop();
        }
    }

    [Fact]
    public async Task TryRunUiAutomationAsync_ExecutesMatchingRule()
    {
        var rule = new MatchingUiAutomationRule();
        var coordinator = CreateCoordinator([new TestRobotAction()], [rule]);
        coordinator.Start(RobotTaskConstants.DefaultListenUrl);

        try
        {
            var handled = await coordinator.TryRunUiAutomationAsync();

            Assert.True(handled);
            Assert.Equal(1, rule.ExecutionCount);
            Assert.Equal(rule.Key, coordinator.GetStatusSnapshot().LastResult?.Action);
        }
        finally
        {
            coordinator.Stop();
        }
    }

    private static RobotTaskCoordinator CreateCoordinator(IRobotGameAction action)
    {
        return CreateCoordinator([action], []);
    }

    private static RobotTaskCoordinator CreateCoordinator(IEnumerable<IRobotGameAction> actions)
    {
        return CreateCoordinator(actions, []);
    }

    private static RobotTaskCoordinator CreateCoordinator(
        IEnumerable<IRobotGameAction> actions,
        IReadOnlyList<IRobotUiAutomationRule> rules)
    {
        return new RobotTaskCoordinator(
            new RobotActionRegistry(actions),
            rules,
            new StaticGameUiStateService(new GameUiSnapshot
            {
                State = GameUiStateId.InLevel,
                Confidence = 1d,
                Summary = "Test state."
            }),
            GameCaptureService.Instance,
            ScriptInputSimulationService.Instance,
            CoordinateTransformService.Instance);
    }

    private sealed class StaticGameUiStateService : IGameUiStateService
    {
        private readonly GameUiSnapshot _snapshot;

        public StaticGameUiStateService(GameUiSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<GameUiSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_snapshot);
        }

        public void ResetStabilizationState()
        {
        }
    }

    private sealed class TestRobotAction : IRobotGameAction
    {
        public string Key => "test";

        public RobotActionMetadata Metadata { get; } = new()
        {
            Key = "test",
            DisplayName = "Test action"
        };

        public Task<RobotActionPrecheckResult> CheckAsync(
            RobotActionContext context,
            RobotActionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RobotActionPrecheckResult.Success("Ready."));
        }

        public Task<RobotActionResult> ExecuteAsync(
            RobotActionContext context,
            RobotActionRequest request,
            IProgress<RobotActionProgress> progress,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RobotActionResult.Completed("Done."));
        }
    }

    private sealed class BlockingRobotAction : IRobotGameAction
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Key => "blocking";

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RobotActionMetadata Metadata { get; } = new()
        {
            Key = "blocking",
            DisplayName = "Blocking action",
            TimeoutMs = 10000
        };

        public Task<RobotActionPrecheckResult> CheckAsync(
            RobotActionContext context,
            RobotActionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RobotActionPrecheckResult.Success("Ready."));
        }

        public async Task<RobotActionResult> ExecuteAsync(
            RobotActionContext context,
            RobotActionRequest request,
            IProgress<RobotActionProgress> progress,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await _completion.Task.WaitAsync(cancellationToken);
            return RobotActionResult.Completed("Done.");
        }

        public void Complete()
        {
            _completion.TrySetResult();
        }
    }

    private sealed class MatchingUiAutomationRule : IRobotUiAutomationRule
    {
        public int Priority => 100;

        public string Key => "test_ui_rule";

        public int ExecutionCount { get; private set; }

        public bool CanHandle(GameUiSnapshot snapshot)
        {
            return snapshot.State == GameUiStateId.InLevel;
        }

        public Task<RobotActionResult> ExecuteAsync(
            RobotActionContext context,
            IProgress<RobotActionProgress> progress,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(RobotActionResult.Completed("UI rule handled."));
        }
    }
}
