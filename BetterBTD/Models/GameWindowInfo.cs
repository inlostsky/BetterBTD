using System.Windows;
using BetterBTD.Helpers;

namespace BetterBTD.Models;

public readonly record struct GameWindowInfo(
    nint Handle,
    string Title,
    NativeWindowBounds WindowBounds,
    NativeWindowBounds ClientBounds,
    double ScaleFactor)
{
    public int ClientWidth => ClientBounds.Width;

    public int ClientHeight => ClientBounds.Height;

    public Point ScreenToClient(Point screenPoint)
    {
        return new Point(
            screenPoint.X - ClientBounds.Left,
            screenPoint.Y - ClientBounds.Top);
    }

    public Point ClientToScreen(Point clientPoint)
    {
        return new Point(
            ClientBounds.Left + clientPoint.X,
            ClientBounds.Top + clientPoint.Y);
    }
}
