using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using NitroDrift.Engine;
using NitroDrift.Entities;

namespace NitroDrift;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly GameLoop _loop = new();
    private readonly Random _rng = new();
    private readonly Track _track = new();

    private Car _player = null!;
    private readonly List<AiCar> _rivals = [];
    private readonly List<UIElement> _trackVisuals = [];

    private bool _upDown, _downDown, _leftDown, _rightDown, _spaceDown;
    private int _lastLap;
    private int _lastCountdown = 4;
    private bool _wasBoosting;

    private enum GameState { Title, Countdown, Racing, Paused, Finished }
    private GameState _state = GameState.Title;
    private double _countdownTimer;
    private double _raceTime;
    private double _bestTime;

    public MainWindow()
    {
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

    private void StartRace()
    {
        ClearAll();
        DrawTrack();

        // 플레이어
        var startWp = _track.Waypoints[0];
        _player = new Car(Color.FromRgb(0x3A, 0x86, 0xFF), "YOU");
        double startAngle = Math.Atan2(
            _track.Waypoints[1].Y - startWp.Y,
            _track.Waypoints[1].X - startWp.X);
        _player.Reset(startWp.X, startWp.Y - 15, startAngle);
        GameCanvas.Children.Add(_player.Visual);

        // 라이벌
        _rivals.Clear();
        (Color Color, string Name, double Skill)[] rivalDefs = [
            (Color.FromRgb(0xE7, 0x4C, 0x3C), "RED", 0.85),
            (Color.FromRgb(0x2E, 0xCC, 0x71), "GRN", 0.90),
            (Color.FromRgb(0xFF, 0xA5, 0x00), "ORG", 0.80),
        ];
        for (int i = 0; i < rivalDefs.Length; i++)
        {
            var (color, name, skill) = rivalDefs[i];
            var ai = new AiCar(color, name, skill, _rng);
            ai.Reset(startWp.X, startWp.Y + (i + 1) * 18, startAngle);
            _rivals.Add(ai);
            GameCanvas.Children.Add(ai.Visual);
        }

        _raceTime = 0;
        _lastLap = 1;
        _lastCountdown = 4;
        _wasBoosting = false;
        _state = GameState.Countdown;
        _countdownTimer = 3.0;
        SoundGen.PlayBgm(Sounds.Bgm);

        TitlePanel.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
    }

    private void DrawTrack()
    {
        // 트랙 경로
        var pathFig = new PathFigure { StartPoint = new Point(_track.Waypoints[0].X, _track.Waypoints[0].Y) };
        for (int i = 1; i < _track.Waypoints.Length; i++)
            pathFig.Segments.Add(new LineSegment(new Point(_track.Waypoints[i].X, _track.Waypoints[i].Y), true));
        pathFig.IsClosed = true;

        var pathGeom = new PathGeometry();
        pathGeom.Figures.Add(pathFig);

        // 트랙 외곽
        var outer = new System.Windows.Shapes.Path
        {
            Data = pathGeom,
            Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            StrokeThickness = _track.TrackWidth,
            Fill = Brushes.Transparent,
            StrokeLineJoin = PenLineJoin.Round
        };
        _trackVisuals.Add(outer);
        GameCanvas.Children.Add(outer);

        // 트랙 아스팔트
        var inner = new System.Windows.Shapes.Path
        {
            Data = pathGeom,
            Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            StrokeThickness = _track.TrackWidth - 8,
            Fill = Brushes.Transparent,
            StrokeLineJoin = PenLineJoin.Round
        };
        _trackVisuals.Add(inner);
        GameCanvas.Children.Add(inner);

        // 센터라인 (점선)
        var centerLine = new System.Windows.Shapes.Path
        {
            Data = pathGeom,
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1.5,
            StrokeDashArray = [6, 4],
            Fill = Brushes.Transparent,
            Opacity = 0.3,
            StrokeLineJoin = PenLineJoin.Round
        };
        _trackVisuals.Add(centerLine);
        GameCanvas.Children.Add(centerLine);

        // 시작/결승 라인
        var startWp = _track.Waypoints[0];
        var finishLine = new Rectangle
        {
            Width = 6, Height = _track.TrackWidth,
            Fill = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(finishLine, startWp.X - 3);
        Canvas.SetTop(finishLine, startWp.Y - _track.TrackWidth / 2);
        _trackVisuals.Add(finishLine);
        GameCanvas.Children.Add(finishLine);
    }

    private void OnUpdate(double dt)
    {
        if (_state == GameState.Countdown)
        {
            _countdownTimer -= dt;
            int cd = (int)Math.Ceiling(_countdownTimer);
            CountdownText.Text = cd > 0 ? cd.ToString() : "GO!";
            CountdownText.Visibility = Visibility.Visible;
            if (cd < _lastCountdown)
            {
                _lastCountdown = cd;
                SoundGen.Sfx(cd > 0 ? Sounds.CountdownSfx : Sounds.GoSfx);
            }
            if (_countdownTimer <= -0.5)
            {
                CountdownText.Visibility = Visibility.Collapsed;
                _state = GameState.Racing;
            }
            return;
        }

        if (_state != GameState.Racing) return;

        _raceTime += dt;

        // 플레이어 업데이트
        _player.RaceTime = _raceTime;
        _player.Update(dt, _track, _upDown, _downDown, _leftDown, _rightDown, _spaceDown);

        // 웨이포인트 체크 (Car.Update에서도 하지만 lap 체크 중복 방지)
        // Car.Update에서 이미 처리함

        // 라이벌 업데이트
        foreach (var rival in _rivals)
            rival.UpdateAi(dt, _track);

        // Lap SFX
        if (_player.Lap > _lastLap)
        {
            _lastLap = _player.Lap;
            SoundGen.Sfx(Sounds.LapSfx);
        }

        // Boost SFX
        if (_player.IsBoosting && !_wasBoosting)
            SoundGen.Sfx(Sounds.BoostSfx);
        _wasBoosting = _player.IsBoosting;

        // HUD
        int speedKmh = (int)(Math.Abs(_player.Speed) * 3.6 / 3);
        SpeedText.Text = $"{speedKmh} km/h";
        LapText.Text = $"LAP {Math.Min(_player.Lap, _track.TotalLaps)}/{_track.TotalLaps}";
        TimeText.Text = FormatTime(_raceTime);
        BoostBar.Width = _player.BoostFuel;

        // 포지션
        int pos = 1;
        double playerProgress = GetProgress(_player);
        foreach (var r in _rivals)
            if (GetProgress(r) > playerProgress) pos++;
        PosText.Text = $"POS: {pos}{Suffix(pos)}";

        // 부스트 바 색상
        BoostBar.Background = new SolidColorBrush(_player.IsBoosting
            ? Color.FromRgb(0xFF, 0xD7, 0x00)
            : Color.FromRgb(0xFF, 0x66, 0x00));

        // 완주 체크
        if (_player.Finished)
        {
            _state = GameState.Finished;
            _player.RaceTime = _raceTime;
            SoundGen.StopBgm();
            SoundGen.Sfx(Sounds.FinishSfx);

            HudPanel.Visibility = Visibility.Collapsed;

            // 결과
            var standings = new List<(string Name, double Time)> { (_player.Name, _raceTime) };
            foreach (var r in _rivals)
            {
                if (!r.Finished) r.RaceTime = _raceTime + _rng.NextDouble() * 5;
                standings.Add((r.Name, r.RaceTime));
            }
            standings.Sort((a, b) => a.Time.CompareTo(b.Time));

            int finalPos = standings.FindIndex(s => s.Name == "YOU") + 1;
            ResultPosText.Text = finalPos switch
            {
                1 => "🥇 1ST PLACE!",
                2 => "🥈 2ND PLACE",
                3 => "🥉 3RD PLACE",
                _ => $"{finalPos}TH PLACE"
            };
            ResultTimeText.Text = $"TIME: {FormatTime(_raceTime)}";

            var sb = standings.Select((s, i) => $"{i + 1}. {s.Name}  {FormatTime(s.Time)}");
            ResultStandings.Text = string.Join("\n", sb);

            if (_bestTime == 0 || _raceTime < _bestTime)
                _bestTime = _raceTime;

            ResultOverlay.Visibility = Visibility.Visible;
        }
    }

    private double GetProgress(Car car) => car.Lap * 1000.0 + car.CurrentWaypoint;

    private void OnRender()
    {
        if (_state is not (GameState.Racing or GameState.Paused or GameState.Countdown or GameState.Finished))
            return;

        _player.SyncPosition();
        foreach (var r in _rivals) r.SyncPosition();
    }

    private static string FormatTime(double t)
    {
        int min = (int)(t / 60);
        double sec = t % 60;
        return $"{min}:{sec:00.00}";
    }

    private static string Suffix(int pos) => pos switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.W: _upDown = true; break;
            case Key.Down or Key.S: _downDown = true; break;
            case Key.Left or Key.A: _leftDown = true; break;
            case Key.Right or Key.D: _rightDown = true; break;
            case Key.Space: _spaceDown = true; break;
            case Key.Enter when _state is GameState.Title or GameState.Finished:
                StartRace();
                break;
            case Key.P when _state == GameState.Racing:
                _state = GameState.Paused;
                _loop.Pause();
                PauseOverlay.Visibility = Visibility.Visible;
                break;
            case Key.P when _state == GameState.Paused:
                _state = GameState.Racing;
                _loop.Resume();
                PauseOverlay.Visibility = Visibility.Collapsed;
                break;
            case Key.Escape when _state == GameState.Racing:
                _loop.Stop();
                _state = GameState.Title;
                ShowTitle();
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.W: _upDown = false; break;
            case Key.Down or Key.S: _downDown = false; break;
            case Key.Left or Key.A: _leftDown = false; break;
            case Key.Right or Key.D: _rightDown = false; break;
            case Key.Space: _spaceDown = false; break;
        }
    }

    private void ClearAll()
    {
        foreach (var v in _trackVisuals) GameCanvas.Children.Remove(v);
        _trackVisuals.Clear();
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);
        foreach (var r in _rivals) GameCanvas.Children.Remove(r.Visual);
        _rivals.Clear();
    }

    private void ShowTitle()
    {
        SoundGen.StopBgm();
        ClearAll();
        HudPanel.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        CountdownText.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        BestTimeTitle.Text = _bestTime > 0 ? $"BEST: {FormatTime(_bestTime)}" : "";
        _loop.Start();
    }
}
