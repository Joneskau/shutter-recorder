using System;

namespace Shutter.Core;

public interface IHotkeyService : IDisposable
{
    event EventHandler OnRecordStart;
    event EventHandler OnRecordStop;
    event EventHandler OnPauseToggle;

    HotkeyBinding Binding { get; }
    bool Register(HotkeyBinding binding);
    bool RegisterPause(HotkeyBinding binding);
    void Unregister();
}
