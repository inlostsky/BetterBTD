using BetterBTD.Models.AutoTasks;
using BetterBTD.Models.MyScripts;

namespace BetterBTD.Core.AutoTasks.Strategies;

public sealed class CollectionAutoTaskStrategy : StageNavigationAutoTaskStrategyBase
{
    public override AutoTaskKind Kind => AutoTaskKind.Collection;

    protected override AutoTaskScriptQuery BuildScriptQuery(AutoTaskRuntimeState state)
    {
        var slotId = string.Empty;
        if (ManagedScriptCollectionModeCatalog.TryNormalizeKey(state.Request.VariantKey, out var variantKey))
        {
            slotId = ManagedScriptSlotIdFactory.CreateCollectionSlotId(variantKey, state.Request.StageTarget.Map);
        }

        return new AutoTaskScriptQuery
        {
            Kind = Kind,
            StageTarget = state.Request.StageTarget,
            VariantKey = state.Request.VariantKey,
            SlotId = slotId,
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
