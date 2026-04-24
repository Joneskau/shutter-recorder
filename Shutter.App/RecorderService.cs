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

    public string? LastSavedPath { get; private set; }

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

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _tempPath = Path.Combine(outputFolder, $"{timestamp}.wav.tmp");

        _writer = new WaveFileWriter(_tempPath, _capture.WaveFormat);
        _capture.DataAvailable += (s, e) =>
        {
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
            var rms = CalculateRms(e.Buffer, e.BytesRecorded);
            LevelAvailable?.Invoke(rms);
        };

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
        var finalPath = Path.ChangeExtension(_tempPath, null);
        File.Move(_tempPath, finalPath, overwrite: true);
        LastSavedPath = finalPath;
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
