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
    private IHotkeyService? _hotkeyService;
    private RecorderService? _recorderService;
    private CaptureController? _controller;
    private OverlayWindow? _overlay;
    private NotificationService? _notifications;
    private RecordingHistoryService? _historyService;
    private DeviceHealthService? _deviceHealthService;
    private HistoryWindow? _historyWindow;
    private TaskbarIcon? _trayIcon;
    private AppSettings? _settings;
    private EventBus? _eventBus;

    private static readonly string OutputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recordings");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();
        Directory.CreateDirectory(OutputFolder);

        _recorderService = new RecorderService { SelectedDeviceId = _settings.InputDeviceId };
        
        if (_settings.RecordingMode == "pushToTalk")
        {
            _hotkeyService = new LowLevelKeyboardHookService();
        }
        else
        {
            _hotkeyService = new HotkeyService();
        }
        
        _overlay = new OverlayWindow();
        _notifications = new NotificationService();
        _deviceHealthService = new DeviceHealthService();
        
        // Layer A: Startup health check
        var initialResolution = _deviceHealthService.ResolveDevice(_settings.InputDeviceId);
        if (initialResolution.IsFallback)
        {
            _notifications.ShowFallback(initialResolution.OriginalDeviceName, initialResolution.DeviceName);
        }

        _deviceHealthService.DeviceAdded += (id, name) =>
        {
            if (_settings.InputDeviceId == id)
            {
                _notifications.ShowDeviceChanged(name, true);
            }
        };

        _deviceHealthService.DeviceRemoved += (id, name) =>
        {
            if (_settings.InputDeviceId == id)
            {
                _notifications.ShowDeviceChanged(name, false);
            }
        };
        
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

        _eventBus = new EventBus(_settings.Stealth);

        _eventBus.Subscribe<MicNotFoundEvent>("errorToast", _ =>
        {
            _notifications.ShowError("No microphone detected.");
        });

        _eventBus.Subscribe<RecordingStartedEvent>("widget", e =>
        {
            _overlay.StartRecording(e.IsPushToTalk);
        });

        _eventBus.Subscribe<RecordingSavedEvent>("savedToast", e =>
        {
            _notifications.ShowSaved(e.FilePath);
        });

        _eventBus.Subscribe<RecordingFailedEvent>("errorToast", e =>
        {
            _notifications.ShowError(e.Reason);
        });

        _eventBus.Subscribe<SilenceDetectedEvent>("silenceWarning", _ =>
        {
            // The silence warning is an error toast indicating the recording was silent.
            _notifications.ShowError("Recording was silent.");
        });

        _eventBus.Subscribe<ClipboardCopiedEvent>("clipboard", e =>
        {
            Clipboard.SetText(e.Text);
        });

        _eventBus.Subscribe<HotkeyCollisionEvent>("errorToast", e =>
        {
            var msg = e.Action == "1409"
                ? "The hotkey is already in use by another app."
                : $"Failed to register hotkey (error {e.Action}).";
            MessageBox.Show(msg, "Shutter — Hotkey Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });

        _eventBus.Subscribe<RecordingSavedEvent>("widget", _ =>
        {
            _overlay.StopRecording();
        });

        _eventBus.Subscribe<RecordingFailedEvent>("widget", _ =>
        {
            _overlay.StopRecording();
        });

        _controller = new CaptureController(
            _hotkeyService,
            startRecording: () =>
            {
                // Layer B: Pre-recording health check
                var resolution = _deviceHealthService.ResolveDevice(_settings.InputDeviceId);
                if (string.IsNullOrEmpty(resolution.DeviceId))
                {
                    _eventBus.Publish(new MicNotFoundEvent());
                    throw new InvalidOperationException("No input device available");
                }

                if (resolution.IsFallback)
                {
                    _notifications.ShowFallback(resolution.OriginalDeviceName, resolution.DeviceName);
                }

                _recorderService.SelectedDeviceId = resolution.DeviceId;
                _overlay.SetFallbackMode(resolution.IsFallback);
                
                _recorderService.Start(OutputFolder);
                _eventBus.Publish(new RecordingStartedEvent(_settings.RecordingMode == "pushToTalk"));
            },
            stopRecording: (save) =>
            {
                _recorderService.Stop();
                var wavPath = _recorderService.LastSavedPath!;
                
                if (!save)
                {
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                    _eventBus.Publish(new RecordingFailedEvent("Recording discarded (too short)."));
                    return;
                }
                
                _eventBus.Publish(new ClipboardCopiedEvent(wavPath));
                
                var format = _settings!.OutputFormat?.ToLowerInvariant() ?? "wav";
                var qualityStr = _settings.Quality?.ToLowerInvariant() ?? "standard";
                var quality = qualityStr switch {
                    "low" => QualityPreset.Low,
                    "high" => QualityPreset.High,
                    _ => QualityPreset.Standard
                };
                
                if (format != "wav" && format != "mp3" && format != "opus") {
                    _notifications?.ShowError($"Unknown format '{format}', saving as WAV.");
                    format = "wav";
                }
                if (qualityStr != "low" && qualityStr != "standard" && qualityStr != "high") {
                    _notifications?.ShowError($"Unknown quality '{qualityStr}', using standard.");
                    quality = QualityPreset.Standard;
                }
                
                IEncoder encoder = format switch {
                    "mp3" => new Mp3Encoder(),
                    "opus" => new OpusEncoder(),
                    _ => new WavPassthroughEncoder()
                };

                var entry = new RecordingEntry(
                    Path.GetFileName(wavPath),
                    wavPath,
                    _recorderService.LastSavedDuration,
                    _recorderService.LastSavedSizeBytes,
                    DateTimeOffset.Now,
                    _recorderService.LastSavedWasSilent
                );
                _historyService?.Add(entry);

                if (_recorderService.LastSavedWasSilent)
                {
                    _eventBus.Publish(new SilenceDetectedEvent());
                }

                if (encoder is WavPassthroughEncoder && quality == QualityPreset.Standard)
                {
                    _eventBus.Publish(new RecordingSavedEvent(wavPath));
                    return;
                }

                var targetExt = format switch {
                    "mp3" => ".mp3",
                    "opus" => ".ogg",
                    _ => ".wav"
                };
                var finalPath = Path.ChangeExtension(wavPath, targetExt);

                System.Threading.Tasks.Task.Run(async () => {
                    try {
                        var encodedPath = await encoder.EncodeAsync(wavPath, finalPath, quality);
                        Application.Current.Dispatcher.Invoke(() => {
                            _eventBus.Publish(new ClipboardCopiedEvent(encodedPath));
                            _eventBus.Publish(new RecordingSavedEvent(encodedPath));
                            
                            var fileInfo = new FileInfo(encodedPath);
                            var updatedEntry = entry with {
                                Path = encodedPath,
                                FileName = Path.GetFileName(encodedPath),
                                SizeBytes = fileInfo.Length
                            };
                            _historyService?.Update(entry, updatedEntry);
                            
                            if (wavPath != encodedPath && File.Exists(wavPath)) {
                                File.Delete(wavPath);
                            }
                        });
                    } catch (Exception) {
                        Application.Current.Dispatcher.Invoke(() => {
                            _eventBus.Publish(new RecordingFailedEvent("Encoding failed — saved as WAV instead."));
                        });
                    }
                });
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
            },
            minimumRecordingMs: _settings.RecordingMode == "pushToTalk" ? _settings.MinimumRecordingMs : 0
        );

        RegisterHotkeyOrShowError(_settings.RecordingMode == "pushToTalk" ? _settings.ToPushToTalkHotkeyBinding() : _settings.ToHotkeyBinding());
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
        
        _hotkeyService.Unregister();
        if (!_hotkeyService.Register(selectedHotkey))
        {
            ShowHotkeyError();
            _hotkeyService.Register(_settings.RecordingMode == "pushToTalk" ? _settings.ToPushToTalkHotkeyBinding() : _settings.ToHotkeyBinding());
            return;
        }

        if (_settings.RecordingMode == "pushToTalk")
        {
            _settings.ApplyPushToTalkHotkeyBinding(selectedHotkey);
        }
        else
        {
            _settings.ApplyHotkeyBinding(selectedHotkey);
        }
        
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

    private void ShowHotkeyError(string action = "record")
    {
        var err = Marshal.GetLastWin32Error();
        _eventBus?.Publish(new HotkeyCollisionEvent(err.ToString()));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _notifications?.Dispose();
        _hotkeyService?.Unregister();
        _deviceHealthService?.Dispose();
        _recorderService?.Dispose();
        
        if (_historyWindow != null)
        {
            _historyWindow.PositionChanged -= null; // detach logic if necessary
            _historyWindow.Close();
        }

        base.OnExit(e);
    }
}
