using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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
    private bool _isRecording;

    public event EventHandler? OnRecordStart;
    public event EventHandler? OnRecordStop;
    public event EventHandler? OnPauseToggle;

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
        return RegisterHotKey(_source!.Handle, HotkeyId, GetModifiers(binding), GetVirtualKey(binding));
    }

    public bool ReRegister(HotkeyBinding binding)
    {
        Unregister();
        return Register(binding);
    }

    public bool RegisterPause(HotkeyBinding binding)
    {
        EnsureWindow();
        return RegisterHotKey(_source!.Handle, PauseHotkeyId, GetModifiers(binding), GetVirtualKey(binding));
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
                if (!_isRecording)
                {
                    _isRecording = true;
                    OnRecordStart?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _isRecording = false;
                    OnRecordStop?.Invoke(this, EventArgs.Empty);
                }
                handled = true;
            }
            else if (id == PauseHotkeyId)
            {
                OnPauseToggle?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();

    private uint GetModifiers(HotkeyBinding binding)
    {
        uint value = 0;
        if (binding.Alt) value |= ModAlt;
        if (binding.Ctrl) value |= ModControl;
        if (binding.Shift) value |= ModShift;
        if (binding.Win) value |= ModWin;
        return value;
    }

    private uint GetVirtualKey(HotkeyBinding binding)
    {
        var key = Enum.TryParse<System.Windows.Input.Key>(binding.Key, true, out var k) ? k : System.Windows.Input.Key.R;
        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
