using PadForge.Models;

namespace PadForge.Services;

/// <summary>
/// XInput + HID 통합 컨트롤러 서비스
/// 폴링 루프 관리 및 매핑 실행 조율
/// </summary>
public class ControllerService : IDisposable
{
    private readonly XInputService _xinput = new();
    private readonly HidService    _hid    = new();
    private readonly ViGEmService  _vigem;

    private VirtualInputService? _virtualInput;
    private ControllerProfile?   _activeProfile;
    private CancellationTokenSource? _cts;

    private readonly HashSet<GamepadInput> _pressed = [];

    public event Action<ControllerState>? ControllerConnected;
    public event Action<ControllerState>? ControllerDisconnected;
    public event Action<ControllerState>? StateUpdated;

    public ControllerService(ViGEmService vigem)
    {
        _vigem = vigem;

        _xinput.ControllerConnected    += s => ControllerConnected?.Invoke(s);
        _xinput.ControllerDisconnected += s => ControllerDisconnected?.Invoke(s);
        _xinput.StateUpdated           += OnStateUpdated;

        _hid.ControllerConnected    += s => ControllerConnected?.Invoke(s);
        _hid.ControllerDisconnected += s => ControllerDisconnected?.Invoke(s);
        _hid.StateUpdated           += OnStateUpdated;
    }

    public void Start(VirtualInputService virtualInput)
    {
        _virtualInput = virtualInput;
        _cts = new CancellationTokenSource();

        _hid.Start();

        // XInput 폴링 루프 ~100Hz
        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                _xinput.Poll();
                await Task.Delay(10, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _hid.Stop();
        _xinput.Dispose();
    }

    public void SetProfile(ControllerProfile? profile)
    {
        _activeProfile = profile;
        if (profile?.ViGEmEnabled == true && _vigem.IsAvailable)
            _vigem.CreateVirtualController();
        else
            _vigem.RemoveVirtualController();
    }

    /// <summary>진동 테스트용</summary>
    public void SetVibration(int index, float left, float right)
        => _xinput.SetVibration(index, left, right);

    public IEnumerable<ControllerState> GetConnectedControllers()
        => _xinput.GetConnected();

    private void OnStateUpdated(ControllerState state)
    {
        StateUpdated?.Invoke(state);

        if (_activeProfile is null || _virtualInput is null) return;

        // 현재 프레임 활성 입력
        var current = state.GetActiveInputs(
            _activeProfile.LeftStickDeadzone,
            _activeProfile.RightStickDeadzone,
            _activeProfile.TriggerDeadzone).ToHashSet();

        // 새로 눌린 버튼
        foreach (var inp in current)
        {
            if (_pressed.Contains(inp)) continue;
            _pressed.Add(inp);
            var mapping = _activeProfile.Mappings.FirstOrDefault(m => m.Input == inp);
            if (mapping is not null)
                _virtualInput.ExecuteDown(mapping.Action);
        }

        // 뗀 버튼
        var released = _pressed.Except(current).ToList();
        foreach (var inp in released)
        {
            _pressed.Remove(inp);
            var mapping = _activeProfile.Mappings.FirstOrDefault(m => m.Input == inp);
            if (mapping is not null)
                _virtualInput.ExecuteUp(mapping.Action);
        }

        // ViGEm 가상 출력
        if (_activeProfile.ViGEmEnabled && _vigem.IsAvailable)
        {
            _vigem.SendState(state.Buttons,
                state.LeftTrigger, state.RightTrigger,
                state.LeftStickX,  state.LeftStickY,
                state.RightStickX, state.RightStickY);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
