using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
    private GlobalHotkeyManager? _globalHotkeys;
    private StealthAuditLog? _stealthAuditLog;
    private DispatcherTimer? _aliveReminderTimer;
    private DateTime _lastInteractionUtc = DateTime.UtcNow;
    private string _stealthToggleSource = "config";
    private string _lastStealthPresetForToggle = "personal";

    private const int RuntimeToggleHotkeyId = 11;
    private const int QuitHotkeyId = 12;

    private static readonly string OutputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recordings");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();
        StealthFilenameService.EnsureSalt(_settings.Stealth);
        _settings.Save();
        Directory.CreateDirectory(OutputFolder);

        _recorderService = new RecorderService { SelectedDeviceId = _settings.InputDeviceId };
        _recorderService.BuildFileStem = () => StealthFilenameService.BuildFileStem(_settings.Stealth);
        
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
        _stealthAuditLog = new StealthAuditLog();
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
        InitializeEventBusSubscriptions();

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
                MarkInteraction();
            },
            stopRecording: (save) =>
            {
                _recorderService.Stop();
                var wavPath = _recorderService.LastSavedPath!;
                
                if (!save)
                {
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                    _eventBus.Publish(new RecordingFailedEvent("Recording discarded (too short)."));
                    WriteStealthAudit("RecordingFailed", wavPath);
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
                    WriteStealthAudit("RecordingSaved", wavPath);
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
                            WriteStealthAudit("RecordingSaved", encodedPath);
                            
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
                            WriteStealthAudit("RecordingFailed", wavPath);
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
        InitializeStealthHotkeys();
        CreateTrayIcon();
        ApplyStealthPreset(_settings.Stealth.Preset, isStartup: true);
        StartAliveReminderTimer();
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

        var window = new MainWindow(_hotkeyService.Binding, _recorderService.GetInputDevices(), _recorderService.SelectedDeviceId, _settings.Stealth);
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
        _settings.Stealth = window.StealthConfig;
        _settings.Save();

        // Also we must reinitialize the eventbus if stealth config changed
        _eventBus = new EventBus(_settings.Stealth);
        InitializeEventBusSubscriptions();

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

    private void InitializeStealthHotkeys()
    {
        _globalHotkeys = new GlobalHotkeyManager();
        if (!_globalHotkeys.Register(RuntimeToggleHotkeyId, _settings!.Stealth.RuntimeToggleHotkey, ToggleStealthRuntime))
        {
            ShowHotkeyError("stealth-toggle");
        }

        if (!_globalHotkeys.Register(QuitHotkeyId, _settings.Stealth.QuitHotkey, () =>
            {
                MarkInteraction();
                if (string.Equals(_settings.Stealth.Preset, "personal", StringComparison.OrdinalIgnoreCase))
                {
                    Shutdown();
                }
            }))
        {
            ShowHotkeyError("stealth-quit");
        }
    }

    private void ToggleStealthRuntime()
    {
        if (_settings is null)
        {
            return;
        }

        MarkInteraction();
        _stealthToggleSource = "hotkey";
        var current = _settings.Stealth.Preset;
        if (string.Equals(current, "off", StringComparison.OrdinalIgnoreCase))
        {
            var next = string.IsNullOrWhiteSpace(_lastStealthPresetForToggle) ? "personal" : _lastStealthPresetForToggle;
            ApplyStealthPreset(next, isStartup: false);
        }
        else
        {
            _lastStealthPresetForToggle = current;
            ApplyStealthPreset("off", isStartup: false);
        }
    }

    private void ApplyStealthPreset(string preset, bool isStartup)
    {
        if (_settings is null || _trayIcon is null)
        {
            return;
        }

        _settings.Stealth.Preset = preset;

        if (string.Equals(preset, "personal", StringComparison.OrdinalIgnoreCase))
        {
            _settings.Stealth.SuppressOnSuccess = new() { "widget", "savedToast", "trayIcon" };
            _trayIcon.Visibility = Visibility.Collapsed;
            ShowStealthOnboardingIfNeeded(isStartup);
        }
        else if (string.Equals(preset, "meeting", StringComparison.OrdinalIgnoreCase))
        {
            _settings.Stealth.SuppressOnSuccess = new();
            _trayIcon.Visibility = Visibility.Visible;
            ShowStealthOnboardingIfNeeded(isStartup);
        }
        else
        {
            _settings.Stealth.Preset = "off";
            _settings.Stealth.SuppressOnSuccess = new();
            _trayIcon.Visibility = Visibility.Visible;
        }

        _settings.Save();
    }

    private void ShowStealthOnboardingIfNeeded(bool isStartup)
    {
        if (_settings is null || _notifications is null || _settings.Stealth.StealthOnboardingShown)
        {
            return;
        }

        if (isStartup)
        {
            MessageBox.Show(
                $"Stealth mode is now active.{Environment.NewLine}{Environment.NewLine}" +
                "Shutter is running invisibly. No widget, no tray icon." + Environment.NewLine + Environment.NewLine +
                $"- Toggle off: {_settings.Stealth.RuntimeToggleHotkey}{Environment.NewLine}" +
                $"- Quit: {_settings.Stealth.QuitHotkey}{Environment.NewLine}{Environment.NewLine}" +
                "This message will not appear again.",
                "Stealth Mode",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            _notifications.ShowStealthEnabled(_settings.Stealth.RuntimeToggleHotkey, _settings.Stealth.QuitHotkey);
        }

        _settings.Stealth.StealthOnboardingShown = true;
        _settings.Save();
    }

    private void StartAliveReminderTimer()
    {
        _aliveReminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _aliveReminderTimer.Tick += (_, _) =>
        {
            if (_settings is null || !string.Equals(_settings.Stealth.Preset, "personal", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var elapsed = DateTime.UtcNow - _lastInteractionUtc;
            if (elapsed.TotalMinutes < _settings.Stealth.AliveReminderAfterMinutes)
            {
                return;
            }

            FlashTaskbarReminder();
            _lastInteractionUtc = DateTime.UtcNow;
        };
        _aliveReminderTimer.Start();
    }

    private void MarkInteraction() => _lastInteractionUtc = DateTime.UtcNow;

    private void FlashTaskbarReminder()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_overlay!).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var fwi = new FLASHWINFO
        {
            cbSize = Convert.ToUInt32(Marshal.SizeOf<FLASHWINFO>()),
            hwnd = hwnd,
            dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
            uCount = 6,
            dwTimeout = 0
        };
        _ = FlashWindowEx(ref fwi);
    }

    private void WriteStealthAudit(string eventName, string filePath)
    {
        if (_settings is null || _stealthAuditLog is null || _recorderService is null)
        {
            return;
        }

        if (string.Equals(_settings.Stealth.Preset, "off", StringComparison.OrdinalIgnoreCase) || !_settings.Stealth.AuditLog)
        {
            return;
        }

        _stealthAuditLog.Write(
            eventName,
            filePath,
            _recorderService.LastSavedDuration,
            _recorderService.LastSavedPeakRms,
            _settings.Stealth.Preset,
            _stealthToggleSource);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _aliveReminderTimer?.Stop();
        _globalHotkeys?.Dispose();
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

    private void InitializeEventBusSubscriptions()
    {
        if (_eventBus == null || _notifications == null || _overlay == null) return;

        // we might subscribe multiple times if we don't clear old event bus but EventBus instance is new.
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
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
