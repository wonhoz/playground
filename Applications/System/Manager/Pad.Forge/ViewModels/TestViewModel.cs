using PadForge.Core;
using PadForge.Models;

namespace PadForge.ViewModels;

public class TestViewModel : ViewModelBase
{
    private readonly ControllerService _ctrl;

    // 진동 슬라이더 (0.0~1.0)
    private double _leftVib;
    private double _rightVib;
    private int    _targetIndex;

    public double LeftVibration
    {
        get => _leftVib;
        set { SetField(ref _leftVib, value, nameof(LeftVibration)); ApplyVibration(); }
    }

    public double RightVibration
    {
        get => _rightVib;
        set { SetField(ref _rightVib, value, nameof(RightVibration)); ApplyVibration(); }
    }

    public int TargetControllerIndex
    {
        get => _targetIndex;
        set => SetField(ref _targetIndex, value, nameof(TargetControllerIndex));
    }

    // 스틱 시각화용 (최신 상태)
    private ControllerState? _latestState;

    public double LeftStickX  => (_latestState?.LeftStickX  ?? 0) * 50 + 50;  // 0~100
    public double LeftStickY  => (1 - (_latestState?.LeftStickY ?? 0)) * 50;
    public double RightStickX => (_latestState?.RightStickX ?? 0) * 50 + 50;
    public double RightStickY => (1 - (_latestState?.RightStickY ?? 0)) * 50;

    public double LT => (_latestState?.LeftTrigger  ?? 0) * 100;
    public double RT => (_latestState?.RightTrigger  ?? 0) * 100;

    public ICommand StopVibrationCommand { get; }

    public TestViewModel(ControllerService ctrl)
    {
        _ctrl = ctrl;
        StopVibrationCommand = new RelayCommand(StopVibration);
    }

    public void Update(ControllerState state)
    {
        if (state.Index == _targetIndex || _latestState is null)
        {
            _latestState = state;
            OnPropertyChanged(nameof(LeftStickX));
            OnPropertyChanged(nameof(LeftStickY));
            OnPropertyChanged(nameof(RightStickX));
            OnPropertyChanged(nameof(RightStickY));
            OnPropertyChanged(nameof(LT));
            OnPropertyChanged(nameof(RT));
        }
    }

    private void ApplyVibration()
        => _ctrl.SetVibration(_targetIndex, (float)_leftVib, (float)_rightVib);

    private void StopVibration()
    {
        LeftVibration  = 0;
        RightVibration = 0;
    }
}
