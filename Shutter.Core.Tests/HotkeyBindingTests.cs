using Shutter.App;
using Shutter.Core;
using System.Windows.Input;
using Xunit;

namespace Shutter.Core.Tests;

public class HotkeyBindingTests
{


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
