using BetterBTD.Helpers;

namespace BetterBTD.Models;

public readonly record struct DeviceDisplayInfo(
    int PhysicalWidth,
    int PhysicalHeight,
    int LogicalWidth,
    int LogicalHeight,
    NativeWindowBounds PhysicalWorkAreaBounds,
    NativeWindowBounds LogicalWorkAreaBounds,
    double ScaleX,
    double ScaleY)
{
    public double DisplayScale => (ScaleX + ScaleY) / 2d;
}
