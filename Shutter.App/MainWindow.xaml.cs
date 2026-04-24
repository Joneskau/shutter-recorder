using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Shutter.App;

public partial class MainWindow : Window
{
    private readonly IReadOnlyList<InputDeviceOption> _devices;

    public HotkeyBinding SelectedHotkey { get; private set; }
    public string? SelectedDeviceId { get; private set; }

    public MainWindow(HotkeyBinding hotkey, IReadOnlyList<InputDeviceOption> devices, string? selectedDeviceId)
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
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
