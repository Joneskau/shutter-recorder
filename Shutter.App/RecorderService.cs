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
    private string? _tempPath;

    public string? LastSavedPath { get; private set; }

    public void Start(string outputFolder)
    {
        _capture = new WasapiCapture();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _tempPath = Path.Combine(outputFolder, $"{timestamp}.wav.tmp");

        _writer = new WaveFileWriter(_tempPath, _capture.WaveFormat);
        _capture.DataAvailable += (s, e) =>
            _writer.Write(e.Buffer, 0, e.BytesRecorded);

        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture?.StopRecording();
        _writer?.Dispose();
        _writer = null;
        _capture?.Dispose();
        _capture = null;

        if (_tempPath == null) return;
        var finalPath = Path.ChangeExtension(_tempPath, null); // removes .tmp
        File.Move(_tempPath, finalPath, overwrite: true);
        LastSavedPath = finalPath;
        _tempPath = null;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _capture?.Dispose();
    }
}
