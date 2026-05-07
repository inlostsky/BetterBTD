using BetterBTD.Core.Simulator;
using Fischless.WindowsInput;
using CvPoint = OpenCvSharp.Point;
using System.Windows;

namespace BetterBTD.Helpers.Extensions;

public static class ClickExtension
{
    public static void Click(this CvPoint point)
    {
        Click(point.X, point.Y);
    }

    public static IMouseSimulator Click(double x, double y)
    {
        var absoluteCoordinate = NativeWindowHelper.ToVirtualDesktopAbsoluteCoordinate(new Point(x, y));
        return Simulation.SendInput.Mouse
            .MoveMouseToPositionOnVirtualDesktop(absoluteCoordinate.X, absoluteCoordinate.Y)
            .LeftButtonDown()
            .Sleep(50)
            .LeftButtonUp();
    }

    public static IMouseSimulator Move(double x, double y)
    {
        var absoluteCoordinate = NativeWindowHelper.ToVirtualDesktopAbsoluteCoordinate(new Point(x, y));
        return Simulation.SendInput.Mouse.MoveMouseToPositionOnVirtualDesktop(absoluteCoordinate.X, absoluteCoordinate.Y);
    }

    public static IMouseSimulator Move(CvPoint point)
    {
        return Move(point.X, point.Y);
    }
}
