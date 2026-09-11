using KeyMacro.Models;
using static KeyMacro.Services.NativeMethods;

namespace KeyMacro.Services;

/// <summary>SendInput 으로 마우스 · 키보드 입력을 합성한다</summary>
public static class InputSender
{
    /// <summary>이 앱이 만들어낸 입력임을 표시하는 서명 — 훅에서 자기 입력을 걸러내는 데 사용</summary>
    public static readonly IntPtr Signature = new(0x4B4D4143); // "KMAC"

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    /// <summary>확장 키 플래그가 필요한 키 (방향키 · 편집키 · 우측 수식키 등)</summary>
    private static bool IsExtended(int vk) =>
        vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
           or 0x2D or 0x2E or 0x2C or 0x90 or 0xA3 or 0xA5 or 0x5B or 0x5C or 0x5D or 0x6F;

    // ── 마우스 ────────────────────────────────────────────────────────
    /// <summary>가상 화면 절대 좌표(물리 픽셀)로 커서 이동</summary>
    public static void MoveTo(int x, int y)
    {
        int left   = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top    = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width  = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
        int height = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));

        // 0~65535 정규화 좌표로 변환 (+0.5 픽셀 보정으로 반올림 오차 방지)
        int nx = (int)Math.Round((x - left + 0.5) * 65535.0 / width);
        int ny = (int)Math.Round((y - top  + 0.5) * 65535.0 / height);

        Send(new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = Math.Clamp(nx, 0, 65535),
                    dy = Math.Clamp(ny, 0, 65535),
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                    dwExtraInfo = Signature
                }
            }
        });
    }

    /// <summary>지정 좌표로 이동 후 클릭</summary>
    public static void Click(int x, int y, ClickButton button, int count)
    {
        MoveTo(x, y);
        (uint down, uint up) = button switch
        {
            ClickButton.Right  => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            ClickButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _                  => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };

        for (int i = 0; i < Math.Max(1, count); i++)
        {
            Send(MouseButtonInput(down), MouseButtonInput(up));
            if (i < count - 1) Thread.Sleep(40);   // 더블클릭 인식 간격
        }
    }

    private static INPUT MouseButtonInput(uint flag) => new()
    {
        type = INPUT_MOUSE,
        u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flag, dwExtraInfo = Signature } }
    };

    // ── 키보드 ────────────────────────────────────────────────────────
    public static void KeyDown(int vk) => Send(KeyInput(vk, false));
    public static void KeyUp(int vk)   => Send(KeyInput(vk, true));

    /// <summary>키 한 번 누르고 떼기</summary>
    public static void KeyPress(int vk)
    {
        Send(KeyInput(vk, false));
        Thread.Sleep(15);
        Send(KeyInput(vk, true));
    }

    private static INPUT KeyInput(int vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (IsExtended(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = flags, dwExtraInfo = Signature }
            }
        };
    }

    /// <summary>문자열을 유니코드 입력으로 전송 (한글 · 이모지 포함)</summary>
    public static void SendText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\n') { KeyPress(0x0D); continue; }   // 개행은 Enter 로
            if (ch == '\r') continue;

            Send(UnicodeInput(ch, false), UnicodeInput(ch, true));
            Thread.Sleep(5);
        }
    }

    private static INPUT UnicodeInput(char ch, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0),
                dwExtraInfo = Signature
            }
        }
    };

    // ── 수식 키 상태 정리 ─────────────────────────────────────────────
    /// <summary>사용자가 물리적으로 누르고 있는 수식 키를 떼어 매크로 입력 간섭을 막는다</summary>
    public static void ReleaseHeldModifiers()
    {
        foreach (var vk in new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            if ((GetAsyncKeyState(vk) & 0x8000) != 0) Send(KeyInput(vk, true));
    }

    private static void Send(params INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, InputSize);
}
