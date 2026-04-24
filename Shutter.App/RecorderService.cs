using System;
using System.Collections.Generic;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Shutter.Core;

namespace Shutter.App;

public class RecorderService : IRecorderService, IDisposable
{
    private WasapiCapture? _capture;
    private WaveFileWriter? _writer;
    private string? _tempPath;
    // Volatile: written by the UI/controller thread, read by the audio callback thread.
    private volatile bool _isPaused;
    
    private double _rmsSum;
    private int _rmsCount;

    public string? LastSavedPath { get; private set; }
    public TimeSpan LastSavedDuration { get; private set; }
    public long LastSavedSizeBytes { get; private set; }
    public bool LastSavedWasSilent { get; private set; }

    public event Action<float>? LevelAvailable;

    public string? SelectedDeviceId { get; set; }

    public IReadOnlyList<InputDeviceOption> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var items = new List<InputDeviceOption>();

        foreach (var device in devices)
        {
            items.Add(new InputDeviceOption(device.ID, device.FriendlyName));
        }

        return items;
    }

    public void Start(string outputFolder)
    {
        var device = ResolveSelectedDevice();
        _capture = device is null ? new WasapiCapture() : new WasapiCapture(device);

        _rmsSum = 0;
        _rmsCount = 0;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _tempPath = Path.Combine(outputFolder, $"{timestamp}.wav.tmp");

        _writer = new WaveFileWriter(_tempPath, _capture.WaveFormat);
        _capture.DataAvailable += (s, e) =>
        {
            // Discard buffers while paused — keeps WASAPI running for instant resume
            // and avoids false silence accumulation in the RMS sum.
            if (_isPaused) return;
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
            var rms = CalculateRms(e.Buffer, e.BytesRecorded);
            _rmsSum += rms;
            _rmsCount++;
            LevelAvailable?.Invoke(rms);
        };

        _capture.StartRecording();
    }

    public void Pause() => _isPaused = true;

    public void Resume() => _isPaused = false;

    public void Stop()
    {
        _isPaused = false; // reset in case we stopped while paused
        LastSavedDuration = _writer?.TotalTime ?? TimeSpan.Zero;
        
        // Very basic silence detection threshold (e.g. 0.005)
        LastSavedWasSilent = _rmsCount > 0 && (_rmsSum / _rmsCount) < 0.005;

        _capture?.StopRecording();
        _writer?.Dispose();
        _writer = null;
        _capture?.Dispose();
        _capture = null;

        if (_tempPath == null) return;
        var finalPath = Path.ChangeExtension(_tempPath, null);
        File.Move(_tempPath, finalPath, overwrite: true);
        LastSavedPath = finalPath;
        LastSavedSizeBytes = new FileInfo(finalPath).Length;
        _tempPath = null;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _capture?.Dispose();
    }

    private MMDevice? ResolveSelectedDevice()
    {
        if (string.IsNullOrWhiteSpace(SelectedDeviceId))
        {
            return null;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDevice(SelectedDeviceId);
        }
        catch
        {
            return null;
        }
    }

    private static float CalculateRms(byte[] buffer, int bytesRecorded)
    {
        var samples = bytesRecorded / 2;
        if (samples == 0)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < bytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(buffer, i);
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / samples) / short.MaxValue;
    }
}

public sealed record InputDeviceOption(string Id, string Name);
