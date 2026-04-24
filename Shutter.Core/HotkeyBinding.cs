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
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(Key.ToUpperInvariant());
        return string.Join("+", parts);
    }

    public static HotkeyBinding Parse(string s)
    {
        var parts = s.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var hb = new HotkeyBinding { Key = "S" }; // fallback
        foreach (var p in parts)
        {
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) hb.Ctrl = true;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) hb.Alt = true;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) hb.Shift = true;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) hb.Win = true;
            else hb.Key = p;
        }
        return hb;
    }
}
