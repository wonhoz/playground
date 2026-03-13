using HidSharp;
using PadForge.Models;

namespace PadForge.Services;

/// <summary>
/// HidSharp 기반 HID 컨트롤러 서비스
/// DS4 / DualSense / Switch Pro 등 DirectInput 계열 감지
/// </summary>
public class HidService : IDisposable
{
    // 알려진 게임패드 Vendor/Product ID
    private static readonly (int Vid, int Pid, ControllerType Type)[] KnownDevices =
    [
        (0x054C, 0x05C4, ControllerType.DualShock4),  // DS4 v1
        (0x054C, 0x09CC, ControllerType.DualShock4),  // DS4 v2
        (0x054C, 0x0CE6, ControllerType.DualSense),   // DualSense
        (0x057E, 0x2009, ControllerType.SwitchPro),   // Switch Pro Controller
        (0x057E, 0x2006, ControllerType.SwitchPro),   // Joy-Con L
        (0x057E, 0x2007, ControllerType.SwitchPro),   // Joy-Con R
    ];

    private readonly List<HidDevice> _activeDevices = [];
    private readonly List<Thread> _readThreads = [];
    private volatile bool _running;

    public event Action<ControllerState>? ControllerConnected;
    public event Action<ControllerState>? ControllerDisconnected;
    public event Action<ControllerState>? StateUpdated;

    public void Start()
    {
        _running = true;
        DeviceList.Local.Changed += OnDeviceListChanged;
        ScanDevices();
    }

    public void Stop()
    {
        _running = false;
        DeviceList.Local.Changed -= OnDeviceListChanged;
        lock (_activeDevices) _activeDevices.Clear();
    }

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
        => ScanDevices();

    private void ScanDevices()
    {
        var allDevices = DeviceList.Local.GetHidDevices();
        foreach (var device in allDevices)
        {
            var known = KnownDevices.FirstOrDefault(k =>
                k.Vid == device.VendorID && k.Pid == device.ProductID);

            if (known == default) continue;
            if (_activeDevices.Contains(device)) continue;

            lock (_activeDevices) _activeDevices.Add(device);

            var state = new ControllerState
            {
                Type        = known.Type,
                IsConnected = true,
                IsWireless  = true,
            };
            ControllerConnected?.Invoke(state);
            StartReadThread(device, state, known.Type);
        }
    }

    private void StartReadThread(HidDevice device, ControllerState state, ControllerType type)
    {
        var t = new Thread(() => ReadLoop(device, state, type))
        {
            IsBackground = true,
            Name         = $"HID_{type}"
        };
        _readThreads.Add(t);
        t.Start();
    }

    private void ReadLoop(HidDevice device, ControllerState state, ControllerType type)
    {
        try
        {
            if (!device.TryOpen(out var stream)) return;
            using (stream)
            {
                var buf = new byte[device.GetMaxInputReportLength()];
                while (_running)
                {
                    int read = stream.Read(buf, 0, buf.Length);
                    if (read <= 0) break;

                    ParseReport(buf, read, state, type);
                    StateUpdated?.Invoke(state);
                }
            }
        }
        catch { /* 연결 해제 */ }
        finally
        {
            state.IsConnected = false;
            lock (_activeDevices) _activeDevices.Remove(device);
            ControllerDisconnected?.Invoke(state);
        }
    }

    /// <summary>HID 리포트 파싱 (타입별)</summary>
    private static void ParseReport(byte[] buf, int len, ControllerState state, ControllerType type)
    {
        switch (type)
        {
            case ControllerType.DualShock4:
            case ControllerType.DualSense:
                ParseDualShock(buf, len, state);
                break;
            case ControllerType.SwitchPro:
                ParseSwitchPro(buf, len, state);
                break;
        }
    }

    /// <summary>DS4/DualSense USB 리포트 파싱 (Report ID 0x01)</summary>
    private static void ParseDualShock(byte[] buf, int len, ControllerState state)
    {
        if (len < 10) return;

        // 스틱 (1~4번 바이트)
        state.LeftStickX  =  (buf[1] / 127.5f) - 1f;
        state.LeftStickY  = -((buf[2] / 127.5f) - 1f);  // Y축 반전
        state.RightStickX =  (buf[3] / 127.5f) - 1f;
        state.RightStickY = -((buf[4] / 127.5f) - 1f);

        // 트리거
        state.LeftTrigger  = buf[8] / 255f;
        state.RightTrigger = buf[9] / 255f;

        // 버튼 (5~8번 바이트)
        var b5 = buf[5];  // 방향키 하위 4비트, 모양 버튼 상위 4비트
        var b6 = buf[6];
        var b7 = buf[7];

        XButtons btns = XButtons.None;

        // 모양 버튼 (b5 상위 4비트)
        if ((b5 & 0x80) != 0) btns |= XButtons.Y;       // Triangle→Y
        if ((b5 & 0x40) != 0) btns |= XButtons.B;       // Circle→B
        if ((b5 & 0x20) != 0) btns |= XButtons.A;       // Cross→A
        if ((b5 & 0x10) != 0) btns |= XButtons.X;       // Square→X

        // D-pad (b5 하위 4비트, 방향 인코딩)
        var dpad = b5 & 0x0F;
        if (dpad == 0 || dpad == 1 || dpad == 7) btns |= XButtons.DpadUp;
        if (dpad == 2 || dpad == 1 || dpad == 3) btns |= XButtons.DpadRight;
        if (dpad == 4 || dpad == 3 || dpad == 5) btns |= XButtons.DpadDown;
        if (dpad == 6 || dpad == 5 || dpad == 7) btns |= XButtons.DpadLeft;

        // 숄더/트리거 (b6)
        if ((b6 & 0x01) != 0) btns |= XButtons.LeftShoulder;
        if ((b6 & 0x02) != 0) btns |= XButtons.RightShoulder;

        // Options/Share (b6)
        if ((b6 & 0x10) != 0) btns |= XButtons.Back;
        if ((b6 & 0x20) != 0) btns |= XButtons.Start;

        // 스틱 누름 (b6)
        if ((b6 & 0x40) != 0) btns |= XButtons.LeftThumb;
        if ((b6 & 0x80) != 0) btns |= XButtons.RightThumb;

        state.Buttons = btns;
    }

    /// <summary>Switch Pro 리포트 파싱 (BT/USB 공통 포맷)</summary>
    private static void ParseSwitchPro(byte[] buf, int len, ControllerState state)
    {
        if (len < 12) return;

        // Switch Pro 표준 입력 리포트 0x30
        if (buf[0] != 0x30 && buf[0] != 0x3F) return;

        var b3 = buf[3];
        var b4 = buf[4];
        var b5 = buf[5];

        XButtons btns = XButtons.None;

        // 오른쪽 버튼 (b3)
        if ((b3 & 0x08) != 0) btns |= XButtons.Y;
        if ((b3 & 0x04) != 0) btns |= XButtons.X;
        if ((b3 & 0x02) != 0) btns |= XButtons.B;
        if ((b3 & 0x01) != 0) btns |= XButtons.A;
        if ((b3 & 0x40) != 0) btns |= XButtons.RightShoulder;

        // 왼쪽 버튼 (b5)
        if ((b5 & 0x08) != 0) btns |= XButtons.DpadLeft;
        if ((b5 & 0x04) != 0) btns |= XButtons.DpadDown;
        if ((b5 & 0x02) != 0) btns |= XButtons.DpadUp;
        if ((b5 & 0x01) != 0) btns |= XButtons.DpadRight;
        if ((b5 & 0x40) != 0) btns |= XButtons.LeftShoulder;

        // 공통 (b4)
        if ((b4 & 0x02) != 0) btns |= XButtons.Back;
        if ((b4 & 0x04) != 0) btns |= XButtons.Start;
        if ((b4 & 0x08) != 0) btns |= XButtons.LeftThumb;
        if ((b4 & 0x10) != 0) btns |= XButtons.RightThumb;

        state.Buttons = btns;

        // 스틱 (12비트 형식)
        if (len >= 12)
        {
            int lx = buf[6]  | ((buf[7]  & 0x0F) << 8);
            int ly = (buf[7]  >> 4) | (buf[8]  << 4);
            int rx = buf[9]  | ((buf[10] & 0x0F) << 8);
            int ry = (buf[10] >> 4) | (buf[11] << 4);

            state.LeftStickX  =  (lx / 2047.5f) - 1f;
            state.LeftStickY  = -((ly / 2047.5f) - 1f);
            state.RightStickX =  (rx / 2047.5f) - 1f;
            state.RightStickY = -((ry / 2047.5f) - 1f);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
