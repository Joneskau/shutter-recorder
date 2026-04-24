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
    private readonly CaptureController _controller;

    public CaptureControllerTests()
    {
        _hotkeyService = Substitute.For<IHotkeyService>();
        _startRecording = Substitute.For<Action>();
        _stopRecording = Substitute.For<Action>();
        _controller = new CaptureController(_hotkeyService, _startRecording, _stopRecording);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        Assert.Equal(RecorderState.Idle, _controller.State);
    }

    [Fact]
    public void HotkeyPressed_WhenIdle_StartsRecording()
    {
        // Act
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);

        // Assert
        Assert.Equal(RecorderState.Recording, _controller.State);
        _startRecording.Received(1).Invoke();
    }

    [Fact]
    public void HotkeyPressed_WhenRecording_StopsRecording()
    {
        // Arrange
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty); // Transition to Recording

        // Act
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);

        // Assert
        Assert.Equal(RecorderState.Idle, _controller.State);
        _stopRecording.Received(1).Invoke();
    }

    [Fact]
    public void HotkeyPressed_WhenStartFails_RevertsToIdle()
    {
        // Arrange
        _startRecording.When(x => x.Invoke()).Do(_ => throw new Exception("Failed to start"));

        // Act & Assert
        Assert.Throws<Exception>(() => _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty));
        Assert.Equal(RecorderState.Idle, _controller.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task HotkeyPressed_DuringStateTransition_IsIgnored()
    {
        // Arrange
        var syncStart = new System.Threading.ManualResetEvent(false);
        var syncComplete = new System.Threading.ManualResetEvent(false);

        _startRecording.When(x => x.Invoke()).Do(_ => 
        {
            syncStart.Set();
            syncComplete.WaitOne();
        });

        // Start first press in background
        var firstPressTask = System.Threading.Tasks.Task.Run(() => 
        {
            _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);
        });

        syncStart.WaitOne(); // Wait until startRecording is called

        // Act: Second press while first is still in progress (State is Starting)
        _hotkeyService.HotkeyPressed += Raise.EventWith(EventArgs.Empty);

        syncComplete.Set(); // Let first press complete
        await firstPressTask;

        // Assert
        _startRecording.Received(1).Invoke(); // Only called once
        Assert.Equal(RecorderState.Recording, _controller.State);
    }
}
