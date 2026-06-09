using BetterBTD.Core.Config;
using Fischless.WindowsInput;
using InputMouseButton = Fischless.WindowsInput.MouseButton;

namespace BetterBTD.Core.Simulator;

internal enum InputSimulationCommandType
{
    MoveMouseToVirtualDesktop,
    MoveMouseBy,
    MouseButtonDown,
    MouseButtonUp,
    KeyPress,
    KeyDown,
    KeyUp,
    Delay
}

internal sealed record InputSimulationCommand
{
    public required InputSimulationCommandType Type { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public int DeltaX { get; init; }

    public int DeltaY { get; init; }

    public InputMouseButton MouseButton { get; init; } = InputMouseButton.LeftButton;

    public KeyId Key { get; init; } = KeyId.None;

    public int Milliseconds { get; init; }
}
