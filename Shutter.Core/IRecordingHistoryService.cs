using System.Collections.ObjectModel;

namespace Shutter.Core;

public interface IRecordingHistoryService
{
    ObservableCollection<RecordingEntry> Entries { get; }
    void Add(RecordingEntry entry);
    void Remove(RecordingEntry entry);
    void Update(RecordingEntry oldEntry, RecordingEntry newEntry);
}
