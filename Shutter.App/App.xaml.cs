using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Shutter.Core;

namespace Shutter.App;

public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private RecorderService? _recorderService;
    private CaptureController? _controller;

    private static readonly string OutputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recordings");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Directory.CreateDirectory(OutputFolder);

        _recorderService = new RecorderService();
        _hotkeyService = new HotkeyService(MainWindow);

        _controller = new CaptureController(
            _hotkeyService,
            startRecording: () => _recorderService.Start(OutputFolder),
            stopRecording: () =>
            {
                _recorderService.Stop();
                var path = _recorderService.LastSavedPath!;
                Clipboard.SetText(path);
                MessageBox.Show($"Saved: {Path.GetFileName(path)}", "Shutter");
            }
        );

        if (!_hotkeyService.Register())
        {
            var err = Marshal.GetLastWin32Error();
            var msg = err == 1409
                ? "Ctrl+Alt+Space is already in use by another app."
                : $"Failed to register hotkey (error {err}).";
            MessageBox.Show(msg, "Shutter — Hotkey Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Unregister();
        _recorderService?.Dispose();
        base.OnExit(e);
    }
}

