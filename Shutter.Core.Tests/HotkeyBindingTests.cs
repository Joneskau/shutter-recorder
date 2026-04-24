using Shutter.App;
using Shutter.Core;
using System.Windows.Input;
using Xunit;

namespace Shutter.Core.Tests;

public class HotkeyBindingTests
{
    [Theory]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, false, false, false, HotkeyService.ModAlt)]
    [InlineData(false, true, false, false, HotkeyService.ModControl)]
    [InlineData(false, false, true, false, HotkeyService.ModShift)]
    [InlineData(false, false, false, true, HotkeyService.ModWin)]
    [InlineData(true, true, true, true, HotkeyService.ModAlt | HotkeyService.ModControl | HotkeyService.ModShift | HotkeyService.ModWin)]
    public void Modifiers_ReturnsCorrectBitmask(bool alt, bool ctrl, bool shift, bool win, uint expected)
    {
        var binding = new HotkeyBinding
        {
            Alt = alt,
            Ctrl = ctrl,
            Shift = shift,
            Win = win
        };

        Assert.Equal(expected, binding.Modifiers);
    }

    [Fact]
    public void VirtualKey_ValidKey_ReturnsCorrectCode()
    {
        var binding = new HotkeyBinding { Key = "A" };
        var expected = (uint)KeyInterop.VirtualKeyFromKey(Key.A);
        Assert.Equal(expected, binding.VirtualKey);
    }

    [Fact]
    public void VirtualKey_InvalidKey_DefaultsToR()
    {
        var binding = new HotkeyBinding { Key = "InvalidKeyName" };
        var expected = (uint)KeyInterop.VirtualKeyFromKey(Key.R);
        Assert.Equal(expected, binding.VirtualKey);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var binding = new HotkeyBinding
        {
            Key = "R",
            Win = true,
            Shift = true,
            Alt = false,
            Ctrl = false
        };

        Assert.Equal("Win + Shift + R", binding.ToString());
    }
}
