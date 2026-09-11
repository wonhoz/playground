namespace KeyMacro.Models;

/// <summary>가상 키 코드 → 사용자에게 보여줄 키 이름 변환</summary>
public static class KeyNames
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab",     [0x0D] = "Enter",   [0x13] = "Pause",
        [0x14] = "CapsLock",  [0x1B] = "Esc",     [0x20] = "Space",   [0x21] = "PageUp",
        [0x22] = "PageDown",  [0x23] = "End",     [0x24] = "Home",
        [0x25] = "←",         [0x26] = "↑",       [0x27] = "→",       [0x28] = "↓",
        [0x2C] = "PrintScreen", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x5B] = "LWin",      [0x5C] = "RWin",    [0x5D] = "Menu",
        [0x90] = "NumLock",   [0x91] = "ScrollLock",
        [0xA0] = "LShift",    [0xA1] = "RShift",  [0xA2] = "LCtrl",   [0xA3] = "RCtrl",
        [0xA4] = "LAlt",      [0xA5] = "RAlt",
        [0xBA] = ";",  [0xBB] = "=",  [0xBC] = ",",  [0xBD] = "-",
        [0xBE] = ".",  [0xBF] = "/",  [0xC0] = "`",  [0xDB] = "[",
        [0xDC] = "\\", [0xDD] = "]",  [0xDE] = "'",
        [0x6A] = "숫자패드 *", [0x6B] = "숫자패드 +", [0x6D] = "숫자패드 -",
        [0x6E] = "숫자패드 .", [0x6F] = "숫자패드 /",
        [0x15] = "한/영",  [0x19] = "한자"
    };

    /// <summary>단일 키 이름</summary>
    public static string Of(int vk)
    {
        if (vk == 0) return "(없음)";
        if (Map.TryGetValue(vk, out var name)) return name;
        if (vk is >= 0x30 and <= 0x39) return ((char)vk).ToString();               // 0~9
        if (vk is >= 0x41 and <= 0x5A) return ((char)vk).ToString();               // A~Z
        if (vk is >= 0x60 and <= 0x69) return $"숫자패드 {vk - 0x60}";             // NumPad0~9
        if (vk is >= 0x70 and <= 0x87) return $"F{vk - 0x6F}";                     // F1~F24
        return $"VK{vk:X2}";
    }

    /// <summary>수식 키를 포함한 조합 표기 (예: Ctrl + Shift + A)</summary>
    public static string Combo(int vk, bool ctrl, bool alt, bool shift, bool win)
    {
        var parts = new List<string>(5);
        if (ctrl)  parts.Add("Ctrl");
        if (alt)   parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win)   parts.Add("Win");
        parts.Add(Of(vk));
        return string.Join(" + ", parts);
    }

    /// <summary>해당 vk 자체가 수식 키인지</summary>
    public static bool IsModifier(int vk) =>
        vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
}
