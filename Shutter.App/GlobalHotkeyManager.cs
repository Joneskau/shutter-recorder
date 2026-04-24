using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Shutter.App;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;

    private HwndSource? _source;
    private Window? _window;
    private readonly Dictionary<int, Action> _handlers = new();

    public bool Register(int id, string chord, Action handler)
    {
        EnsureWindow();
        if (!TryParseChord(chord, out var modifiers, out var key))
        {
            return false;
        }

        var registered = RegisterHotKey(_source!.Handle, id, modifiers, key);
        if (!registered)
        {
            return false;
        }

        _handlers[id] = handler;
        return true;
    }

    public void Unregister(int id)
    {
        if (_source is null)
        {
            return;
        }

        UnregisterHotKey(_source.Handle, id);
        _handlers.Remove(id);
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
            if (_handlers.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryParseChord(string chord, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        if (string.IsNullOrWhiteSpace(chord))
        {
            return false;
        }

        var parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0002;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0001;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0004;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0008;
            }
        }

        var keyToken = parts[^1];
        if (!Enum.TryParse<Key>(keyToken, true, out var parsedKey))
        {
            if (!Enum.TryParse<Key>($"D{keyToken}", true, out parsedKey))
            {
                return false;
            }
        }

        key = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
        return key != 0;
    }

    public void Dispose()
    {
        if (_source != null)
        {
            foreach (var id in _handlers.Keys)
            {
                UnregisterHotKey(_source.Handle, id);
            }
            _handlers.Clear();
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        _window = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
