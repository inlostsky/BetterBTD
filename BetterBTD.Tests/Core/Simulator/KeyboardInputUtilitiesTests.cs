using BetterBTD.Core.Config;
using BetterBTD.Core.Simulator;
using Fischless.WindowsInput;
using Vanara.PInvoke;

namespace BetterBTD.Tests.Core.Simulator;

public sealed class KeyboardInputUtilitiesTests
{
    [Fact]
    public void KeyPress_PageUp_UsesExtendedKeyOverload()
    {
        var keyboard = new RecordingKeyboardSimulator();

        KeyboardInputUtilities.KeyPress(keyboard, KeyId.PageUp);

        var call = Assert.Single(keyboard.Calls);
        Assert.Equal(nameof(IKeyboardSimulator.KeyPress), call.Method);
        Assert.Equal(User32.VK.VK_PRIOR, call.Key);
        Assert.True(call.IsExtendedKey);
    }

    [Fact]
    public void KeyDown_PageDown_UsesExtendedKeyOverload()
    {
        var keyboard = new RecordingKeyboardSimulator();

        KeyboardInputUtilities.KeyDown(keyboard, KeyId.PageDown);

        var call = Assert.Single(keyboard.Calls);
        Assert.Equal(nameof(IKeyboardSimulator.KeyDown), call.Method);
        Assert.Equal(User32.VK.VK_NEXT, call.Key);
        Assert.True(call.IsExtendedKey);
    }

    [Fact]
    public void KeyUp_LetterKey_UsesDefaultOverload()
    {
        var keyboard = new RecordingKeyboardSimulator();

        KeyboardInputUtilities.KeyUp(keyboard, KeyId.U);

        var call = Assert.Single(keyboard.Calls);
        Assert.Equal(nameof(IKeyboardSimulator.KeyUp), call.Method);
        Assert.Equal(User32.VK.VK_U, call.Key);
        Assert.Null(call.IsExtendedKey);
    }

    private sealed class RecordingKeyboardSimulator : IKeyboardSimulator
    {
        public List<KeyboardCall> Calls { get; } = [];

        public IMouseSimulator Mouse { get; } = new RecordingMouseSimulator();

        public IKeyboardSimulator KeyDown(User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyDown), null, keyCode));
            return this;
        }

        public IKeyboardSimulator KeyDown(bool? isExtendedKey, User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyDown), isExtendedKey, keyCode));
            return this;
        }

        public IKeyboardSimulator KeyPress(User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyPress), null, keyCode));
            return this;
        }

        public IKeyboardSimulator KeyPress(bool? isExtendedKey, User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyPress), isExtendedKey, keyCode));
            return this;
        }

        public IKeyboardSimulator KeyPress(params User32.VK[] keyCodes) => this;

        public IKeyboardSimulator KeyPress(bool? isExtendedKey, params User32.VK[] keyCodes) => this;

        public IKeyboardSimulator KeyUp(User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyUp), null, keyCode));
            return this;
        }

        public IKeyboardSimulator KeyUp(bool? isExtendedKey, User32.VK keyCode)
        {
            Calls.Add(new KeyboardCall(nameof(KeyUp), isExtendedKey, keyCode));
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<User32.VK> modifierKeyCodes, IEnumerable<User32.VK> keyCodes) => this;

        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<User32.VK> modifierKeyCodes, User32.VK keyCode) => this;

        public IKeyboardSimulator ModifiedKeyStroke(User32.VK modifierKey, IEnumerable<User32.VK> keyCodes) => this;

        public IKeyboardSimulator ModifiedKeyStroke(User32.VK modifierKeyCode, User32.VK keyCode) => this;

        public IKeyboardSimulator TextEntry(string text) => this;

        public IKeyboardSimulator TextEntry(char character) => this;

        public IKeyboardSimulator Sleep(int millsecondsTimeout) => this;

        public IKeyboardSimulator Sleep(TimeSpan timeout) => this;
    }

    private sealed class RecordingMouseSimulator : IMouseSimulator
    {
        public IKeyboardSimulator Keyboard => throw new NotSupportedException();

        public IMouseSimulator MoveMouseBy(int pixelDeltaX, int pixelDeltaY) => this;

        public IMouseSimulator MoveMouseTo(double absoluteX, double absoluteY) => this;

        public IMouseSimulator MoveMouseToPositionOnVirtualDesktop(double absoluteX, double absoluteY) => this;

        public IMouseSimulator LeftButtonDown() => this;

        public IMouseSimulator LeftButtonUp() => this;

        public IMouseSimulator LeftButtonClick() => this;

        public IMouseSimulator LeftButtonDoubleClick() => this;

        public IMouseSimulator MiddleButtonDown() => this;

        public IMouseSimulator MiddleButtonUp() => this;

        public IMouseSimulator MiddleButtonClick() => this;

        public IMouseSimulator MiddleButtonDoubleClick() => this;

        public IMouseSimulator RightButtonDown() => this;

        public IMouseSimulator RightButtonUp() => this;

        public IMouseSimulator RightButtonClick() => this;

        public IMouseSimulator RightButtonDoubleClick() => this;

        public IMouseSimulator XButtonDown(int buttonId) => this;

        public IMouseSimulator XButtonUp(int buttonId) => this;

        public IMouseSimulator XButtonClick(int buttonId) => this;

        public IMouseSimulator XButtonDoubleClick(int buttonId) => this;

        public IMouseSimulator VerticalScroll(int scrollAmountInClicks) => this;

        public IMouseSimulator HorizontalScroll(int scrollAmountInClicks) => this;

        public IMouseSimulator Sleep(int millsecondsTimeout) => this;

        public IMouseSimulator Sleep(TimeSpan timeout) => this;
    }

    private sealed record KeyboardCall(string Method, bool? IsExtendedKey, User32.VK Key);
}
