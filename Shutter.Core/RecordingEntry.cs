using System;

namespace Shutter.Core;

public sealed record RecordingEntry
{
    public required string FileName    { get; init; }
    public required string Path        { get; init; }
    public required string Duration    { get; init; }  // "mm:ss"
    public long            SizeBytes   { get; init; }
    public DateTimeOffset  RecordedAt  { get; init; }
    public bool            WasSilent   { get; init; }
}
