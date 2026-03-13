namespace PadForge.Models;

/// <summary>XInput 버튼 열거형 (XINPUT_GAMEPAD 비트 플래그와 일치)</summary>
[Flags]
public enum XButtons : ushort
{
    None           = 0x0000,
    DpadUp         = 0x0001,
    DpadDown       = 0x0002,
    DpadLeft       = 0x0004,
    DpadRight      = 0x0008,
    Start          = 0x0010,
    Back           = 0x0020,
    LeftThumb      = 0x0040,
    RightThumb     = 0x0080,
    LeftShoulder   = 0x0100,
    RightShoulder  = 0x0200,
    A              = 0x1000,
    B              = 0x2000,
    X              = 0x4000,
    Y              = 0x8000,
}

/// <summary>매핑 가능한 게임패드 입력 (버튼 + 아날로그 축)</summary>
public enum GamepadInput
{
    // 디지털 버튼
    DpadUp, DpadDown, DpadLeft, DpadRight,
    Start, Back, Guide,
    LeftThumb, RightThumb,
    LeftShoulder, RightShoulder,
    A, B, X, Y,

    // 트리거 (임계값 초과 시 디지털 취급)
    LeftTrigger, RightTrigger,

    // 스틱 방향 (임계값 기준)
    LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight,
    RightStickUp, RightStickDown, RightStickLeft, RightStickRight,
}

/// <summary>컨트롤러 타입</summary>
public enum ControllerType
{
    Unknown,
    XInput,
    DualShock4,
    DualSense,
    SwitchPro,
    Generic,
}
