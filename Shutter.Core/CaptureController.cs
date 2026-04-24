using System;

namespace Shutter.Core;

/// <summary>
/// State machine: Idle → Starting → Recording ↔ Pausing/Paused/Resuming → Stopping → Idle.
/// Hotkey presses during any transient state (Starting/Stopping/Pausing/Resuming) are silently ignored.
/// Stop is accepted from both Recording and Paused states.
/// </summary>
public class CaptureController
{
    private readonly IHotkeyService _hotkey;
    private readonly Action _startRecording;
    private readonly Action _stopRecording;
    private readonly Action _pauseRecording;
    private readonly Action _resumeRecording;

    public RecorderState State { get; private set; } = RecorderState.Idle;

    public CaptureController(
        IHotkeyService hotkey,
        Action startRecording,
        Action stopRecording,
        Action pauseRecording,
        Action resumeRecording)
    {
        _hotkey = hotkey;
        _startRecording = startRecording;
        _stopRecording = stopRecording;
        _pauseRecording = pauseRecording;
        _resumeRecording = resumeRecording;

        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _hotkey.PauseHotkeyPressed += OnPauseHotkeyPressed;
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
                throw; // surface to caller
            }
        }
        else if (State == RecorderState.Recording || State == RecorderState.Paused)
        {
            // Stop is valid from both Recording and Paused states.
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
        // Starting, Pausing, Resuming, Stopping — ignore the press.
    }

    private void OnPauseHotkeyPressed(object? sender, EventArgs e)
    {
        if (State == RecorderState.Recording)
        {
            State = RecorderState.Pausing;
            try
            {
                _pauseRecording();
                State = RecorderState.Paused;
            }
            catch
            {
                // If pause somehow fails, fall back to Recording so the user can still stop.
                State = RecorderState.Recording;
                throw;
            }
        }
        else if (State == RecorderState.Paused)
        {
            State = RecorderState.Resuming;
            try
            {
                _resumeRecording();
                State = RecorderState.Recording;
            }
            catch
            {
                State = RecorderState.Paused;
                throw;
            }
        }
        // Any other state — ignore.
    }
}
