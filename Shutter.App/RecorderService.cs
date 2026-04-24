using System;
using System.IO;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Shutter.Core;

namespace Shutter.App;

public class RecorderService : IRecorderService, IDisposable
{
    private WasapiCapture? _capture;
    private WaveFileWriter? _writer;
    private string? _finalPath;
    private string? _tempPath;

    public void Start(string filePath)
    {
        _finalPath = filePath;
        _tempPath = filePath + ".tmp";

        _capture = new WasapiCapture();
        _writer = new WaveFileWriter(_tempPath, _capture.WaveFormat);

        _capture.DataAvailable += (s, e) =>
        {
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        _capture.RecordingStopped += (s, e) =>
        {
            _writer?.Dispose();
            _writer = null;
            _capture?.Dispose();
            _capture = null;

            if (File.Exists(_tempPath))
            {
                if (File.Exists(_finalPath)) File.Delete(_finalPath);
                File.Move(_tempPath, _finalPath);
            }
        };

        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture?.StopRecording();
    }

    public void Dispose()
    {
        Stop();
        _writer?.Dispose();
        _capture?.Dispose();
    }
}
