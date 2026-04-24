using System;

namespace Shutter.Core;

public interface IHotkeyService
{
    event EventHandler HotkeyPressed;
    bool Register();
    void Unregister();
}
