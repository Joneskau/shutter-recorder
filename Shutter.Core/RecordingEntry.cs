using System;

namespace Shutter.Core;

public sealed record RecordingEntry(
    string FileName,
    string Path,
    TimeSpan Duration,
    long SizeBytes,
    DateTimeOffset RecordedAt,
    bool WasSilent
);
