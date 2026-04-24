using System;

namespace Shutter.Core;

public interface IRecorderService
{
    void Start(string filePath);
    void Stop();
}
