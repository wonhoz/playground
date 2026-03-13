using PadForge.Models;

namespace PadForge.Services;

/// <summary>XInput1_4.dll P/Invoke 래퍼 — 최대 4개 컨트롤러 폴링</summary>
public class XInputService : IDisposable
{
    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    private const byte BATTERY_TYPE_WIRED    = 0x01;
    private const byte BATTERY_TYPE_ALKALINE = 0x02;
    private const byte BATTERY_TYPE_NIMH     = 0x03;

    private const uint ERROR_SUCCESS        = 0;
    private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_4.dll", EntryPoint = "#100")]  // 숨겨진 내보내기 (가이드 버튼 포함)
    private static extern uint XInputGetStateEx(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    #endregion

    public const int MaxControllers = 4;

    private readonly ControllerState[] _states = new ControllerState[MaxControllers];
    private readonly bool[] _wasConnected = new bool[MaxControllers];

    public event Action<ControllerState>? ControllerConnected;
    public event Action<ControllerState>? ControllerDisconnected;
    public event Action<ControllerState>? StateUpdated;

    public XInputService()
    {
        for (int i = 0; i < MaxControllers; i++)
            _states[i] = new ControllerState { Index = i };
    }

    /// <summary>모든 컨트롤러 상태 1회 갱신 (폴링 루프에서 호출)</summary>
    public void Poll()
    {
        for (uint i = 0; i < MaxControllers; i++)
            UpdateController(i);
    }

    private void UpdateController(uint idx)
    {
        var result = XInputGetStateEx(idx, out var raw);
        var state  = _states[idx];

        bool connected = result == ERROR_SUCCESS;
        bool wasConn   = _wasConnected[idx];

        if (!connected)
        {
            if (wasConn)
            {
                state.IsConnected = false;
                _wasConnected[idx] = false;
                ControllerDisconnected?.Invoke(state);
            }
            return;
        }

        state.IsConnected = true;
        var gp = raw.Gamepad;

        state.Buttons       = (XButtons)gp.wButtons;
        state.LeftTrigger   = gp.bLeftTrigger  / 255f;
        state.RightTrigger  = gp.bRightTrigger / 255f;
        state.LeftStickX    = NormalizeStick(gp.sThumbLX);
        state.LeftStickY    = NormalizeStick(gp.sThumbLY);
        state.RightStickX   = NormalizeStick(gp.sThumbRX);
        state.RightStickY   = NormalizeStick(gp.sThumbRY);
        state.Type          = ControllerType.XInput;

        // 배터리 정보
        if (XInputGetBatteryInformation(idx, 0, out var batt) == ERROR_SUCCESS)
        {
            state.IsWireless    = batt.BatteryType != BATTERY_TYPE_WIRED;
            state.BatteryLevel  = batt.BatteryType == BATTERY_TYPE_WIRED
                ? -1
                : MapBatteryLevel(batt.BatteryLevel);
        }

        if (!wasConn)
        {
            _wasConnected[idx] = true;
            ControllerConnected?.Invoke(state);
        }

        StateUpdated?.Invoke(state);
    }

    private static float NormalizeStick(short raw)
    {
        // 데드존 처리 없이 순수 정규화 (데드존은 프로파일에서 처리)
        return raw / (raw >= 0 ? 32767f : 32768f);
    }

    private static int MapBatteryLevel(byte level) => level switch
    {
        0 => 10,
        1 => 40,
        2 => 70,
        3 => 100,
        _ => -1
    };

    /// <summary>진동 설정 (left/right: 0.0~1.0)</summary>
    public void SetVibration(int index, float left, float right)
    {
        var vib = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed  = (ushort)(left  * 65535),
            wRightMotorSpeed = (ushort)(right * 65535)
        };
        XInputSetState((uint)index, ref vib);
    }

    /// <summary>현재 상태 스냅샷 반환</summary>
    public ControllerState GetState(int index) => _states[index];

    /// <summary>연결된 컨트롤러 목록</summary>
    public IEnumerable<ControllerState> GetConnected() =>
        _states.Where(s => s.IsConnected);

    public void Dispose()
    {
        // 진동 정지
        for (int i = 0; i < MaxControllers; i++)
            SetVibration(i, 0f, 0f);
    }
}
