using System;
using System.Windows;
using System.Windows.Threading;

namespace Shutter.App;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private DateTime _startTime;

    public OverlayWindow()
    {
        InitializeComponent();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (s, e) =>
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedText.Text = elapsed.ToString(@"mm\:ss");
        };
    }

    public void StartRecording()
    {
        _startTime = DateTime.Now;
        ElapsedText.Text = "00:00";
        LevelBar.Value = 0;
        _timer.Start();
        Show();
    }

    public void StopRecording()
    {
        _timer.Stop();
        Hide();
    }

    public void UpdateLevel(float rms)
    {
        // rms is 0.0–1.0; scale to 0–100 for the progress bar
        Dispatcher.InvokeAsync(() => LevelBar.Value = rms * 100);
    }
}
