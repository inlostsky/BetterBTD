using BetterBTD.Core.Config;
using BetterBTD.Core.ScriptExecution;
using BetterBTD.Tests.TestDoubles;
using System.Windows.Input;

namespace BetterBTD.Tests.ScriptExecution;

public sealed class ScriptExecutionKeyBindingResolverTests
{
    [Fact]
    public void ResolvePlacementHotkey_HeroSelection_UsesGeneralHeroBinding()
    {
        var expected = new HotkeyBinding
        {
            Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
            Key = KeyId.H
        };

        using var _ = new KeyBindingOverrideScope(expected);

        var hotkey = ScriptExecutionKeyBindingResolver.ResolvePlacementHotkey("Hero:Geraldo");

        Assert.Equal(expected.Modifiers, hotkey.Modifiers);
        Assert.Equal(expected.Key, hotkey.Key);
    }
}
