using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Shutter.Core;

namespace Shutter.App;

public class LowLevelKeyboardHookService : IHotkeyService
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;
    private HotkeyBinding _binding = new();
    private bool _isPressed;

    public event EventHandler? OnRecordStart;
    public event EventHandler? OnRecordStop;
    public event EventHandler? OnPauseToggle;

    public HotkeyBinding Binding => _binding;

    public LowLevelKeyboardHookService()
    {
        _proc = HookCallback;
    }

    public bool Register(HotkeyBinding binding)
    {
        _binding = binding;
        if (_hookId == IntPtr.Zero)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        return _hookId != IntPtr.Zero;
    }

    public bool RegisterPause(HotkeyBinding binding)
    {
        // Pause is disabled in Push-to-Talk mode
        return true;
    }

    public void Unregister()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                // Check logical modifiers
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0;

                if (Enum.TryParse<Key>(_binding.Key, true, out var targetKey))
                {
                    if (key == targetKey && ctrl == _binding.Ctrl && alt == _binding.Alt && shift == _binding.Shift && win == _binding.Win)
                    {
                        if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                        {
                            if (!_isPressed)
                            {
                                _isPressed = true;
                                OnRecordStart?.Invoke(this, EventArgs.Empty);
                            }
                        }
                        else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                        {
                            if (_isPressed)
                            {
                                _isPressed = false;
                                OnRecordStop?.Invoke(this, EventArgs.Empty);
                            }
                        }
                    }
                    else if (key == targetKey && (msg == WM_KEYUP || msg == WM_SYSKEYUP))
                    {
                        // Robust release: if the main key is released even if modifiers were let go first
                        if (_isPressed)
                        {
                            _isPressed = false;
                            OnRecordStop?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
}
