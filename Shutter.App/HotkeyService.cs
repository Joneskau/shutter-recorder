using System;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Shutter.Core;

namespace Shutter.App;

public class HotkeyService : IHotkeyService
{
    private const int HotkeyId = 1;
    private HwndSource? _hwndSource;
    public event EventHandler? HotkeyPressed;

    public bool Register()
    {
        var window = Application.Current.MainWindow;
        if (window == null) return false;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(HwndHook);

        // Registering Ctrl + Alt + Space
        return PInvoke.RegisterHotKey(
            (HWND)hwnd,
            HotkeyId,
            HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
            (uint)VIRTUAL_KEY.VK_SPACE);
    }

    public void Unregister()
    {
        var window = Application.Current.MainWindow;
        if (window == null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            PInvoke.UnregisterHotKey((HWND)hwnd, HotkeyId);
        }

        _hwndSource?.RemoveHook(HwndHook);
        _hwndSource = null;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
