using BetterBTD.Core.Config;
using Fischless.WindowsInput;
using Vanara.PInvoke;
using InputMouseButton = Fischless.WindowsInput.MouseButton;

namespace BetterBTD.Core.Simulator;

internal interface IInputSimulationCommandDispatcher
{
    void Dispatch(IEnumerable<InputSimulationCommand> commands);
}

internal sealed class InputSimulationCommandDispatcher : IInputSimulationCommandDispatcher
{
    public void Dispatch(IEnumerable<InputSimulationCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        foreach (var command in commands)
        {
            DispatchCommand(command);
        }
    }

    private static void DispatchCommand(InputSimulationCommand command)
    {
        switch (command.Type)
        {
            case InputSimulationCommandType.MoveMouseToVirtualDesktop:
                Simulation.SendInput.Mouse.MoveMouseToPositionOnVirtualDesktop(command.X, command.Y);
                break;
            case InputSimulationCommandType.MouseButtonDown:
                MouseButtonDown(command.MouseButton);
                break;
            case InputSimulationCommandType.MouseButtonUp:
                MouseButtonUp(command.MouseButton);
                break;
            case InputSimulationCommandType.KeyPress:
                KeyPress(command.Key);
                break;
            case InputSimulationCommandType.KeyDown:
                KeyDown(command.Key);
                break;
            case InputSimulationCommandType.KeyUp:
                KeyUp(command.Key);
                break;
            case InputSimulationCommandType.Delay:
                Thread.Sleep(Math.Max(0, command.Milliseconds));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void MouseButtonDown(InputMouseButton button)
    {
        switch (button)
        {
            case InputMouseButton.LeftButton:
                Simulation.SendInput.Mouse.LeftButtonDown();
                break;
            case InputMouseButton.MiddleButton:
                Simulation.SendInput.Mouse.MiddleButtonDown();
                break;
            case InputMouseButton.RightButton:
                Simulation.SendInput.Mouse.RightButtonDown();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.");
        }
    }

    private static void MouseButtonUp(InputMouseButton button)
    {
        switch (button)
        {
            case InputMouseButton.LeftButton:
                Simulation.SendInput.Mouse.LeftButtonUp();
                break;
            case InputMouseButton.MiddleButton:
                Simulation.SendInput.Mouse.MiddleButtonUp();
                break;
            case InputMouseButton.RightButton:
                Simulation.SendInput.Mouse.RightButtonUp();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.");
        }
    }

    private static void KeyPress(KeyId key)
    {
        switch (key)
        {
            case KeyId.None:
            case KeyId.Unknown:
                break;
            case KeyId.MouseLeftButton:
                Simulation.SendInput.Mouse.LeftButtonClick();
                break;
            case KeyId.MouseRightButton:
                Simulation.SendInput.Mouse.RightButtonClick();
                break;
            case KeyId.MouseMiddleButton:
                Simulation.SendInput.Mouse.MiddleButtonClick();
                break;
            case KeyId.MouseSideButton1:
                Simulation.SendInput.Mouse.XButtonClick(0x0001);
                break;
            case KeyId.MouseSideButton2:
                Simulation.SendInput.Mouse.XButtonClick(0x0002);
                break;
            default:
                var vk = key.ToVK();
                if (InputBuilder.IsExtendedKey(vk))
                {
                    Simulation.SendInput.Keyboard.KeyPress(false, vk);
                }
                else
                {
                    Simulation.SendInput.Keyboard.KeyPress(vk);
                }

                break;
        }
    }

    private static void KeyDown(KeyId key)
    {
        switch (key)
        {
            case KeyId.None:
            case KeyId.Unknown:
                break;
            case KeyId.MouseLeftButton:
                Simulation.SendInput.Mouse.LeftButtonDown();
                break;
            case KeyId.MouseRightButton:
                Simulation.SendInput.Mouse.RightButtonDown();
                break;
            case KeyId.MouseMiddleButton:
                Simulation.SendInput.Mouse.MiddleButtonDown();
                break;
            case KeyId.MouseSideButton1:
                Simulation.SendInput.Mouse.XButtonDown(0x0001);
                break;
            case KeyId.MouseSideButton2:
                Simulation.SendInput.Mouse.XButtonDown(0x0002);
                break;
            default:
                var vk = key.ToVK();
                if (InputBuilder.IsExtendedKey(vk))
                {
                    Simulation.SendInput.Keyboard.KeyDown(false, vk);
                }
                else
                {
                    Simulation.SendInput.Keyboard.KeyDown(vk);
                }

                break;
        }
    }

    private static void KeyUp(KeyId key)
    {
        switch (key)
        {
            case KeyId.None:
            case KeyId.Unknown:
                break;
            case KeyId.MouseLeftButton:
                Simulation.SendInput.Mouse.LeftButtonUp();
                break;
            case KeyId.MouseRightButton:
                Simulation.SendInput.Mouse.RightButtonUp();
                break;
            case KeyId.MouseMiddleButton:
                Simulation.SendInput.Mouse.MiddleButtonUp();
                break;
            case KeyId.MouseSideButton1:
                Simulation.SendInput.Mouse.XButtonUp(0x0001);
                break;
            case KeyId.MouseSideButton2:
                Simulation.SendInput.Mouse.XButtonUp(0x0002);
                break;
            default:
                var vk = key.ToVK();
                if (InputBuilder.IsExtendedKey(vk))
                {
                    Simulation.SendInput.Keyboard.KeyUp(false, vk);
                }
                else
                {
                    Simulation.SendInput.Keyboard.KeyUp(vk);
                }

                break;
        }
    }
}
