using System;

namespace Shutter.Core;

public interface IRecorderService
{
    string? LastSavedPath { get; }
    TimeSpan LastSavedDuration { get; }
    long LastSavedSizeBytes { get; }
    bool LastSavedWasSilent { get; }
    void Start(string outputFolder);
    void Pause();
    void Resume();
    void Stop();
}
