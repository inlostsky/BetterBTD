using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class BlackBorderAutoTaskStrategy : StageNavigationAutoTaskStrategyBase
{
    public override AutoTaskKind Kind => AutoTaskKind.BlackBorder;

    protected override AutoTaskScriptQuery BuildScriptQuery(AutoTaskRuntimeState state)
    {
        return new AutoTaskScriptQuery
        {
            Kind = Kind,
            StageTarget = state.Request.StageTarget,
            RequiredTags = ["black-border"],
            Description = "Resolve a black-border script for the selected stage."
        };
    }

    protected override AutoTaskDecision DecideAfterScriptExecution(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        state.ClearPendingScriptOutcome();
        return AutoTaskDecision.Fail("Black border post-stage progression is not implemented yet.");
    }
}
