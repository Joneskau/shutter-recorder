using System.IO;
using System.Text.Json;
using System.Windows;

namespace Shutter.App;

public sealed class AppSettings
{
    public string HotkeyKey { get; set; } = "R";
    public bool HotkeyCtrl { get; set; }
    public bool HotkeyAlt { get; set; }
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyWin { get; set; } = true;
    public double OverlayLeft { get; set; } = 600;
    public double OverlayTop { get; set; } = 20;
    public string? InputDeviceId { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShutterRecorder",
        "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public HotkeyBinding ToHotkeyBinding()
    {
        return new HotkeyBinding
        {
            Key = HotkeyKey,
            Ctrl = HotkeyCtrl,
            Alt = HotkeyAlt,
            Shift = HotkeyShift,
            Win = HotkeyWin
        };
    }

    public void ApplyHotkeyBinding(HotkeyBinding binding)
    {
        HotkeyKey = binding.Key;
        HotkeyCtrl = binding.Ctrl;
        HotkeyAlt = binding.Alt;
        HotkeyShift = binding.Shift;
        HotkeyWin = binding.Win;
    }

    public Point OverlayPosition => new(OverlayLeft, OverlayTop);

    public void SetOverlayPosition(Point point)
    {
        OverlayLeft = point.X;
        OverlayTop = point.Y;
    }
}
