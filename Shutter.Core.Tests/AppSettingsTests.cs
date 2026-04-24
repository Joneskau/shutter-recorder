using System.IO;
using System.Text.Json;
using Shutter.App;
using Xunit;

namespace Shutter.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _tempPath;

    public AppSettingsTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"shutter_settings_test_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var settings = AppSettings.Load(_tempPath);
        
        Assert.NotNull(settings);
        Assert.Equal("R", settings.HotkeyKey);
        Assert.True(settings.HotkeyShift);
        Assert.True(settings.HotkeyWin);
    }

    [Fact]
    public void SaveAndLoad_PersistsSettings()
    {
        var settings = new AppSettings
        {
            HotkeyKey = "X",
            HotkeyCtrl = true,
            OverlayLeft = 123
        };

        settings.Save(_tempPath);
        var loaded = AppSettings.Load(_tempPath);

        Assert.Equal("X", loaded.HotkeyKey);
        Assert.True(loaded.HotkeyCtrl);
        Assert.Equal(123, loaded.OverlayLeft);
    }

    [Fact]
    public void Load_WhenJsonIsInvalid_ReturnsDefaultSettings()
    {
        File.WriteAllText(_tempPath, "NOT VALID JSON {");

        var settings = AppSettings.Load(_tempPath);

        Assert.NotNull(settings);
        Assert.Equal("R", settings.HotkeyKey); // Default
    }

    [Fact]
    public void Load_WhenJsonIsNull_ReturnsDefaultSettings()
    {
        File.WriteAllText(_tempPath, "null");

        var settings = AppSettings.Load(_tempPath);

        Assert.NotNull(settings);
        Assert.Equal("R", settings.HotkeyKey); // Default
    }
}
