using System;
using System.Collections.Generic;
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
    private readonly IRecordingHistoryService _history;
    private readonly AppSettings _settings;

    // Display wrapper — built on window open so File.Exists runs fresh each time.
    private sealed class RecordingEntryViewModel
    {
        private readonly RecordingEntry _e;
        public RecordingEntry Entry => _e;

        public RecordingEntryViewModel(RecordingEntry e)
        {
            _e = e;
            FileExists = File.Exists(e.Path);
        }

        public bool   FileExists  { get; }
        public string FileName    => _e.FileName;
        public string Duration    => _e.Duration;
        public string Path        => _e.Path;

        public string StatusGlyph =>
            _e.WasSilent ? "⚠" : (!FileExists ? "✕" : "");
        public string StatusTooltip =>
            _e.WasSilent ? "Silent recording" : (!FileExists ? "File not found" : "OK");
        public string StatusColor =>
            _e.WasSilent ? "#FFD700" : (!FileExists ? "#FF6B6B" : "Transparent");
        public double PathOpacity  => FileExists ? 1.0 : 0.4;

        public string FormattedDate =>
            _e.RecordedAt.LocalDateTime.ToString("d MMM, HH:mm");

        public string FormattedSize => _e.SizeBytes switch
        {
            >= 1_000_000_000 => $"{_e.SizeBytes / 1_000_000_000.0:F1} GB",
            >= 1_000_000     => $"{_e.SizeBytes / 1_000_000.0:F1} MB",
            >= 1_000         => $"{_e.SizeBytes / 1_000.0:F1} KB",
            _                => $"{_e.SizeBytes} B"
        };
    }

    public HistoryWindow(IRecordingHistoryService history, AppSettings settings)
    {
        _history  = history;
        _settings = settings;
        InitializeComponent();

        Left   = settings.HistoryWindowLeft;
        Top    = settings.HistoryWindowTop;
        Width  = settings.HistoryWindowWidth;
        Height = settings.HistoryWindowHeight;

        Activated += (_, _) => RefreshGrid();
        Closing   += OnWindowClosing;
    }

    // ── Grid management ──────────────────────────────────────────────────────

    private void RefreshGrid()
    {
        var vms = _history.Entries
            .Select(e => new RecordingEntryViewModel(e))
            .ToList();

        HistoryGrid.ItemsSource = vms;
        EmptyState.Visibility   = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryGrid.Visibility  = vms.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CountText.Text          = vms.Count > 0 ? $"{vms.Count} recording{(vms.Count == 1 ? "" : "s")}" : "";
    }

    private RecordingEntryViewModel? SelectedVm =>
        HistoryGrid.SelectedItem as RecordingEntryViewModel;

    // ── Mouse interactions ───────────────────────────────────────────────────

    private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SelectedVm is { } vm)
        {
            Clipboard.SetText(vm.Path);
            StatusText.Text = $"Copied: {vm.Path}";
        }
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedVm is not { } vm) return;
        OpenFile(vm);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVm is { } vm)
        {
            Clipboard.SetText(vm.Path);
            StatusText.Text = $"Copied: {vm.Path}";
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVm is { } vm) OpenFile(vm);
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVm is not { } vm) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vm.Path}\"")
        {
            UseShellExecute = true
        });
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVm is not { } vm) return;
        _history.Remove(vm.Entry);
        RefreshGrid();
        StatusText.Text = $"Removed: {vm.FileName}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OpenFile(RecordingEntryViewModel vm)
    {
        if (!vm.FileExists)
        {
            MessageBox.Show($"File not found:\n{vm.Path}", "Shutter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(vm.Path) { UseShellExecute = true });
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.HistoryWindowLeft   = Left;
        _settings.HistoryWindowTop    = Top;
        _settings.HistoryWindowWidth  = Width;
        _settings.HistoryWindowHeight = Height;
        _settings.Save();
    }
}
