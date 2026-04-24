using System.Collections.Generic;

namespace Shutter.Core;

public interface IRecordingHistoryService
{
    IReadOnlyList<RecordingEntry> Entries { get; }
    void Add(RecordingEntry entry);
    void Remove(RecordingEntry entry);
}
