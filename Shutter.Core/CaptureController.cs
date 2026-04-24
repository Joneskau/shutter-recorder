using System;
using System.IO;

namespace Shutter.Core;

public enum RecorderState { Idle, Starting, Recording, Stopping }

public class CaptureController
{
    private readonly IHotkeyService _hotkeyService;
    private readonly IRecorderService _recorderService;
    private RecorderState _state = RecorderState.Idle;

    public RecorderState State => _state;

    public event EventHandler<string>? RecordingFinished;
    public event EventHandler<Exception>? ErrorOccurred;

    public CaptureController(IHotkeyService hotkeyService, IRecorderService recorderService)
    {
        _hotkeyService = hotkeyService;
        _recorderService = recorderService;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_state == RecorderState.Idle)
        {
            StartRecording();
        }
        else if (_state == RecorderState.Recording)
        {
            StopRecording();
        }
    }

    public void StartRecording()
    {
        if (_state != RecorderState.Idle) return;

        try
        {
            _state = RecorderState.Starting;
            
            var outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            _recorderService.Start(outputFolder);
            
            _state = RecorderState.Recording;
        }
        catch (Exception ex)
        {
            _state = RecorderState.Idle;
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    public void StopRecording()
    {
        if (_state != RecorderState.Recording) return;

        try
        {
            _state = RecorderState.Stopping;
            _recorderService.Stop();
            _state = RecorderState.Idle;
            
            var path = _recorderService.LastSavedPath;
            if (path != null)
            {
                RecordingFinished?.Invoke(this, path);
            }
        }
        catch (Exception ex)
        {
            _state = RecorderState.Idle;
            ErrorOccurred?.Invoke(this, ex);
        }
    }
}
