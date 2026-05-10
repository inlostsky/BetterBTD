using BetterBTD.Core.ScriptExecution.Handlers;

namespace BetterBTD.Tests.ScriptExecution.Handlers;

public sealed class PlaceMonkeyInstructionHandlerTimingTests
{
    [Fact]
    public void PlacementModeActivationTimeout_IsTenMinutes()
    {
        Assert.Equal(600_000, PlaceMonkeyInstructionHandler.PlacementModeActivationTimeoutMilliseconds);
    }
}
