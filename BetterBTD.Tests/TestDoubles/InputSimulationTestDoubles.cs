using System.Windows;
using BetterBTD.Core.Simulator;
using BetterBTD.Models;
using BetterBTD.Services;

namespace BetterBTD.Tests.TestDoubles;

internal sealed class RecordingInputSimulationCommandDispatcher : IInputSimulationCommandDispatcher
{
    public List<InputSimulationCommand> Commands { get; } = [];

    public void Dispatch(IEnumerable<InputSimulationCommand> commands)
    {
        Commands.AddRange(commands);
    }
}

internal sealed class FakeScriptInputSimulationEnvironment : IScriptInputSimulationEnvironment
{
    private readonly Func<Point, GameWindowInfo, Point> _convertScriptToScreenCoordinate;
    private readonly Func<Point, Point> _toVirtualDesktopAbsoluteCoordinate;
    private readonly GameWindowInfo _windowInfo;
    private readonly bool _hasTargetWindow;

    public FakeScriptInputSimulationEnvironment(
        GameWindowInfo windowInfo,
        Func<Point, GameWindowInfo, Point>? convertScriptToScreenCoordinate = null,
        Func<Point, Point>? toVirtualDesktopAbsoluteCoordinate = null,
        bool hasTargetWindow = true)
    {
        _windowInfo = windowInfo;
        _hasTargetWindow = hasTargetWindow;
        _convertScriptToScreenCoordinate = convertScriptToScreenCoordinate ?? ((point, _) => point);
        _toVirtualDesktopAbsoluteCoordinate = toVirtualDesktopAbsoluteCoordinate ?? (point => point);
    }

    public string TargetWindowTitle => _windowInfo.Title;

    public bool TryGetTargetWindowInfo(out GameWindowInfo windowInfo)
    {
        windowInfo = _windowInfo;
        return _hasTargetWindow;
    }

    public Point ConvertScriptToScreenCoordinate(Point scriptCoordinate, GameWindowInfo windowInfo)
    {
        return _convertScriptToScreenCoordinate(scriptCoordinate, windowInfo);
    }

    public Point ToVirtualDesktopAbsoluteCoordinate(Point screenCoordinate)
    {
        return _toVirtualDesktopAbsoluteCoordinate(screenCoordinate);
    }
}
