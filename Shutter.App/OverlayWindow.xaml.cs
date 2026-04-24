using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Shutter.App;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    // Accumulated active recording time — frozen during pause.
    private TimeSpan _activeElapsed = TimeSpan.Zero;
    private DateTime _segmentStart;

    public event Action<Point>? PositionChanged;

    public OverlayWindow()
    {
        InitializeComponent();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (s, e) =>
        {
            var total = _activeElapsed + (DateTime.Now - _segmentStart);
            ElapsedText.Text = total.ToString(@"mm\:ss");
        };
    }

    public void SetPosition(Point point)
    {
        Left = point.X;
        Top = point.Y;
    }

    public void StartRecording()
    {
        _activeElapsed = TimeSpan.Zero;
        _segmentStart = DateTime.Now;
        ElapsedText.Text = "00:00";
        LevelBar.Value = 0;
        LevelBar.Opacity = 1.0;
        RecordingDot.Visibility = Visibility.Visible;
        PausedGlyph.Visibility = Visibility.Collapsed;
        _timer.Start();
        Show();
    }

    /// <summary>
    /// Freezes the elapsed timer and swaps to the yellow ⏸ indicator.
    /// The level bar stays visible but dims so the user can still confirm
    /// the mic is active and ready for resume.
    /// </summary>
    public void PauseRecording()
    {
        // Snapshot active time before freezing the timer tick.
        _activeElapsed += DateTime.Now - _segmentStart;
        _timer.Stop();

        RecordingDot.Visibility = Visibility.Collapsed;
        PausedGlyph.Visibility = Visibility.Visible;
        LevelBar.Opacity = 0.35;
    }

    /// <summary>
    /// Resumes the elapsed timer from where it was frozen and restores the red indicator.
    /// </summary>
    public void ResumeRecording()
    {
        _segmentStart = DateTime.Now;
        _timer.Start();

        RecordingDot.Visibility = Visibility.Visible;
        PausedGlyph.Visibility = Visibility.Collapsed;
        LevelBar.Opacity = 1.0;
    }

    public void StopRecording()
    {
        _timer.Stop();
        LevelBar.Opacity = 1.0;
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
