using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Shutter.Core;

namespace Shutter.App;

public partial class MainWindow : Window
{
    private readonly IReadOnlyList<InputDeviceOption> _devices;

    public HotkeyBinding SelectedHotkey { get; private set; }
    public string? SelectedDeviceId { get; private set; }
    public string SelectedOutputFormat { get; private set; } = "wav";
    public string SelectedQuality { get; private set; } = "standard";
    public string SelectedStealthPreset { get; private set; } = "off";
    public string SelectedFilenameStyle { get; private set; } = "timestamp";

    public MainWindow(
        HotkeyBinding hotkey,
        IReadOnlyList<InputDeviceOption> devices,
        string? selectedDeviceId,
        string outputFormat,
        string quality,
        string stealthPreset,
        string filenameStyle)
    {
        InitializeComponent();

        _devices = devices;
        DeviceCombo.ItemsSource = _devices;

        var allowedKeys = Enum.GetValues<Key>()
            .Where(k => (k >= Key.A && k <= Key.Z) || (k >= Key.D0 && k <= Key.D9) || (k >= Key.F1 && k <= Key.F12))
            .Select(k => k.ToString())
            .ToList();

        KeyCombo.ItemsSource = allowedKeys;

        WinCheck.IsChecked = hotkey.Win;
        CtrlCheck.IsChecked = hotkey.Ctrl;
        AltCheck.IsChecked = hotkey.Alt;
        ShiftCheck.IsChecked = hotkey.Shift;
        KeyCombo.SelectedItem = hotkey.Key;

        SelectedHotkey = hotkey;
        SelectedDeviceId = selectedDeviceId;
        DeviceCombo.SelectedValue = selectedDeviceId;

        OutputFormatCombo.ItemsSource = new[] { "wav", "mp3", "opus" };
        QualityCombo.ItemsSource = new[] { "low", "standard", "high" };
        StealthPresetCombo.ItemsSource = new[] { "off", "personal", "meeting" };
        FilenameStyleCombo.ItemsSource = new[] { "timestamp", "random" };

        OutputFormatCombo.SelectedItem = outputFormat.ToLowerInvariant();
        QualityCombo.SelectedItem = quality.ToLowerInvariant();
        StealthPresetCombo.SelectedItem = stealthPreset.ToLowerInvariant();
        FilenameStyleCombo.SelectedItem = filenameStyle.ToLowerInvariant();

        SelectedOutputFormat = outputFormat.ToLowerInvariant();
        SelectedQuality = quality.ToLowerInvariant();
        SelectedStealthPreset = stealthPreset.ToLowerInvariant();
        SelectedFilenameStyle = filenameStyle.ToLowerInvariant();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (KeyCombo.SelectedItem is not string key)
        {
            MessageBox.Show("Please choose a key.", "Shutter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (WinCheck.IsChecked != true && CtrlCheck.IsChecked != true && AltCheck.IsChecked != true && ShiftCheck.IsChecked != true)
        {
            MessageBox.Show("Please select at least one modifier key.", "Shutter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedHotkey = new HotkeyBinding
        {
            Key = key,
            Win = WinCheck.IsChecked == true,
            Ctrl = CtrlCheck.IsChecked == true,
            Alt = AltCheck.IsChecked == true,
            Shift = ShiftCheck.IsChecked == true
        };

        SelectedDeviceId = DeviceCombo.SelectedValue as string;
        SelectedOutputFormat = OutputFormatCombo.SelectedItem as string ?? "wav";
        SelectedQuality = QualityCombo.SelectedItem as string ?? "standard";
        SelectedStealthPreset = StealthPresetCombo.SelectedItem as string ?? "off";
        SelectedFilenameStyle = FilenameStyleCombo.SelectedItem as string ?? "timestamp";
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
