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
    public StealthSettings StealthConfig { get; private set; }

    public MainWindow(HotkeyBinding hotkey, IReadOnlyList<InputDeviceOption> devices, string? selectedDeviceId, StealthSettings stealthConfig)
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
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
