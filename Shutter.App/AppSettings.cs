using System.IO;
using System.Text.Json;
using System.Windows;
using Shutter.Core;

namespace Shutter.App;

public sealed class AppSettings
{
    public string HotkeyKey { get; set; } = "R";
    public bool HotkeyCtrl { get; set; }
    public bool HotkeyAlt { get; set; }
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyWin { get; set; } = true;

    public string RecordingMode { get; set; } = "toggle";
    public int MinimumRecordingMs { get; set; } = 300;

    public string PushToTalkHotkeyKey { get; set; } = "Space";
    public bool PushToTalkHotkeyCtrl { get; set; } = true;
    public bool PushToTalkHotkeyAlt { get; set; } = true;
    public bool PushToTalkHotkeyShift { get; set; }
    public bool PushToTalkHotkeyWin { get; set; }

    public string OutputFormat { get; set; } = "wav";
    public string Quality { get; set; } = "standard";

    // Pause/resume hotkey — defaults to Ctrl+Alt+P
    public string PauseHotkeyKey { get; set; } = "P";
    public bool PauseHotkeyCtrl { get; set; } = true;
    public bool PauseHotkeyAlt { get; set; } = true;
    public bool PauseHotkeyShift { get; set; }
    public bool PauseHotkeyWin { get; set; }

    public double OverlayLeft { get; set; } = 600;
    public double OverlayTop { get; set; } = 20;
    
    public double? HistoryLeft { get; set; }
    public double? HistoryTop { get; set; }

    public string? InputDeviceId { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShutterRecorder",
        "settings.json");

    public static AppSettings Load(string? path = null)
    {
        var actualPath = path ?? SettingsPath;
        if (!File.Exists(actualPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(actualPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(string? path = null)
    {
        var actualPath = path ?? SettingsPath;
        var directory = Path.GetDirectoryName(actualPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(actualPath, json);
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

    public HotkeyBinding ToPushToTalkHotkeyBinding()
    {
        return new HotkeyBinding
        {
            Key = PushToTalkHotkeyKey,
            Ctrl = PushToTalkHotkeyCtrl,
            Alt = PushToTalkHotkeyAlt,
            Shift = PushToTalkHotkeyShift,
            Win = PushToTalkHotkeyWin
        };
    }

    public void ApplyPushToTalkHotkeyBinding(HotkeyBinding binding)
    {
        PushToTalkHotkeyKey = binding.Key;
        PushToTalkHotkeyCtrl = binding.Ctrl;
        PushToTalkHotkeyAlt = binding.Alt;
        PushToTalkHotkeyShift = binding.Shift;
        PushToTalkHotkeyWin = binding.Win;
    }

    public HotkeyBinding ToPauseHotkeyBinding()
    {
        return new HotkeyBinding
        {
            Key = PauseHotkeyKey,
            Ctrl = PauseHotkeyCtrl,
            Alt = PauseHotkeyAlt,
            Shift = PauseHotkeyShift,
            Win = PauseHotkeyWin
        };
    }

    public void ApplyPauseHotkeyBinding(HotkeyBinding binding)
    {
        PauseHotkeyKey = binding.Key;
        PauseHotkeyCtrl = binding.Ctrl;
        PauseHotkeyAlt = binding.Alt;
        PauseHotkeyShift = binding.Shift;
        PauseHotkeyWin = binding.Win;
    }

    public Point OverlayPosition => new(OverlayLeft, OverlayTop);

    public void SetOverlayPosition(Point point)
    {
        OverlayLeft = point.X;
        OverlayTop = point.Y;
    }

    public void SetHistoryPosition(Point point)
    {
        HistoryLeft = point.X;
        HistoryTop = point.Y;
    }
}
