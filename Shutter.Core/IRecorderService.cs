using System;

namespace Shutter.Core;

public interface IRecorderService
{
    string? LastSavedPath { get; }
    void Start(string outputFolder);
    void Pause();
    void Resume();
    void Stop();
}
