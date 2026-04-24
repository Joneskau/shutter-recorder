using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Shutter.Core;

namespace Shutter.App;

public class HotkeyService : IHotkeyService, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 1;
    private const int PauseHotkeyId = 2;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private HwndSource? _source;
    private Window? _window;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? PauseHotkeyPressed;

    public HotkeyBinding Binding { get; private set; } = new()
    {
        Key = "R",
        Shift = true,
        Win = true
    };

    public bool Register() => Register(Binding);

    public bool Register(HotkeyBinding binding)
    {
        Binding = binding;
        EnsureWindow();
        return RegisterHotKey(_source!.Handle, HotkeyId, binding.Modifiers, binding.VirtualKey);
    }

    public bool ReRegister(HotkeyBinding binding)
    {
        Unregister();
        return Register(binding);
    }

    public bool RegisterPause(HotkeyBinding binding)
    {
        EnsureWindow();
        return RegisterHotKey(_source!.Handle, PauseHotkeyId, binding.Modifiers, binding.VirtualKey);
    }

    public bool ReRegisterPause(HotkeyBinding binding)
    {
        UnregisterPause();
        return RegisterPause(binding);
    }

    public void UnregisterPause()
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, PauseHotkeyId);
        }
    }

    public void Unregister()
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            UnregisterHotKey(_source.Handle, PauseHotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        _window = null;
    }

    private void EnsureWindow()
    {
        if (_source != null)
        {
            return;
        }

        _window = new Window { ShowInTaskbar = false, Visibility = Visibility.Hidden, WindowStyle = WindowStyle.None };
        var helper = new WindowInteropHelper(_window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            var id = wParam.ToInt32();
            if (id == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (id == PauseHotkeyId)
            {
                PauseHotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
