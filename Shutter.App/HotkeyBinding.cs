using System.Windows.Input;

namespace Shutter.App;

public sealed class HotkeyBinding
{
    public string Key { get; set; } = "R";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }

    public uint Modifiers
    {
        get
        {
            uint value = 0;
            if (Alt) value |= HotkeyService.ModAlt;
            if (Ctrl) value |= HotkeyService.ModControl;
            if (Shift) value |= HotkeyService.ModShift;
            if (Win) value |= HotkeyService.ModWin;
            return value;
        }
    }

    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Enum.TryParse<Key>(Key, true, out var key) ? key : Key.R);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Win) parts.Add("Win");
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key.ToUpperInvariant());
        return string.Join(" + ", parts);
    }
}
