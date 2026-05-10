using BetterBTD.Core.ScriptExecution.Handlers;
using WpfPoint = System.Windows.Point;

namespace BetterBTD.Tests.ScriptExecution.Handlers;

public sealed class ScriptInstructionHandlerSupportTests
{
    [Fact]
    public void BuildPlacementSearchCoordinates_ExpandsAroundCenterInEightDirections()
    {
        var coordinates = ScriptInstructionHandlerSupport
            .BuildPlacementSearchCoordinates(new WpfPoint(100, 200))
            .Take(17)
            .ToArray();

        Assert.Equal(
        [
            new WpfPoint(100, 200),
            new WpfPoint(101, 200),
            new WpfPoint(101, 201),
            new WpfPoint(100, 201),
            new WpfPoint(99, 201),
            new WpfPoint(99, 200),
            new WpfPoint(99, 199),
            new WpfPoint(100, 199),
            new WpfPoint(101, 199),
            new WpfPoint(102, 200),
            new WpfPoint(102, 202),
            new WpfPoint(100, 202),
            new WpfPoint(98, 202),
            new WpfPoint(98, 200),
            new WpfPoint(98, 198),
            new WpfPoint(100, 198),
            new WpfPoint(102, 198)
        ], coordinates);
    }

    [Fact]
    public void BuildPlacementSearchCoordinates_StopsAfterTwentyRings()
    {
        var coordinates = ScriptInstructionHandlerSupport
            .BuildPlacementSearchCoordinates(new WpfPoint(0, 0))
            .ToArray();

        Assert.Equal(161, coordinates.Length);
        Assert.Contains(new WpfPoint(20, 20), coordinates);
        Assert.Contains(new WpfPoint(-20, -20), coordinates);
        Assert.DoesNotContain(new WpfPoint(21, 0), coordinates);
        Assert.DoesNotContain(new WpfPoint(0, -21), coordinates);
    }
}
