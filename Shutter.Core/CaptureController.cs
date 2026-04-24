using System;

namespace Shutter.Core;

/// <summary>
/// State machine: Idle → Starting → Recording ↔ Pausing/Paused/Resuming → Stopping → Idle.
/// Hotkey presses during transient states are silently ignored.
/// Stop is accepted from both Recording and Paused.
/// </summary>
public class CaptureController
{
    private readonly IHotkeyService _hotkey;
    private readonly Action _startRecording;
    private readonly Action _stopRecording;
    private readonly Action _pauseRecording;
    private readonly Action _resumeRecording;

    // Timing — used to compute ActiveDuration excluding paused intervals.
    private DateTime _sessionStart;
    private TimeSpan _totalPausedDuration;
    private DateTime _pauseStart;

    public RecorderState State { get; private set; } = RecorderState.Idle;

    /// <summary>
    /// Active (non-paused) recording duration for the most recently completed session.
    /// Set just before the stop callback fires so it is readable inside that callback.
    /// </summary>
    public TimeSpan ActiveDuration { get; private set; }

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
            _sessionStart = DateTime.Now;
            _totalPausedDuration = TimeSpan.Zero;
            try
            {
                _startRecording();
                State = RecorderState.Recording;
            }
            catch
            {
                State = RecorderState.Idle;
                throw;
            }
        }
        else if (State == RecorderState.Recording || State == RecorderState.Paused)
        {
            // If stopping from Paused, account for the current paused interval.
            if (State == RecorderState.Paused)
                _totalPausedDuration += DateTime.Now - _pauseStart;

            ActiveDuration = (DateTime.Now - _sessionStart) - _totalPausedDuration;
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
            _pauseStart = DateTime.Now;
            try
            {
                _pauseRecording();
                State = RecorderState.Paused;
            }
            catch
            {
                State = RecorderState.Recording;
                throw;
            }
        }
        else if (State == RecorderState.Paused)
        {
            _totalPausedDuration += DateTime.Now - _pauseStart;
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
