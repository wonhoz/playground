using PadForge.Models;

namespace PadForge.Services;

/// <summary>
/// user32.dll SendInput P/Invoke 기반 가상 키보드/마우스 입력 전송
/// </summary>
public class VirtualInputService
{
    #region P/Invoke

    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP       = 0x0002;
    private const uint KEYEVENTF_UNICODE     = 0x0004;
    private const uint KEYEVENTF_SCANCODE    = 0x0008;

    private const uint MOUSEEVENTF_MOVE        = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    private const uint MOUSEEVENTF_WHEEL       = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL      = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(4)] public MOUSEINPUT mi;
        [FieldOffset(4)] public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short VkKeyScanExW(char ch, IntPtr dwhkl);

    #endregion

    // 현재 눌린 상태 추적 (키 업 전송용)
    private readonly HashSet<GamepadInput> _pressedInputs = [];
    private readonly object _lock = new();

    /// <summary>매핑 동작 실행 (버튼 눌림 이벤트)</summary>
    public void ExecuteDown(MappingAction action)
    {
        switch (action.Type)
        {
            case ActionType.KeyPress:
                SendKeyDown(ParseVk(action.KeyCode));
                break;
            case ActionType.KeySequence:
                foreach (var key in action.KeySequence)
                    SendKeyTap(ParseVk(key));
                break;
            case ActionType.MouseButton:
                SendMouseDown((Models.MouseButton)action.Mouse);
                break;
            case ActionType.MouseScroll:
                SendMouseScroll(action.ScrollDir, action.ScrollAmount);
                break;
            case ActionType.TextType:
                if (!string.IsNullOrEmpty(action.Text))
                    TypeText(action.Text);
                break;
            case ActionType.OpenApp:
                if (!string.IsNullOrEmpty(action.AppPath) && File.Exists(action.AppPath))
                    System.Diagnostics.Process.Start(action.AppPath);
                break;
        }
    }

    /// <summary>매핑 동작 해제 (버튼 떼기 이벤트)</summary>
    public void ExecuteUp(MappingAction action)
    {
        switch (action.Type)
        {
            case ActionType.KeyPress:
                SendKeyUp(ParseVk(action.KeyCode));
                break;
            case ActionType.MouseButton:
                SendMouseUp((Models.MouseButton)action.Mouse);
                break;
        }
    }

    private static void SendKeyDown(ushort vk)
    {
        var inp = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki   = new KEYBDINPUT { wVk = vk, dwFlags = 0 }
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyUp(ushort vk)
    {
        var inp = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki   = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP }
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyTap(ushort vk)
    {
        INPUT[] inputs =
        [
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk } },
            new() { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
        ];
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseDown(Models.MouseButton btn)
    {
        uint flag = btn switch
        {
            Models.MouseButton.Left   => MOUSEEVENTF_LEFTDOWN,
            Models.MouseButton.Right  => MOUSEEVENTF_RIGHTDOWN,
            Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
            _ => 0
        };
        if (flag == 0) return;
        var inp = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flag } };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseUp(Models.MouseButton btn)
    {
        uint flag = btn switch
        {
            Models.MouseButton.Left   => MOUSEEVENTF_LEFTUP,
            Models.MouseButton.Right  => MOUSEEVENTF_RIGHTUP,
            Models.MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        if (flag == 0) return;
        var inp = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flag } };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseScroll(ScrollDirection dir, int amount)
    {
        bool horizontal = dir is ScrollDirection.Left or ScrollDirection.Right;
        int delta = (dir is ScrollDirection.Up or ScrollDirection.Right ? 1 : -1) * amount * 120;

        var inp = new INPUT
        {
            type = INPUT_MOUSE,
            mi   = new MOUSEINPUT
            {
                dwFlags   = horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL,
                mouseData = (uint)delta
            }
        };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private static void TypeText(string text)
    {
        var inputs = new List<INPUT>();
        foreach (char c in text)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki   = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE }
            });
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki   = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP }
            });
        }
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    /// <summary>VK 코드 문자열 파싱 ("VK_RETURN" → 0x0D, "A" → 0x41 등)</summary>
    private static ushort ParseVk(string? keyCode)
    {
        if (string.IsNullOrEmpty(keyCode)) return 0;

        // 이름으로 파싱
        if (keyCode.StartsWith("VK_", StringComparison.OrdinalIgnoreCase))
        {
            var name = keyCode[3..].ToUpperInvariant();
            return name switch
            {
                "RETURN" or "ENTER" => 0x0D,
                "ESCAPE" or "ESC"   => 0x1B,
                "SPACE"             => 0x20,
                "BACK"              => 0x08,
                "TAB"               => 0x09,
                "DELETE"            => 0x2E,
                "INSERT"            => 0x2D,
                "HOME"              => 0x24,
                "END"               => 0x23,
                "PRIOR" or "PAGEUP" => 0x21,
                "NEXT" or "PAGEDOWN"=> 0x22,
                "LEFT"              => 0x25,
                "UP"                => 0x26,
                "RIGHT"             => 0x27,
                "DOWN"              => 0x28,
                "F1"                => 0x70,
                "F2"                => 0x71,
                "F3"                => 0x72,
                "F4"                => 0x73,
                "F5"                => 0x74,
                "F6"                => 0x75,
                "F7"                => 0x76,
                "F8"                => 0x77,
                "F9"                => 0x78,
                "F10"               => 0x79,
                "F11"               => 0x7A,
                "F12"               => 0x7B,
                "CONTROL" or "CTRL" => 0x11,
                "MENU" or "ALT"     => 0x12,
                "SHIFT"             => 0x10,
                "LWIN"              => 0x5B,
                "RWIN"              => 0x5C,
                _ => 0
            };
        }

        // 단일 문자
        if (keyCode.Length == 1)
        {
            char c = char.ToUpperInvariant(keyCode[0]);
            if (c >= 'A' && c <= 'Z') return (ushort)c;
            if (c >= '0' && c <= '9') return (ushort)c;
        }

        return 0;
    }
}
