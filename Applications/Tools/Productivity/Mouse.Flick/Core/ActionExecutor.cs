using System.Runtime.InteropServices;

namespace MouseFlick.Core;

/// <summary>
/// "Alt+Left", "Ctrl+W", "F5" 형태의 키 조합 문자열을 SendInput으로 전송
/// </summary>
internal static class ActionExecutor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint       type;
        public KEYBDINPUT ki;
        // 구조체 크기 맞추기 (union: MOUSEINPUT/HARDWAREINPUT 중 가장 큰 것)
        private IntPtr    _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD   = 1;
    private const uint KEYEVENTF_KEYUP  = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // 수식자 키 별칭 → VK 코드
    private static readonly Dictionary<string, ushort> _modifiers
        = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ctrl",    0x11 }, { "control", 0x11 },
        { "alt",     0x12 }, { "menu",    0x12 },
        { "shift",   0x10 },
        { "win",     0x5B }, { "lwin",    0x5B },
    };

    public static void Execute(string keyCombo)
    {
        if (string.IsNullOrWhiteSpace(keyCombo)) return;

        var parts = keyCombo.Split('+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var vks = new List<ushort>();
        foreach (var part in parts)
        {
            if (_modifiers.TryGetValue(part, out var modVk))
                vks.Add(modVk);
            else
            {
                var vk = ResolveKey(part);
                if (vk != 0) vks.Add(vk);
            }
        }

        if (vks.Count == 0) return;

        var inputs = new List<INPUT>();

        // KeyDown (수식자 먼저)
        foreach (var vk in vks)
            inputs.Add(MakeKeyInput(vk, 0));

        // KeyUp (역순)
        for (int i = vks.Count - 1; i >= 0; i--)
            inputs.Add(MakeKeyInput(vks[i], KEYEVENTF_KEYUP));

        var arr = inputs.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKeyInput(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        ki   = new KEYBDINPUT { wVk = vk, dwFlags = flags }
    };

    private static ushort ResolveKey(string key)
    {
        // Keys enum 값 = VK 코드 (대부분 일치)
        if (Enum.TryParse<Keys>(key, true, out var k) && k != Keys.None)
            return (ushort)(k & Keys.KeyCode);

        // 별칭 처리
        return key.ToLowerInvariant() switch
        {
            "esc"     => (ushort)Keys.Escape,
            "escape"  => (ushort)Keys.Escape,
            "del"     => (ushort)Keys.Delete,
            "delete"  => (ushort)Keys.Delete,
            "ins"     => (ushort)Keys.Insert,
            "insert"  => (ushort)Keys.Insert,
            "pgup"    => (ushort)Keys.PageUp,
            "pgdn"    => (ushort)Keys.PageDown,
            "back"    => (ushort)Keys.Back,
            "bs"      => (ushort)Keys.Back,
            "enter"   => (ushort)Keys.Return,
            "ret"     => (ushort)Keys.Return,
            "tab"     => (ushort)Keys.Tab,
            "space"   => (ushort)Keys.Space,
            "minus"   => 0xBD,  // VK_OEM_MINUS
            "plus"    => 0xBB,  // VK_OEM_PLUS
            "comma"   => 0xBC,  // VK_OEM_COMMA
            "period"  => 0xBE,  // VK_OEM_PERIOD
            _         => 0
        };
    }
}
