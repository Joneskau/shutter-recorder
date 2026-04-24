using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shutter.Core;

namespace Shutter.App;

public partial class HistoryWindow : Window
{
    private readonly IRecordingHistoryService _historyService;
    private readonly ObservableCollection<HistoryItemViewModel> _items = new();

    public event Action<Point>? PositionChanged;

    public HistoryWindow(IRecordingHistoryService historyService)
    {
        InitializeComponent();
        _historyService = historyService;
        HistoryList.ItemsSource = _items;

        RefreshItems();
        _historyService.Entries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // For simplicity on v2, just refresh the whole list when the collection changes
        Dispatcher.InvokeAsync(RefreshItems);
    }

    private void RefreshItems()
    {
        _items.Clear();
        foreach (var entry in _historyService.Entries)
        {
            _items.Add(new HistoryItemViewModel(entry));
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Refresh file existence checks when window is focused
        foreach (var item in _items)
        {
            item.RefreshExists();
        }
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedItem() is { Exists: true } item)
        {
            Process.Start(new ProcessStartInfo(item.Entry.Path) { UseShellExecute = true });
        }
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GetSelectedItem() is { } item)
        {
            Clipboard.SetText(item.Entry.Path);
        }
    }

    private HistoryItemViewModel? GetSelectedItem()
    {
        return HistoryList.SelectedItem as HistoryItemViewModel;
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is { } item)
        {
            Clipboard.SetText(item.Entry.Path);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is { Exists: true } item)
        {
            Process.Start(new ProcessStartInfo(item.Entry.Path) { UseShellExecute = true });
        }
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is { Exists: true } item)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Entry.Path}\"") { UseShellExecute = true });
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is { } item)
        {
            // Remove from history
            _historyService.Remove(item.Entry);

            // Try delete file
            if (item.Exists)
            {
                try
                {
                    File.Delete(item.Entry.Path);
                }
                catch
                {
                    // Ignore delete errors for now
                }
            }
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        PositionChanged?.Invoke(new Point(Left, Top));
    }

    // Hide instead of close
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

public class HistoryItemViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public RecordingEntry Entry { get; }

    public HistoryItemViewModel(RecordingEntry entry)
    {
        Entry = entry;
        RefreshExists();
    }

    private bool _exists;
    public bool Exists
    {
        get => _exists;
        set
        {
            if (_exists != value)
            {
                _exists = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Exists)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MissingVisibility)));
            }
        }
    }

    public void RefreshExists()
    {
        Exists = File.Exists(Entry.Path);
    }

    public Visibility MissingVisibility => Exists ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SilenceVisibility => Entry.WasSilent ? Visibility.Visible : Visibility.Collapsed;

    public string FriendlyTimestamp => Entry.RecordedAt.ToString("dd MMM, HH:mm");
    public string FormattedDuration => $"{(int)Entry.Duration.TotalMinutes}m {Entry.Duration.Seconds}s";
    
    public string FormattedSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = Entry.SizeBytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public string TruncatedPath
    {
        get
        {
            var p = Entry.Path;
            if (p.Length > 50) return "..." + p.Substring(p.Length - 47);
            return p;
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
