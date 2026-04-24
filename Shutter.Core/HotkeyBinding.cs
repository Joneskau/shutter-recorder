using System;
using System.Collections.Generic;

namespace Shutter.Core;

public sealed class HotkeyBinding
{
    public string Key { get; set; } = "R";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }

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
