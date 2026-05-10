using BetterBTD.Core.Config;
using Fischless.WindowsInput;

namespace BetterBTD.Core.Simulator;

internal static class KeyboardInputUtilities
{
    public static bool IsExtendedKey(KeyId key)
    {
        return key is not KeyId.None and not KeyId.Unknown && InputBuilder.IsExtendedKey(key.ToVK());
    }

    public static void KeyDown(IKeyboardSimulator keyboard, KeyId key)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        var vk = key.ToVK();
        if (InputBuilder.IsExtendedKey(vk))
        {
            keyboard.KeyDown(true, vk);
        }
        else
        {
            keyboard.KeyDown(vk);
        }
    }

    public static void KeyUp(IKeyboardSimulator keyboard, KeyId key)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        var vk = key.ToVK();
        if (InputBuilder.IsExtendedKey(vk))
        {
            keyboard.KeyUp(true, vk);
        }
        else
        {
            keyboard.KeyUp(vk);
        }
    }

    public static void KeyPress(IKeyboardSimulator keyboard, KeyId key)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        var vk = key.ToVK();
        if (InputBuilder.IsExtendedKey(vk))
        {
            keyboard.KeyPress(true, vk);
        }
        else
        {
            keyboard.KeyPress(vk);
        }
    }
}
