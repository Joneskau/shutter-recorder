using System;

namespace Shutter.Core;

public interface IHotkeyService
{
    event EventHandler HotkeyPressed;
    event EventHandler PauseHotkeyPressed;
    bool Register();
    void Unregister();
}
