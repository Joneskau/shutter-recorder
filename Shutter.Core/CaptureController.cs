using System;

namespace Shutter.Core;

public class CaptureController
{
    private readonly IHotkeyService _hotkey;
    private readonly Action _startRecording;
    private readonly Action _stopRecording;

    public RecorderState State { get; private set; } = RecorderState.Idle;

    public CaptureController(
        IHotkeyService hotkey,
        Action startRecording,
        Action stopRecording)
    {
        _hotkey = hotkey;
        _startRecording = startRecording;
        _stopRecording = stopRecording;
        _hotkey.HotkeyPressed += OnHotkeyPressed;
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (State == RecorderState.Idle)
        {
            State = RecorderState.Starting;
            try
            {
                _startRecording();
                State = RecorderState.Recording;
            }
            catch
            {
                State = RecorderState.Idle;
                throw; // let the caller surface the error
            }
        }
        else if (State == RecorderState.Recording)
        {
            State = RecorderState.Stopping;
            try
            {
                _stopRecording();
            }
            finally
            {
                State = RecorderState.Idle;
            }
        }
        // any other state (Starting, Stopping) — ignore the press
    }
}
