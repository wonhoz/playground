using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TrackStar.Engine;
using TrackStar.Events;

namespace TrackStar;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const double TrackW = 784;
    private const double TrackH = 462;

    private readonly GameLoop _loop = new();
    private bool _leftDown, _rightDown, _spaceJust;

    private enum GameState { Title, Playing, EventResult, Final }
    private GameState _state = GameState.Title;

    private readonly SportEvent[] _events;
    private int _currentEvent;
    private SportEvent CurrentEvent => _events[_currentEvent];
    private readonly List<string> _results = [];

    // 트랙 비주얼
    private readonly List<UIElement> _trackVisuals = [];
    private readonly Rectangle[] _runners = new Rectangle[4]; // 0=player, 1~3=rivals
    private double _resultShowTimer;
    private int _lastCountdown = -1;
    private EventPhase _lastPhase = EventPhase.Ready;

    public MainWindow()
    {
        _events = [new Sprint100m(), new Hurdles(), new LongJump(), new Javelin()];

        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int value = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
            }
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    private void StartGame()
    {
        _currentEvent = 0;
        _results.Clear();
        ClearTrack();
        foreach (var ev in _events) ev.Reset();

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        FinalOverlay.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;

        SetupTrack();
        CurrentEvent.Reset();
        CurrentEvent.Phase = EventPhase.Ready;
        _lastCountdown = -1;
        _lastPhase = EventPhase.Ready;
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void SetupTrack()
    {
        ClearTrack();

        // 트랙/필드 배경
        var ground = new Rectangle
        {
            Width = TrackW, Height = 200,
            Fill = new SolidColorBrush(Color.FromRgb(0xCC, 0x55, 0x33)) // 트랙 색
        };
        Canvas.SetTop(ground, 180);
        _trackVisuals.Add(ground);
        GameCanvas.Children.Add(ground);

        // 레인 라인
        for (int lane = 0; lane < 5; lane++)
        {
            var line = new Rectangle
            {
                Width = TrackW, Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                Opacity = 0.3
            };
            Canvas.SetTop(line, 180 + lane * 50);
            _trackVisuals.Add(line);
            GameCanvas.Children.Add(line);
        }

        // 잔디
        var grass = new Rectangle
        {
            Width = TrackW, Height = 180,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x22, 0x88, 0x33),
                Color.FromRgb(0x1A, 0x66, 0x28), 90)
        };
        _trackVisuals.Add(grass);
        GameCanvas.Children.Add(grass);

        // 러너들
        Color[] colors = [
            Color.FromRgb(0x3A, 0x86, 0xFF), // 플레이어
            Color.FromRgb(0xE7, 0x4C, 0x3C),
            Color.FromRgb(0x2E, 0xCC, 0x71),
            Color.FromRgb(0xFF, 0xA5, 0x00)
        ];
        for (int i = 0; i < 4; i++)
        {
            _runners[i] = new Rectangle
            {
                Width = 16, Height = 20,
                Fill = new SolidColorBrush(colors[i]),
                RadiusX = 4, RadiusY = 4,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = i == 0 ? 2 : 0.5
            };
            Canvas.SetLeft(_runners[i], 20);
            Canvas.SetTop(_runners[i], 188 + i * 50);
            _trackVisuals.Add(_runners[i]);
            GameCanvas.Children.Add(_runners[i]);
        }
    }

    private void ClearTrack()
    {
        foreach (var v in _trackVisuals) GameCanvas.Children.Remove(v);
        _trackVisuals.Clear();
    }

    private void OnUpdate(double dt)
    {
        if (_state == GameState.EventResult)
        {
            _resultShowTimer -= dt;
            if (_resultShowTimer <= 0)
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                _currentEvent++;
                if (_currentEvent >= _events.Length)
                {
                    // 대회 종료
                    SoundGen.StopBgm();
                    SoundGen.Sfx(Sounds.MedalSfx);
                    _state = GameState.Final;
                    HudPanel.Visibility = Visibility.Collapsed;
                    GaugePanel.Visibility = Visibility.Collapsed;
                    FinalMedals.Text = string.Join("\n", _results);
                    FinalOverlay.Visibility = Visibility.Visible;
                    return;
                }
                _state = GameState.Playing;
                _lastCountdown = -1;
                _lastPhase = EventPhase.Ready;
                SetupTrack();
                CurrentEvent.Reset();
                CurrentEvent.Phase = EventPhase.Ready;
            }
            return;
        }

        if (_state != GameState.Playing) return;

        bool sp = _spaceJust;
        _spaceJust = false;

        CurrentEvent.Update(dt, _leftDown, _rightDown, sp);

        // Audio: countdown ticks & go
        if (CurrentEvent.Phase == EventPhase.Countdown)
        {
            int cd = (int)Math.Ceiling(CurrentEvent.Timer);
            if (cd != _lastCountdown && cd >= 1 && cd <= 3)
            {
                SoundGen.Sfx(Sounds.CountdownSfx);
                _lastCountdown = cd;
            }
        }
        if (_lastPhase == EventPhase.Countdown && CurrentEvent.Phase == EventPhase.Active)
        {
            SoundGen.Sfx(Sounds.GoSfx);
        }

        // Audio: step sfx on alternating key presses during active phase
        if (CurrentEvent.Phase == EventPhase.Active && (_leftDown || _rightDown))
        {
            SoundGen.Sfx(Sounds.StepSfx);
        }

        // Audio: jump sfx
        if (CurrentEvent.Phase == EventPhase.Active && sp)
        {
            SoundGen.Sfx(Sounds.JumpSfx);
        }

        _lastPhase = CurrentEvent.Phase;

        // HUD
        EventNameText.Text = CurrentEvent.Name;

        if (CurrentEvent.Phase == EventPhase.Countdown)
        {
            int cd = (int)Math.Ceiling(CurrentEvent.Timer);
            CountdownText.Text = cd > 0 ? cd.ToString() : "GO!";
            CountdownText.Visibility = Visibility.Visible;
            TimerText.Text = "";
        }
        else
        {
            CountdownText.Visibility = Visibility.Collapsed;
        }

        if (CurrentEvent.Phase == EventPhase.Active)
        {
            TimerText.Text = $"{CurrentEvent.Timer:F2}s";
        }

        // 종목별 추가 정보
        UpdateEventSpecificUI();

        // 결과
        if (CurrentEvent.IsComplete)
        {
            SoundGen.Sfx(Sounds.FinishSfx);
            _results.Add($"{CurrentEvent.Name}: {CurrentEvent.ResultText}");
            ResultTitle.Text = CurrentEvent.Name;
            ResultDetail.Text = CurrentEvent.ResultText;
            ResultNext.Text = _currentEvent < _events.Length - 1
                ? $"다음 종목: {_events[_currentEvent + 1].Name}"
                : "최종 결과 발표!";
            ResultOverlay.Visibility = Visibility.Visible;
            GaugePanel.Visibility = Visibility.Collapsed;
            _state = GameState.EventResult;
            _resultShowTimer = 3.0;
        }
    }

    private void UpdateEventSpecificUI()
    {
        if (CurrentEvent is LongJump lj)
        {
            InfoText.Text = $"시도: {lj.Attempt}/3  최고: {lj.BestDistance:F2}m";
        }
        else if (CurrentEvent is Javelin jav)
        {
            InfoText.Text = $"시도: {jav.Attempt}/3  최고: {jav.BestDistance:F1}m";

            if (jav.InPowerPhase)
            {
                GaugePanel.Visibility = Visibility.Visible;
                GaugeLabel.Text = "POWER";
                GaugeFill.Width = 300 * jav.PowerGauge;
                GaugeFill.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x00));
                GaugeOptimal.Visibility = Visibility.Collapsed;
            }
            else if (jav.InAnglePhase)
            {
                GaugePanel.Visibility = Visibility.Visible;
                GaugeLabel.Text = "ANGLE (최적: 40~45°)";
                GaugeFill.Width = 300 * jav.AngleGauge;
                GaugeFill.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xBB, 0xFF));
                GaugeOptimal.Visibility = Visibility.Visible;
                Canvas.SetLeft(GaugeOptimal, 300 * 0.375); // 30~45도 = 0.375~0.5
                GaugeOptimal.Width = 300 * 0.1;
            }
            else
            {
                GaugePanel.Visibility = Visibility.Collapsed;
            }
        }
        else if (CurrentEvent is Hurdles h)
        {
            InfoText.Text = $"넘어진 허들: {h.HurdlesHit}";
        }
        else
        {
            InfoText.Text = "";
            GaugePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnRender()
    {
        if (_state is not (GameState.Playing or GameState.EventResult)) return;

        // 러너 위치 동기화
        double trackStart = 30;
        double trackEnd = TrackW - 40;
        double range = trackEnd - trackStart;

        Canvas.SetLeft(_runners[0], trackStart + CurrentEvent.PlayerPos * range);
        for (int i = 0; i < 3; i++)
            Canvas.SetLeft(_runners[i + 1], trackStart + CurrentEvent.RivalPos[i] * range);

        // 멀리뛰기 점프 높이
        if (CurrentEvent is LongJump lj && lj.HasJumped)
        {
            Canvas.SetTop(_runners[0], 188 - lj.JumpHeight);
        }
        else
        {
            Canvas.SetTop(_runners[0], 188);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A: _leftDown = true; break;
            case Key.Right or Key.D: _rightDown = true; break;
            case Key.Space: _spaceJust = true; break;
            case Key.Enter when _state is GameState.Title or GameState.Final:
                StartGame();
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A: _leftDown = false; break;
            case Key.Right or Key.D: _rightDown = false; break;
        }
    }
}
