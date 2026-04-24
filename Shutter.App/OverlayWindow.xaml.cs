using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Shutter.App;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private DateTime _startTime;

    public event Action<Point>? PositionChanged;

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

    public void SetPosition(Point point)
    {
        Left = point.X;
        Top = point.Y;
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
        Dispatcher.InvokeAsync(() => LevelBar.Value = rms * 100);
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        PositionChanged?.Invoke(new Point(Left, Top));
    }
}
