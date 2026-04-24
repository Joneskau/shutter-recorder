using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Shutter.App;

public sealed class StealthAuditLog
{
    private readonly string _path;
    private readonly string _backupPath;
    private readonly object _gate = new();

    public StealthAuditLog()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShutterRecorder");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "stealth.log");
        _backupPath = Path.Combine(directory, "stealth.log.1");
    }

    public void Write(string @event, string filePath, TimeSpan duration, double peakRms, string preset, string stealthToggleSource)
    {
        lock (_gate)
        {
            RotateIfNeeded();

            var payload = new
            {
                @event,
                timestamp = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                filePath,
                durationSeconds = duration.TotalSeconds,
                peakRMS = peakRms,
                preset,
                stealthToggleSource
            };

            var line = JsonSerializer.Serialize(payload);
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var info = new FileInfo(_path);
        var overSize = info.Length >= 5 * 1024 * 1024;
        var overLines = false;

        if (!overSize)
        {
            var count = 0;
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                _ = reader.ReadLine();
                count++;
                if (count >= 10_000)
                {
                    overLines = true;
                    break;
                }
            }
        }

        if (!overSize && !overLines)
        {
            return;
        }

        if (File.Exists(_backupPath))
        {
            File.Delete(_backupPath);
        }

        File.Move(_path, _backupPath);
    }
}
