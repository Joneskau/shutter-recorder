using System;
using Shutter.Core;
using NSubstitute;
using Xunit;

namespace Shutter.Core.Tests;

public class CaptureControllerTests
{
    private readonly IHotkeyService _hotkeyService;
    private readonly Action _startRecording;
    private readonly Action _stopRecording;
    private readonly Action _pauseRecording;
    private readonly Action _resumeRecording;
    private readonly CaptureController _controller;

    public CaptureControllerTests()
    {
        _hotkeyService = Substitute.For<IHotkeyService>();
        _startRecording = Substitute.For<Action>();
        _stopRecording = Substitute.For<Action>();
        _pauseRecording = Substitute.For<Action>();
        _resumeRecording = Substitute.For<Action>();
        _controller = new CaptureController(
            _hotkeyService, _startRecording, _stopRecording, _pauseRecording, _resumeRecording);
    }

    // ── Baseline ─────────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsIdle()
    {
        Assert.Equal(RecorderState.Idle, _controller.State);
    }

    [Fact]
    public void HotkeyPressed_WhenIdle_StartsRecording()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);

        Assert.Equal(RecorderState.Recording, _controller.State);
        _startRecording.Received(1).Invoke();
    }

    [Fact]
    public void HotkeyPressed_WhenRecording_StopsRecording()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording

        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Idle

        Assert.Equal(RecorderState.Idle, _controller.State);
        _stopRecording.Received(1).Invoke();
    }

    [Fact]
    public void HotkeyPressed_WhenStartFails_RevertsToIdle()
    {
        _startRecording.When(x => x.Invoke()).Do(_ => throw new Exception("Failed to start"));

        Assert.Throws<Exception>(() => _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty));
        Assert.Equal(RecorderState.Idle, _controller.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task HotkeyPressed_DuringStateTransition_IsIgnored()
    {
        var syncStart = new System.Threading.ManualResetEvent(false);
        var syncComplete = new System.Threading.ManualResetEvent(false);

        _startRecording.When(x => x.Invoke()).Do(_ =>
        {
            syncStart.Set();
            syncComplete.WaitOne();
        });

        var firstPressTask = System.Threading.Tasks.Task.Run(() =>
        {
            _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);
        });

        syncStart.WaitOne();

        // Second press while State == Starting — should be ignored.
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);

        syncComplete.Set();
        await firstPressTask;

        _startRecording.Received(1).Invoke();
        Assert.Equal(RecorderState.Recording, _controller.State);
    }

    // ── Pause / Resume ────────────────────────────────────────────────────────

    [Fact]
    public void PauseHotkey_WhenRecording_TransitionsToPaused()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording

        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty);

        Assert.Equal(RecorderState.Paused, _controller.State);
        _pauseRecording.Received(1).Invoke();
        _resumeRecording.DidNotReceive().Invoke();
    }

    [Fact]
    public void PauseHotkey_WhenPaused_ResumesToRecording()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Paused

        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording

        Assert.Equal(RecorderState.Recording, _controller.State);
        _resumeRecording.Received(1).Invoke();
    }

    [Fact]
    public void PauseHotkey_WhenIdle_IsIgnored()
    {
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty);

        Assert.Equal(RecorderState.Idle, _controller.State);
        _pauseRecording.DidNotReceive().Invoke();
    }

    [Fact]
    public void StopHotkey_WhenPaused_StopsRecording()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Paused

        // Stop without resuming first — must be accepted from Paused.
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Idle

        Assert.Equal(RecorderState.Idle, _controller.State);
        _stopRecording.Received(1).Invoke();
    }

    [Fact]
    public void PauseHotkey_CanCycleMultipleTimes()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording

        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Paused
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Paused
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording

        Assert.Equal(RecorderState.Recording, _controller.State);
        _pauseRecording.Received(2).Invoke();
        _resumeRecording.Received(2).Invoke();
    }

    [Fact]
    public void PauseHotkey_WhenPauseFails_RevertsToRecording()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording
        _pauseRecording.When(x => x.Invoke()).Do(_ => throw new Exception("pause failed"));

        Assert.Throws<Exception>(() =>
            _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty));

        Assert.Equal(RecorderState.Recording, _controller.State);
    }

    [Fact]
    public void PauseHotkey_WhenResumeFails_RevertsTosPaused()
    {
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Recording
        _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty); // → Paused
        _resumeRecording.When(x => x.Invoke()).Do(_ => throw new Exception("resume failed"));

        Assert.Throws<Exception>(() =>
            _hotkeyService.PauseHotkeyPressed += Raise.EventWith(EventArgs.Empty));

        Assert.Equal(RecorderState.Paused, _controller.State);
    }
}
