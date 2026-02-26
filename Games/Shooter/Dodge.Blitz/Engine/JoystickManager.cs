using System.Runtime.InteropServices;

namespace DodgeBlitz.Engine;

/// <summary>
/// XInput(xinput1_4.dll) 기반 게임패드 입력 관리자.
/// 첫 번째 컨트롤러(index 0)만 지원. DLL 부재 시 graceful 무시.
/// </summary>
public sealed class JoystickManager
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte   LeftTrigger;
        public byte   RightTrigger;
        public short  ThumbLX;
        public short  ThumbLY;
        public short  ThumbRX;
        public short  ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint         PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    // 버튼 비트마스크
    private const ushort DpadUp    = 0x0001;
    private const ushort DpadDown  = 0x0002;
    private const ushort DpadLeft  = 0x0004;
    private const ushort DpadRight = 0x0008;
    private const ushort BtnStart  = 0x0010;
    private const ushort BtnBack   = 0x0020;
    private const ushort BtnA      = 0x1000;
    private const ushort BtnB      = 0x2000;

    // 아날로그 스틱 데드존 (~25% of 32767)
    private const short Deadzone = 8192;

    private bool _available = true;  // false = xinput1_4.dll 없음
    private bool _prevStart, _prevBack;

    public bool IsConnected { get; private set; }

    // 방향 (왼쪽 스틱 + D-Pad 통합)
    public bool Left  { get; private set; }
    public bool Right { get; private set; }
    public bool Up    { get; private set; }
    public bool Down  { get; private set; }

    // 버튼 (A/Start = 시작, B/Back = 뒤로)
    public bool Start { get; private set; }
    public bool Back  { get; private set; }

    /// <summary>이번 Poll에서 처음 눌린 경우만 true (엣지 트리거)</summary>
    public bool StartJustPressed => Start && !_prevStart;
    public bool BackJustPressed  => Back  && !_prevBack;

    public void Poll()
    {
        if (!_available) return;

        _prevStart = Start;
        _prevBack  = Back;

        try
        {
            uint result = XInputGetState(0, out var state);
            IsConnected = result == 0;  // ERROR_SUCCESS

            if (!IsConnected)
            {
                Left = Right = Up = Down = Start = Back = false;
                return;
            }

            var g = state.Gamepad;

            // 왼쪽 스틱 + D-Pad 방향
            Left  = g.ThumbLX < -Deadzone || (g.Buttons & DpadLeft)  != 0;
            Right = g.ThumbLX >  Deadzone || (g.Buttons & DpadRight) != 0;
            Up    = g.ThumbLY >  Deadzone || (g.Buttons & DpadUp)    != 0;  // XInput: Y+ = 위
            Down  = g.ThumbLY < -Deadzone || (g.Buttons & DpadDown)  != 0;

            // A / Start → 게임 시작·재시작
            Start = (g.Buttons & BtnStart) != 0 || (g.Buttons & BtnA) != 0;
            // B / Back → 타이틀로 복귀
            Back  = (g.Buttons & BtnBack)  != 0 || (g.Buttons & BtnB) != 0;
        }
        catch (DllNotFoundException)
        {
            _available  = false;  // DLL 없음 → 이후 폴링 생략
            IsConnected = false;
        }
        catch
        {
            // 일시적 오류 무시
        }
    }
}
