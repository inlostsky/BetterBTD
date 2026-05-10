using BetterBTD.Core.Config;
using BetterBTD.Services;

namespace BetterBTD.Tests.Services;

public sealed class HardwareInputSimulationServiceTests
{
    [Fact]
    public void TryGetScanCode_PageUp_ReturnsSharedScanCodeWithE0()
    {
        var success = HardwareInputSimulationService.TryGetScanCode(KeyId.PageUp, out var scanCode, out var isE0, out var isE1);

        Assert.True(success);
        Assert.Equal(0x49, scanCode);
        Assert.True(isE0);
        Assert.False(isE1);
    }

    [Fact]
    public void TryGetScanCode_PageDown_ReturnsSharedScanCodeWithE0()
    {
        var success = HardwareInputSimulationService.TryGetScanCode(KeyId.PageDown, out var scanCode, out var isE0, out var isE1);

        Assert.True(success);
        Assert.Equal(0x51, scanCode);
        Assert.True(isE0);
        Assert.False(isE1);
    }

    [Fact]
    public void TryGetScanCode_LetterKey_DoesNotSetExtendedFlags()
    {
        var success = HardwareInputSimulationService.TryGetScanCode(KeyId.U, out var scanCode, out var isE0, out var isE1);

        Assert.True(success);
        Assert.Equal(0x16, scanCode);
        Assert.False(isE0);
        Assert.False(isE1);
    }
}
