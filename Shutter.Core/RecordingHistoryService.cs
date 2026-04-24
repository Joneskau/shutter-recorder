using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Shutter.Core;

/// <summary>
/// JSON-backed recording history.  Capped at <see cref="MaxEntries"/> (oldest trimmed).
/// Any I/O or parse failure is non-fatal: the service starts with an empty list.
/// </summary>
public sealed class RecordingHistoryService : IRecordingHistoryService
{
    public const int MaxEntries = 100;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _historyPath;
    private readonly List<RecordingEntry> _entries;
    private readonly object _lock = new();

    public static string DefaultHistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShutterRecorder", "history.json");

    public RecordingHistoryService(string? historyPath = null)
    {
        _historyPath = historyPath ?? DefaultHistoryPath;
        _entries = Load(_historyPath);
    }

    public IReadOnlyList<RecordingEntry> Entries
    {
        get { lock (_lock) { return _entries.AsReadOnly(); } }
    }

    public void Add(RecordingEntry entry)
    {
        lock (_lock)
        {
            _entries.Insert(0, entry); // newest first
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(_entries.Count - 1);
            Save();
        }
    }

    public void Remove(RecordingEntry entry)
    {
        lock (_lock)
        {
            _entries.Remove(entry);
            Save();
        }
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private static List<RecordingEntry> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<RecordingEntry>>(json) ?? [];
        }
        catch
        {
            // Corrupt file: start fresh, don't crash.
            return [];
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch { /* non-fatal */ }
    }
}
