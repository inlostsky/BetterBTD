using BetterBTD.Helpers;
using System.Threading;
using System.Windows;

namespace BetterBTD.Core.Simulator;

public class MouseEventSimulator
{
    public void Move(int x, int y)
    {
        Move(new Point(x, y));
    }

    public void Move(Point screenPoint)
    {
        var absolutePoint = NativeWindowHelper.ToVirtualDesktopAbsoluteCoordinate(screenPoint);
        Simulation.SendInput.Mouse.MoveMouseToPositionOnVirtualDesktop(absolutePoint.X, absolutePoint.Y);
    }

    public bool MoveClient(nint hWnd, int x, int y)
    {
        return MoveClient(hWnd, new Point(x, y));
    }

    public bool MoveClient(nint hWnd, Point clientPoint)
    {
        if (!NativeWindowHelper.TryClientToScreen(hWnd, clientPoint, out var screenPoint))
        {
            return false;
        }

        Move(screenPoint);
        return true;
    }

    public void MoveAbsolute(int x, int y)
    {
        Simulation.SendInput.Mouse.MoveMouseToPositionOnVirtualDesktop(x, y);
    }

    public void LeftButtonDown()
    {
        Simulation.SendInput.Mouse.LeftButtonDown();
    }

    public void LeftButtonUp()
    {
        Simulation.SendInput.Mouse.LeftButtonUp();
    }

    public bool Click(int x, int y)
    {
        if (x == 0 && y == 0)
        {
            return false;
        }

        Move(new Point(x, y));
        LeftButtonDown();
        Thread.Sleep(20);
        LeftButtonUp();
        return true;
    }

    public bool Click(Point point)
    {
        return Click((int)point.X, (int)point.Y);
    }

    public bool DoubleClick(Point point)
    {
        Click(point);
        Thread.Sleep(200);
        return Click(point);
    }

    public bool ClickClient(nint hWnd, int x, int y)
    {
        return ClickClient(hWnd, new Point(x, y));
    }

    public bool ClickClient(nint hWnd, Point clientPoint)
    {
        if (!MoveClient(hWnd, clientPoint))
        {
            return false;
        }

        LeftButtonDown();
        Thread.Sleep(20);
        LeftButtonUp();
        return true;
    }
}
