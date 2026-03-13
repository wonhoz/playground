using PadForge.Core;
using PadForge.Models;

namespace PadForge.ViewModels;

public class ControllerViewModel : ViewModelBase
{
    private ControllerState _state;

    public int            Index    => _state.Index;
    public ControllerType Type     => _state.Type;
    public bool           Connected => _state.IsConnected;
    public string         Label    => $"P{_state.Index + 1} — {_state.Type}";
    public string         Battery  => _state.BatteryLevel > 0 ? $"{_state.BatteryLevel}%" : "유선";

    // 스틱 (UI 바인딩용, 0~200 범위 Canvas 좌표로 변환)
    public double LeftStickDisplayX  => (_state.LeftStickX  + 1) * 50;   // 0~100
    public double LeftStickDisplayY  => (1 - _state.LeftStickY) * 50;
    public double RightStickDisplayX => (_state.RightStickX + 1) * 50;
    public double RightStickDisplayY => (1 - _state.RightStickY) * 50;

    // 트리거
    public float  LeftTrigger  => _state.LeftTrigger;
    public float  RightTrigger => _state.RightTrigger;

    // 버튼 상태
    public bool A             => _state.IsButtonPressed(XButtons.A);
    public bool B             => _state.IsButtonPressed(XButtons.B);
    public bool X             => _state.IsButtonPressed(XButtons.X);
    public bool Y             => _state.IsButtonPressed(XButtons.Y);
    public bool LB            => _state.IsButtonPressed(XButtons.LeftShoulder);
    public bool RB            => _state.IsButtonPressed(XButtons.RightShoulder);
    public bool Start         => _state.IsButtonPressed(XButtons.Start);
    public bool Back          => _state.IsButtonPressed(XButtons.Back);
    public bool DpadUp        => _state.IsButtonPressed(XButtons.DpadUp);
    public bool DpadDown      => _state.IsButtonPressed(XButtons.DpadDown);
    public bool DpadLeft      => _state.IsButtonPressed(XButtons.DpadLeft);
    public bool DpadRight     => _state.IsButtonPressed(XButtons.DpadRight);

    public ControllerViewModel(ControllerState state)
    {
        _state = state;
    }

    public void Update(ControllerState state)
    {
        _state = state;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Battery));
        OnPropertyChanged(nameof(Connected));
        OnPropertyChanged(nameof(LeftStickDisplayX));
        OnPropertyChanged(nameof(LeftStickDisplayY));
        OnPropertyChanged(nameof(RightStickDisplayX));
        OnPropertyChanged(nameof(RightStickDisplayY));
        OnPropertyChanged(nameof(LeftTrigger));
        OnPropertyChanged(nameof(RightTrigger));
        OnPropertyChanged(nameof(A));
        OnPropertyChanged(nameof(B));
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(LB));
        OnPropertyChanged(nameof(RB));
        OnPropertyChanged(nameof(Start));
        OnPropertyChanged(nameof(Back));
        OnPropertyChanged(nameof(DpadUp));
        OnPropertyChanged(nameof(DpadDown));
        OnPropertyChanged(nameof(DpadLeft));
        OnPropertyChanged(nameof(DpadRight));
    }
}
