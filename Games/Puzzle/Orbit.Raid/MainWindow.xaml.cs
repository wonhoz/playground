using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using OrbitRaid.Models;
using OrbitRaid.Services;

namespace OrbitRaid;

public partial class MainWindow : Window
{
    // ── 물리 ──
    private SimulatorState? _state;
    private int _currentLevel = 1;
    private const int MaxLevels = 5;

    // ── 렌더 ──
    private double _scale = 1e-9;       // 픽셀 per 미터
    private double _offsetX, _offsetY;  // 캔버스 중심 오프셋
    private double _canvasW, _canvasH;
    private const int PredictSteps = 300;

    // ── 타이머 ──
    private readonly DispatcherTimer _simTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private bool _running = false;

    // UI 요소 캐시
    private readonly Dictionary<Body, Ellipse> _bodyEllipses = [];
    private Polyline? _predictLine;
    private Ellipse? _playerEllipse;

    public MainWindow()
    {
        InitializeComponent();
        _simTimer.Tick += OnSimTick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
        LoadLevel(_currentLevel);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ══════════════════════════════════════════════════════
    //  레벨 관리
    // ══════════════════════════════════════════════════════

    private void LoadLevel(int num)
    {
        _simTimer.Stop();
        _running = false;
        ResultOverlay.Visibility = Visibility.Collapsed;

        var level = LevelFactory.Create(num);
        _state = new SimulatorState
        {
            Bodies = level.Bodies,
            Player = level.Player,
            Target = level.Target,
            IsRunning = false,
            Result = GameResult.Running
        };

        LblLevel.Text = $"Lv.{num}";
        LblLevelTitle.Text = level.Title;
        LblHint.Text = level.Hint;
        LblStatus.Text = "스페이스: 시작";

        // 자동 줌 설정
        AutoZoom();
        RebuildCanvas();
        UpdateHUD();
    }

    private void AutoZoom()
    {
        if (_state == null) return;
        var allPos = _state.Bodies.Select(b => b.Position)
                          .Concat([_state.Player!.Position]).ToList();
        double maxR = allPos.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
        if (maxR < 1) maxR = 1e11;
        double halfCanvas = Math.Min(_canvasW, _canvasH) / 2.0;
        if (halfCanvas < 1) halfCanvas = 300;
        _scale = halfCanvas / (maxR * 1.3);
        _offsetX = 0;
        _offsetY = 0;
    }

    private void SimCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _canvasW = e.NewSize.Width;
        _canvasH = e.NewSize.Height;
        if (_state != null) { AutoZoom(); RebuildCanvas(); }
    }

    // ══════════════════════════════════════════════════════
    //  시뮬레이션
    // ══════════════════════════════════════════════════════

    private void OnSimTick(object? sender, EventArgs e)
    {
        if (_state == null || !_running) return;

        var speed = (int)SliderSpeed.Value;
        for (int i = 0; i < speed; i++)
            Simulator.Step(_state, Simulator.BaseTimeStep);

        LblSpeed.Text = $"x{speed}";
        UpdateRender();
        UpdateHUD();

        if (_state.Result != GameResult.Running)
        {
            _simTimer.Stop();
            _running = false;
            ShowResult(_state.Result);
        }
    }

    private void ToggleSimulation()
    {
        if (_state == null) return;
        if (_state.Result != GameResult.Running) return;

        _running = !_running;
        _state.IsRunning = _running;

        if (_running)
        {
            _simTimer.Start();
            LblStatus.Text = "■ 시뮬 중...";
        }
        else
        {
            _simTimer.Stop();
            LblStatus.Text = "▶ 일시정지";
            UpdatePredictLine();
        }
    }

    // ══════════════════════════════════════════════════════
    //  렌더링
    // ══════════════════════════════════════════════════════

    private void RebuildCanvas()
    {
        SimCanvas.Children.Clear();
        _bodyEllipses.Clear();
        _predictLine = null;
        _playerEllipse = null;

        if (_state == null) return;

        // 별 배경 (정적 점)
        var rng = new Random(42);
        for (int i = 0; i < 120; i++)
        {
            var star = new Ellipse
            {
                Width = rng.Next(1, 3),
                Height = rng.Next(1, 3),
                Fill = new SolidColorBrush(Color.FromArgb((byte)rng.Next(60, 160), 200, 200, 255))
            };
            Canvas.SetLeft(star, rng.NextDouble() * _canvasW);
            Canvas.SetTop(star, rng.NextDouble() * _canvasH);
            SimCanvas.Children.Add(star);
        }

        // Target SOI 원
        if (_state.Target != null)
        {
            var soiR = _state.Target.SOI * _scale;
            var soiEllipse = new Ellipse
            {
                Width = soiR * 2, Height = soiR * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 80, 220, 120)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([4, 4])
            };
            SimCanvas.Children.Add(soiEllipse);
            UpdateEllipsePosition(soiEllipse, _state.Target.Position, soiR);
        }

        // 예측 선
        _predictLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(120, 80, 200, 220)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([3, 3])
        };
        SimCanvas.Children.Add(_predictLine);

        // 천체
        foreach (var b in _state.Bodies)
        {
            var r = Math.Max(4, b.Radius * _scale);
            var el = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(b.Color),
                ToolTip = b.Name
            };
            if (b.Type == BodyType.Target)
            {
                el.Stroke = new SolidColorBrush(Color.FromArgb(180, 80, 220, 120));
                el.StrokeThickness = 2;
            }
            SimCanvas.Children.Add(el);
            _bodyEllipses[b] = el;
            UpdateEllipsePosition(el, b.Position, r);
        }

        // 플레이어
        if (_state.Player != null)
        {
            var r = Math.Max(5, _state.Player.Radius * _scale);
            _playerEllipse = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(_state.Player.Color),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 150, 230, 250)),
                StrokeThickness = 1.5,
                ToolTip = "탐사선"
            };
            SimCanvas.Children.Add(_playerEllipse);
            UpdateEllipsePosition(_playerEllipse, _state.Player.Position, r);
        }

        UpdatePredictLine();
    }

    private void UpdateRender()
    {
        if (_state == null) return;

        foreach (var (b, el) in _bodyEllipses)
        {
            var r = Math.Max(4, b.Radius * _scale);
            UpdateEllipsePosition(el, b.Position, r);

            // Target SOI 갱신 (Target이 움직이는 경우)
        }

        if (_playerEllipse != null && _state.Player != null)
        {
            var r = Math.Max(5, _state.Player.Radius * _scale);
            UpdateEllipsePosition(_playerEllipse, _state.Player.Position, r);
        }

        // 매 10틱마다 예측선 갱신
        if ((int)(_state.ElapsedTime / Simulator.BaseTimeStep) % 10 == 0)
            UpdatePredictLine();

        // Target SOI 원 위치 갱신
        foreach (UIElement el in SimCanvas.Children)
        {
            if (el is Ellipse soiEl && soiEl.StrokeDashArray?.Count > 0 && _state.Target != null)
            {
                var soiR = _state.Target.SOI * _scale;
                UpdateEllipsePosition(soiEl, _state.Target.Position, soiR);
                break;
            }
        }
    }

    private void UpdateEllipsePosition(Ellipse el, Vector2D pos, double r)
    {
        var cx = _canvasW / 2 + _offsetX;
        var cy = _canvasH / 2 + _offsetY;
        var px = cx + pos.X * _scale;
        var py = cy - pos.Y * _scale;
        Canvas.SetLeft(el, px - r);
        Canvas.SetTop(el, py - r);
    }

    private void UpdatePredictLine()
    {
        if (_predictLine == null || _state?.Player == null) return;
        var path = Simulator.PredictOrbit(_state, PredictSteps, Simulator.BaseTimeStep * 10);
        var pts = new PointCollection();
        var cx = _canvasW / 2 + _offsetX;
        var cy = _canvasH / 2 + _offsetY;
        foreach (var p in path)
            pts.Add(new Point(cx + p.X * _scale, cy - p.Y * _scale));
        _predictLine.Points = pts;
    }

    // ══════════════════════════════════════════════════════
    //  HUD 업데이트
    // ══════════════════════════════════════════════════════

    private void UpdateHUD()
    {
        if (_state == null) return;
        var days = (int)(_state.ElapsedTime / 86400);
        LblTime.Text = $"T+{days}d";

        if (_state.Player != null)
        {
            var speed = _state.Player.Velocity.Length;
            LblPlayerSpeed.Text = $"속도: {speed / 1000:F1} km/s";
            if (_state.Target != null)
            {
                var dist = (_state.Player.Position - _state.Target.Position).Length;
                LblPlayerDist.Text = $"목표까지: {dist / 1e9:F1} Gm";
            }
        }

        // 천체 정보 업데이트
        var infoList = _state.Bodies.Select(b => new BodyInfoItem
        {
            Name = (b.Type == BodyType.Target ? "★ " : "") + b.Name,
            Info = b.IsStatic ? "고정" : $"{b.Velocity.Length / 1000:F1} km/s"
        }).ToList();
        BodyInfoList.ItemsSource = infoList;
    }

    // ══════════════════════════════════════════════════════
    //  결과 표시
    // ══════════════════════════════════════════════════════

    private void ShowResult(GameResult result)
    {
        ResultOverlay.Visibility = Visibility.Visible;
        switch (result)
        {
            case GameResult.Success:
                LblResultTitle.Text = "궤도 진입 성공!";
                LblResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(80, 220, 100));
                LblResultSub.Text = $"레벨 {_currentLevel} 클리어";
                BtnResultAction.Content = _currentLevel < MaxLevels ? "다음 레벨 ▶" : "처음으로";
                StatusBar.Text = $"레벨 {_currentLevel} 성공! 경과 시간: {_state!.ElapsedTime / 86400:F1}일";
                break;
            case GameResult.Collision:
                LblResultTitle.Text = "충돌!";
                LblResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                LblResultSub.Text = "천체와 충돌했습니다";
                BtnResultAction.Content = "↺ 다시하기";
                break;
            case GameResult.Escaped:
                LblResultTitle.Text = "탈출!";
                LblResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(220, 160, 80));
                LblResultSub.Text = "시스템을 벗어났습니다";
                BtnResultAction.Content = "↺ 다시하기";
                break;
        }
        LblStatus.Text = result == GameResult.Success ? "성공!" : "실패";
    }

    private void BtnResultAction_Click(object sender, RoutedEventArgs e)
    {
        if (_state?.Result == GameResult.Success && _currentLevel < MaxLevels)
            _currentLevel++;
        LoadLevel(_currentLevel);
    }

    // ══════════════════════════════════════════════════════
    //  입력
    // ══════════════════════════════════════════════════════

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space: ToggleSimulation(); break;
            case Key.R: LoadLevel(_currentLevel); break;
        }
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e) => LoadLevel(_currentLevel);
    private void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLevel > 1) { _currentLevel--; LoadLevel(_currentLevel); }
    }
    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLevel < MaxLevels) { _currentLevel++; LoadLevel(_currentLevel); }
    }

    // 마우스 휠 줌
    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        _scale *= factor;
        RebuildCanvas();
    }

    // 마우스 드래그 패닝
    private bool _dragging = false;
    private Point _dragStart;
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _dragging = true;
            _dragStart = e.GetPosition(SimCanvas);
            SimCanvas.CaptureMouse();
        }
    }
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var pos = e.GetPosition(SimCanvas);
        _offsetX += pos.X - _dragStart.X;
        _offsetY += pos.Y - _dragStart.Y;
        _dragStart = pos;
        UpdateRender();
        UpdatePredictLine();
    }
}

// HUD 데이터 클래스
file class BodyInfoItem
{
    public string Name { get; set; } = string.Empty;
    public string Info { get; set; } = string.Empty;
}
