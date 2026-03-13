using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PadForge.Models;

namespace PadForge.Services;

/// <summary>
/// ViGEmBus 가상 컨트롤러 서비스 (Nefarius.ViGEm.Client v1.21.x)
/// 드라이버 없을 경우 graceful fallback (IsAvailable = false)
/// API: SetButtonsFull(ushort), SetAxisValue(Xbox360Axis, short), SetSliderValue(Xbox360Slider, byte)
/// </summary>
public class ViGEmService : IDisposable
{
    private ViGEmClient?        _client;
    private IXbox360Controller? _virtualController;

    public bool IsAvailable { get; private set; }

    public ViGEmService()
    {
        try
        {
            _client      = new ViGEmClient();
            IsAvailable  = true;
        }
        catch
        {
            IsAvailable  = false;
        }
    }

    public void CreateVirtualController()
    {
        if (!IsAvailable || _client is null) return;
        try
        {
            _virtualController = _client.CreateXbox360Controller();
            _virtualController.Connect();
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public void RemoveVirtualController()
    {
        try { _virtualController?.Disconnect(); } catch { }
        _virtualController = null;
    }

    /// <summary>
    /// 가상 컨트롤러에 상태 전송
    /// buttons: XInput bitmask (XButtons 열거형과 동일한 비트 구조)
    /// </summary>
    public void SendState(XButtons buttons, float lt, float rt,
                          float lx, float ly, float rx, float ry)
    {
        if (_virtualController is null) return;

        try
        {
            // 버튼 전체 설정 (ushort bitmask 그대로)
            _virtualController.SetButtonsFull((ushort)buttons);

            // 트리거 (0~255)
            _virtualController.SetSliderValue(Xbox360Slider.LeftTrigger,  (byte)(lt * 255));
            _virtualController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rt * 255));

            // 스틱 축 (-32767 ~ 32767)
            _virtualController.SetAxisValue(Xbox360Axis.LeftThumbX,  (short)(lx * 32767));
            _virtualController.SetAxisValue(Xbox360Axis.LeftThumbY,  (short)(ly * 32767));
            _virtualController.SetAxisValue(Xbox360Axis.RightThumbX, (short)(rx * 32767));
            _virtualController.SetAxisValue(Xbox360Axis.RightThumbY, (short)(ry * 32767));
        }
        catch { /* 연결 해제 등 예외 무시 */ }
    }

    public void Dispose()
    {
        RemoveVirtualController();
        _client?.Dispose();
    }
}
