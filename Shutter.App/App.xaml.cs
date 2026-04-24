using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Shutter.Core;

namespace Shutter.App;

public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private RecorderService? _recorderService;
    private CaptureController? _controller;
    private OverlayWindow? _overlay;
    private NotificationService? _notifications;
    private RecordingHistoryService? _historyService;
    private HistoryWindow? _historyWindow;
    private TaskbarIcon? _trayIcon;
    private AppSettings? _settings;

    private static readonly string OutputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recordings");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();
        Directory.CreateDirectory(OutputFolder);

        _recorderService = new RecorderService { SelectedDeviceId = _settings.InputDeviceId };
        _hotkeyService = new HotkeyService();
        _overlay = new OverlayWindow();
        _notifications = new NotificationService();
        
        var historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShutterRecorder", "history.json");
        _historyService = new RecordingHistoryService(historyPath);

        _historyWindow = new HistoryWindow(_historyService);
        if (_settings.HistoryLeft.HasValue && _settings.HistoryTop.HasValue)
        {
            _historyWindow.Left = _settings.HistoryLeft.Value;
            _historyWindow.Top = _settings.HistoryTop.Value;
        }
        _historyWindow.PositionChanged += point =>
        {
            _settings.SetHistoryPosition(point);
            _settings.Save();
        };

        _overlay.SetPosition(_settings.OverlayPosition);
        _overlay.PositionChanged += point =>
        {
            _settings.SetOverlayPosition(point);
            _settings.Save();
        };

        _recorderService.LevelAvailable += level => _overlay?.UpdateLevel(level);

        _controller = new CaptureController(
            _hotkeyService,
            startRecording: () =>
            {
                _recorderService.Start(OutputFolder);
                _overlay.StartRecording();
            },
            stopRecording: () =>
            {
                _recorderService.Stop();
                _overlay.StopRecording();
                var path = _recorderService.LastSavedPath!;
                Clipboard.SetText(path);
                _notifications?.ShowSaved(path);
                
                var entry = new RecordingEntry(
                    Path.GetFileName(path),
                    path,
                    _recorderService.LastSavedDuration,
                    _recorderService.LastSavedSizeBytes,
                    DateTimeOffset.Now,
                    _recorderService.LastSavedWasSilent
                );
                _historyService?.Add(entry);
            },
            pauseRecording: () =>
            {
                _recorderService.Pause();
                _overlay.PauseRecording();
            },
            resumeRecording: () =>
            {
                _recorderService.Resume();
                _overlay.ResumeRecording();
            }
        );

        RegisterHotkeyOrShowError(_settings.ToHotkeyBinding());
        RegisterPauseHotkeyOrShowError(_settings.ToPauseHotkeyBinding());
        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        if (_recorderService is null)
        {
            return;
        }

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Shutter Recorder",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenu = BuildTrayMenu()
        };
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        var historyItem = new MenuItem { Header = "Recording History" };
        historyItem.Click += (_, _) =>
        {
            if (_historyWindow != null)
            {
                _historyWindow.Show();
                _historyWindow.Activate();
            }
        };
        menu.Items.Add(historyItem);
        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        var inputMenu = new MenuItem { Header = "Input Device" };
        foreach (var device in _recorderService!.GetInputDevices())
        {
            var item = new MenuItem
            {
                Header = device.Name,
                IsCheckable = true,
                IsChecked = device.Id == _recorderService.SelectedDeviceId
            };

            item.Click += (_, _) =>
            {
                _recorderService.SelectedDeviceId = device.Id;
                _settings!.InputDeviceId = device.Id;
                _settings.Save();

                foreach (var sibling in inputMenu.Items)
                {
                    if (sibling is MenuItem siblingItem)
                    {
                        siblingItem.IsChecked = false;
                    }
                }

                item.IsChecked = true;
            };

            inputMenu.Items.Add(item);
        }

        menu.Items.Add(inputMenu);
        menu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(quitItem);

        return menu;
    }

    private void OpenSettings()
    {
        if (_hotkeyService is null || _recorderService is null || _settings is null)
        {
            return;
        }

        var window = new MainWindow(_hotkeyService.Binding, _recorderService.GetInputDevices(), _recorderService.SelectedDeviceId);
        if (window.ShowDialog() != true)
        {
            return;
        }

        var selectedHotkey = window.SelectedHotkey;
        if (!_hotkeyService.ReRegister(selectedHotkey))
        {
            ShowHotkeyError();
            _hotkeyService.ReRegister(_settings.ToHotkeyBinding());
            return;
        }

        _settings.ApplyHotkeyBinding(selectedHotkey);
        _recorderService.SelectedDeviceId = window.SelectedDeviceId;
        _settings.InputDeviceId = window.SelectedDeviceId;
        _settings.Save();

        if (_trayIcon != null)
        {
            _trayIcon.ContextMenu = BuildTrayMenu();
        }
    }

    private void RegisterHotkeyOrShowError(HotkeyBinding binding)
    {
        if (!_hotkeyService!.Register(binding))
        {
            ShowHotkeyError("record");
        }
    }

    private void RegisterPauseHotkeyOrShowError(HotkeyBinding binding)
    {
        if (!_hotkeyService!.RegisterPause(binding))
        {
            ShowHotkeyError("pause");
        }
    }

    private static void ShowHotkeyError(string action = "record")
    {
        var err = Marshal.GetLastWin32Error();
        var msg = err == 1409
            ? $"The {action} hotkey is already in use by another app."
            : $"Failed to register {action} hotkey (error {err}).";
        MessageBox.Show(msg, "Shutter — Hotkey Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _notifications?.Dispose();
        _hotkeyService?.Unregister();
        _recorderService?.Dispose();
        
        if (_historyWindow != null)
        {
            _historyWindow.PositionChanged -= null; // detach logic if necessary
            _historyWindow.Close();
        }

        base.OnExit(e);
    }
}
