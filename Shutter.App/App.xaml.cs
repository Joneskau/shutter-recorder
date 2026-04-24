using System.Windows;
using Shutter.Core;

namespace Shutter.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private RecorderService? _recorderService;
    private CaptureController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _hotkeyService = new HotkeyService();
        _recorderService = new RecorderService();
        _controller = new CaptureController(_hotkeyService, _recorderService);

        _controller.RecordingFinished += (s, path) =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Shutter", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        };

        _controller.ErrorOccurred += (s, ex) =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error: {ex.Message}", "Shutter", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        };

        // We need to wait for the window to be loaded to get its HWND for hotkey registration
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, ev) =>
        {
            if (s is MainWindow && _hotkeyService != null)
            {
                if (!_hotkeyService.Register())
                {
                    MessageBox.Show("Failed to register global hotkey (Ctrl+Alt+Space).", "Shutter", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Unregister();
        _recorderService?.Dispose();
        base.OnExit(e);
    }
}

