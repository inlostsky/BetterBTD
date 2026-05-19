using BetterBTD.Models.AutoTasks;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class CollectionAutoTaskStrategy : StageNavigationAutoTaskStrategyBase
{
    public override AutoTaskKind Kind => AutoTaskKind.Collection;

    protected override AutoTaskScriptQuery BuildScriptQuery(AutoTaskRuntimeState state)
    {
        return new AutoTaskScriptQuery
        {
            Kind = Kind,
            StageTarget = state.Request.StageTarget,
            RequiredTags = ["collection"],
            Description = "Resolve a collection-farming script for the selected stage."
        };
    }

    protected override AutoTaskDecision DecideAfterScriptExecution(AutoTaskRuntimeState state, GameUiSnapshot snapshot)
    {
        state.ClearPendingScriptOutcome();
        return AutoTaskDecision.Fail("Collection task post-stage flow is not implemented yet.");
    }
}
