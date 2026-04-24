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
<<<<<<< ours
    public StealthSettings StealthConfig { get; private set; }

    public MainWindow(HotkeyBinding hotkey, IReadOnlyList<InputDeviceOption> devices, string? selectedDeviceId, StealthSettings stealthConfig)
=======
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
>>>>>>> theirs
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

<<<<<<< ours
        StealthConfig = new StealthSettings 
        {
            Preset = stealthConfig.Preset,
            RuntimeToggleHotkey = stealthConfig.RuntimeToggleHotkey,
            QuitHotkey = stealthConfig.QuitHotkey,
            AliveReminderAfterMinutes = stealthConfig.AliveReminderAfterMinutes,
            AliveReminderStyle = stealthConfig.AliveReminderStyle,
            FilenameStyle = stealthConfig.FilenameStyle,
            AutoHideOnScreenShare = stealthConfig.AutoHideOnScreenShare,
            AuditLog = stealthConfig.AuditLog,
            SuppressOnSuccess = stealthConfig.SuppressOnSuccess.ToList(),
            NeverSuppress = stealthConfig.NeverSuppress.ToList()
        };

        foreach(var item in StealthPresetCombo.Items) {
            if (((System.Windows.Controls.ComboBoxItem)item).Content.ToString() == StealthConfig.Preset) {
                StealthPresetCombo.SelectedItem = item;
                break;
            }
        }
        
        StealthToggleKeyCombo.ItemsSource = allowedKeys;
        StealthQuitKeyCombo.ItemsSource = allowedKeys;
        
        var tHotkey = HotkeyBinding.Parse(StealthConfig.RuntimeToggleHotkey);
        StealthToggleWinCheck.IsChecked = tHotkey.Win;
        StealthToggleCtrlCheck.IsChecked = tHotkey.Ctrl;
        StealthToggleAltCheck.IsChecked = tHotkey.Alt;
        StealthToggleShiftCheck.IsChecked = tHotkey.Shift;
        StealthToggleKeyCombo.SelectedItem = tHotkey.Key;

        var qHotkey = HotkeyBinding.Parse(StealthConfig.QuitHotkey);
        StealthQuitWinCheck.IsChecked = qHotkey.Win;
        StealthQuitCtrlCheck.IsChecked = qHotkey.Ctrl;
        StealthQuitAltCheck.IsChecked = qHotkey.Alt;
        StealthQuitShiftCheck.IsChecked = qHotkey.Shift;
        StealthQuitKeyCombo.SelectedItem = qHotkey.Key;
        
        StealthAuditLogCheck.IsChecked = StealthConfig.AuditLog;
        StealthAutoHideCheck.IsChecked = StealthConfig.AutoHideOnScreenShare;
=======
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
>>>>>>> theirs
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

        var tKey = StealthToggleKeyCombo.SelectedItem as string ?? "S";
        var tHotkey = new HotkeyBinding
        {
            Key = tKey,
            Win = StealthToggleWinCheck.IsChecked == true,
            Ctrl = StealthToggleCtrlCheck.IsChecked == true,
            Alt = StealthToggleAltCheck.IsChecked == true,
            Shift = StealthToggleShiftCheck.IsChecked == true
        };
        StealthConfig.RuntimeToggleHotkey = tHotkey.ToString();
        
        var qKey = StealthQuitKeyCombo.SelectedItem as string ?? "Q";
        var qHotkey = new HotkeyBinding
        {
            Key = qKey,
            Win = StealthQuitWinCheck.IsChecked == true,
            Ctrl = StealthQuitCtrlCheck.IsChecked == true,
            Alt = StealthQuitAltCheck.IsChecked == true,
            Shift = StealthQuitShiftCheck.IsChecked == true
        };
        StealthConfig.QuitHotkey = qHotkey.ToString();
        
        StealthConfig.AuditLog = StealthAuditLogCheck.IsChecked == true;
        StealthConfig.AutoHideOnScreenShare = StealthAutoHideCheck.IsChecked == true;
        
        if (StealthPresetCombo.SelectedItem is System.Windows.Controls.ComboBoxItem presetItem)
        {
            StealthConfig.Preset = presetItem.Content.ToString()!;
            
            if (StealthConfig.Preset == "personal")
            {
                StealthConfig.SuppressOnSuccess = new List<string> { "widget", "savedToast", "trayIcon" };
            }
            else
            {
                StealthConfig.SuppressOnSuccess = new List<string>();
            }
        }

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
