using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Shutter.Core;

public class RecordingHistoryService : IRecordingHistoryService
{
    private readonly string _historyFilePath;
    private const int MaxEntries = 100;

    public ObservableCollection<RecordingEntry> Entries { get; }

    public RecordingHistoryService(string historyFilePath)
    {
        _historyFilePath = historyFilePath;
        Entries = new ObservableCollection<RecordingEntry>(LoadHistory());
    }

    public void Add(RecordingEntry entry)
    {
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        SaveHistory();
    }

    public void Remove(RecordingEntry entry)
    {
        if (Entries.Remove(entry))
        {
            SaveHistory();
        }
    }

    private RecordingEntry[] LoadHistory()
    {
        if (!File.Exists(_historyFilePath))
        {
            return Array.Empty<RecordingEntry>();
        }

        try
        {
            var json = File.ReadAllText(_historyFilePath);
            var entries = JsonSerializer.Deserialize<RecordingEntry[]>(json);
            return entries ?? Array.Empty<RecordingEntry>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load history: {ex.Message}");
            return Array.Empty<RecordingEntry>();
        }
    }

    private void SaveHistory()
    {
        try
        {
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Entries.ToArray(), options);
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save history: {ex.Message}");
        }
    }
}
