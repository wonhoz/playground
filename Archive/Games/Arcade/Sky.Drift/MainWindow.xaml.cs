namespace SkyDrift;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 상수 ──────────────────────────────────────────────────────────────
    private const double CanvasW     = 560;
    private const double CanvasH     = 740;
    private const double GliderStartX = 280;
    private const double GliderScreenY = 540;  // 글라이더 화면상 Y 고정 위치 (아래쪽)
    private const double GroundY      = -50;   // 이 고도 이하 = 추락

    // ── 엔진 ──────────────────────────────────────────────────────────────
    private readonly GameLoop          _loop    = new();
    private readonly GliderPhysics     _glider  = new();
    private readonly ScrollEnvironment _env     = new();

    // ── 상태 ──────────────────────────────────────────────────────────────
    private enum GameState { Title, Playing, GameOver }
    private GameState _state = GameState.Title;

    private double _altitude;   // 현재 고도 (세계 좌표)
    private double _maxAlt;     // 이번 판 최고 고도
    private double _bestAlt;    // 역대 최고 고도
    private double _playTime;   // 생존 시간 (초)
    private bool   _keyLeft, _keyRight;

    // ── 비주얼 ────────────────────────────────────────────────────────────
    private Polygon?   _gliderShape;
    private readonly List<UIElement> _envElements = [];
    private readonly List<Line>      _bgLines     = [];

    // 색상
    private static readonly SolidColorBrush BrSky       = Frozen(0x05, 0x0F, 0x23);
    private static readonly SolidColorBrush BrGlider    = Frozen(0x64, 0xC8, 0xFF);
    private static readonly SolidColorBrush BrGliderWing= Frozen(0x3A, 0x8A, 0xFF);
    private static readonly SolidColorBrush BrThermal   = Frozen(0x20, 0x80, 0x20, 30);
    private static readonly SolidColorBrush BrDowndraft = Frozen(0x50, 0x20, 0x20, 25);
    private static readonly SolidColorBrush BrBird      = Frozen(0xCC, 0xCC, 0x88);
    private static readonly SolidColorBrush BrStorm     = Frozen(0x22, 0x22, 0x44);
    private static readonly SolidColorBrush BrWind      = Frozen(0x20, 0x40, 0x60, 40);
    private static readonly SolidColorBrush BrBgLine    = Frozen(0x10, 0x20, 0x40, 60);

    private static SolidColorBrush Frozen(byte r, byte g, byte b, byte a = 255)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource src)
            {
                int v = 1;
                DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int));
            }

            BuildBackground();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 배경 그리드 ───────────────────────────────────────────────────────

    private void BuildBackground()
    {
        for (int i = 0; i <= 8; i++)
        {
            var line = new Line
            {
                X1 = i * 70.0, Y1 = 0, X2 = i * 70.0, Y2 = CanvasH,
                Stroke = BrBgLine, StrokeThickness = 0.5,
            };
            _bgLines.Add(line);
            GameCanvas.Children.Add(line);
        }
    }

    private void ScrollBackground(double altitudeDelta)
    {
        // 배경 그리드 수직 스크롤 (패럴랙스)
        foreach (var l in _bgLines)
        {
            double shift = altitudeDelta * 0.05 % CanvasH;
            l.Y1 = (l.Y1 + shift) % CanvasH;
            l.Y2 = l.Y1 + CanvasH;
        }
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────

    private void StartGame()
    {
        _state    = GameState.Playing;
        _altitude = 0;
        _maxAlt   = 0;
        _playTime = 0;

        _glider.Init(GliderStartX);
        _env.Reset();
        _env.Update(_altitude, CanvasW, CanvasH);

        // 기존 환경 비주얼 제거
        foreach (var el in _envElements) GameCanvas.Children.Remove(el);
        _envElements.Clear();

        // 글라이더 생성
        if (_gliderShape != null) GameCanvas.Children.Remove(_gliderShape);
        _gliderShape = CreateGliderShape();
        GameCanvas.Children.Add(_gliderShape);

        TitlePanel.Visibility   = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        HudPanel.Visibility      = Visibility.Visible;
    }

    // ── 업데이트 ──────────────────────────────────────────────────────────

    private double _prevAltitude;

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        _playTime += dt;

        // 입력 처리
        if (_keyLeft)  _glider.TiltLeft();
        else if (_keyRight) _glider.TiltRight();
        else _glider.ReleaseTilt();

        // 기류 계산
        double lift = _env.GetTotalLift(_glider.X, _altitude);

        // 물리 업데이트
        _glider.Update(dt, lift);

        // 고도 반영 (수직 속도 → 고도)
        _prevAltitude = _altitude;
        _altitude    += _glider.VY * dt;

        // 환경 업데이트
        _env.Update(_altitude, CanvasW, CanvasH);

        // 최고 고도 갱신
        if (_altitude > _maxAlt) _maxAlt = _altitude;

        // 경계 체크
        if (_altitude < GroundY)
        {
            GameOver();
            return;
        }

        if (_glider.X < 10 || _glider.X > CanvasW - 10)
        {
            GameOver();
            return;
        }

        // 장애물 충돌 체크
        foreach (var obs in _env.Obstacles)
        {
            if (obs.Passed) continue;

            // 세계좌표 → 화면 Y
            double screenOY = WorldToScreenY(_altitude, obs.Y);
            if (screenOY < -100 || screenOY > CanvasH + 100) continue;

            if (obs.HitTest(_glider.X, GliderScreenY, 15))
            {
                if (obs.Kind == ObstacleKind.WindZone)
                {
                    // 강풍: 기울기 강제 변경 (위험)
                }
                else
                {
                    GameOver();
                    return;
                }
            }
        }

        // 새떼 이동
        foreach (var obs in _env.Obstacles)
            if (obs.Kind == ObstacleKind.BirdFlock)
                obs.X += obs.VX * dt;

        // HUD 업데이트
        TxtAltitude.Text  = $"{(int)_altitude} m";
        TxtSpeed.Text     = $"VY: {_glider.VY:+0.0;-0.0} m/s  |  VX: {_glider.VX:F0} m/s";

        double liftNow = lift;
        TxtLiftStatus.Text = liftNow > 15 ? "↑ 상승기류!" :
                             liftNow < -10 ? "↓ 하강기류!" : "";
        TxtLiftStatus.Foreground = liftNow > 15
            ? new SolidColorBrush(Color.FromRgb(0x6E, 0xFF, 0x6E))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
    }

    // ── 렌더 ──────────────────────────────────────────────────────────────

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // 환경 요소 재구성
        foreach (var el in _envElements) GameCanvas.Children.Remove(el);
        _envElements.Clear();

        // 열기류 시각화
        foreach (var t in _env.Thermals)
        {
            double sy = WorldToScreenY(_altitude, t.Y);
            if (sy < -t.Radius || sy > CanvasH + t.Radius) continue;

            var ell = new Ellipse
            {
                Width  = t.Radius * 2,
                Height = t.Radius * 2,
                Fill   = t.Type == ThermalType.Rising ? BrThermal : BrDowndraft,
            };
            Canvas.SetLeft(ell, t.X - t.Radius);
            Canvas.SetTop(ell,  sy  - t.Radius);
            GameCanvas.Children.Add(ell);
            _envElements.Add(ell);

            // 아지랑이 표시 (상승 기류)
            if (t.Type == ThermalType.Rising)
            {
                for (int i = 0; i < 3; i++)
                {
                    var wave = new Ellipse
                    {
                        Width  = t.Radius * 0.4,
                        Height = t.Radius * 0.4,
                        Stroke = new SolidColorBrush(Color.FromArgb(60, 0x6E, 0xFF, 0x6E)),
                        StrokeThickness = 1,
                        Fill   = Brushes.Transparent,
                    };
                    Canvas.SetLeft(wave, t.X - t.Radius * 0.2 + (i - 1) * t.Radius * 0.4);
                    Canvas.SetTop(wave,  sy + (i * 8) - t.Radius * 0.3);
                    GameCanvas.Children.Add(wave);
                    _envElements.Add(wave);
                }
            }
        }

        // 장애물 시각화
        foreach (var obs in _env.Obstacles)
        {
            if (obs.Passed) continue;
            double sy = WorldToScreenY(_altitude, obs.Y);
            if (sy < -200 || sy > CanvasH + 100) continue;

            UIElement el = obs.Kind switch
            {
                ObstacleKind.BirdFlock  => CreateBirdFlock(obs.X, sy, obs.Width),
                ObstacleKind.StormCloud => CreateStormCloud(obs.X, sy, obs.Width, obs.Height),
                _                       => CreateWindZone(obs.X, sy, obs.Width, obs.Height),
            };
            GameCanvas.Children.Add(el);
            _envElements.Add(el);
        }

        // 글라이더 위치 업데이트
        if (_gliderShape != null)
        {
            // 기울기에 따라 회전
            var transform = new RotateTransform(_glider.Tilt, 0, 0);
            _gliderShape.RenderTransform = transform;
            Canvas.SetLeft(_gliderShape, _glider.X - 20);
            Canvas.SetTop(_gliderShape,  GliderScreenY - 12);
        }

        // 배경 스크롤
        double altDelta = _altitude - _prevAltitude;
        ScrollBackground(altDelta);
    }

    private double WorldToScreenY(double cameraAlt, double worldY)
    {
        // 글라이더는 GliderScreenY에 고정, 세계가 아래로 스크롤
        double relAlt = worldY - cameraAlt;
        return GliderScreenY - relAlt;
    }

    // ── 게임 오버 ─────────────────────────────────────────────────────────

    private void GameOver()
    {
        _state = GameState.GameOver;
        _loop.Stop();

        bool isNewBest = _maxAlt > _bestAlt;
        if (isNewBest) _bestAlt = _maxAlt;

        TxtFinalAlt.Text  = $"최고 고도: {(int)_maxAlt} m";
        TxtFinalTime.Text = $"생존 시간: {(int)_playTime / 60:D2}:{_playTime % 60:00.0}";
        TxtNewBest.Text   = isNewBest ? "★ 신기록!" : "";
        TxtNewBest.Visibility = isNewBest ? Visibility.Visible : Visibility.Collapsed;

        // 최고 기록 저장
        if (_bestAlt > 0)
        {
            TxtBestScore.Text = $"최고 기록: {(int)_bestAlt} m";
            BdrBestScore.Visibility = Visibility.Visible;
        }

        HudPanel.Visibility      = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Visible;
    }

    // ── 비주얼 헬퍼 ───────────────────────────────────────────────────────

    private static Polygon CreateGliderShape()
    {
        // 삼각형 글라이더 (날개 모양)
        return new Polygon
        {
            Points = new PointCollection
            {
                new(20, 0),   // 기수
                new(0, 24),   // 좌측 날개 끝
                new(12, 18),  // 좌측 몸통
                new(20, 20),  // 꼬리 중앙
                new(28, 18),  // 우측 몸통
                new(40, 24),  // 우측 날개 끝
            },
            Fill            = BrGlider,
            Stroke          = BrGliderWing,
            StrokeThickness = 1.5,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
    }

    private static UIElement CreateBirdFlock(double x, double y, double w)
    {
        var canvas = new Canvas { Width = w, Height = 20 };
        var rng = new Random((int)(x * 7));
        for (int i = 0; i < 6; i++)
        {
            double bx = rng.NextDouble() * w;
            double by = rng.NextDouble() * 16;
            // 새: V자 선
            var bird = new Polyline
            {
                Points = new PointCollection { new(bx - 4, by - 2), new(bx, by + 2), new(bx + 4, by - 2) },
                Stroke = BrBird, StrokeThickness = 1.5,
            };
            canvas.Children.Add(bird);
        }
        Canvas.SetLeft(canvas, x - w / 2);
        Canvas.SetTop(canvas,  y - 10);
        return canvas;
    }

    private static UIElement CreateStormCloud(double x, double y, double w, double h)
    {
        var ell = new Ellipse
        {
            Width  = w,
            Height = h,
            Fill   = BrStorm,
            Stroke = new SolidColorBrush(Color.FromArgb(100, 0x44, 0x44, 0x88)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(ell, x - w / 2);
        Canvas.SetTop(ell,  y - h / 2);
        return ell;
    }

    private static UIElement CreateWindZone(double x, double y, double w, double h)
    {
        var rect = new Rectangle
        {
            Width  = w,
            Height = h,
            Fill   = BrWind,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 0x40, 0x80, 0xFF)),
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
        };
        Canvas.SetLeft(rect, x - w / 2);
        Canvas.SetTop(rect,  y - h / 2);
        return rect;
    }

    // ── 입력 ──────────────────────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A:
                _keyLeft = true; break;
            case Key.Right or Key.D:
                _keyRight = true; break;
            case Key.Return:
                if (_state is GameState.Title or GameState.GameOver)
                {
                    _loop.Start();
                    StartGame();
                }
                break;
            case Key.Escape:
                if (_state == GameState.Playing)
                    GameOver();
                else if (_state == GameState.GameOver)
                {
                    _state = GameState.Title;
                    GameOverPanel.Visibility = Visibility.Collapsed;
                    TitlePanel.Visibility    = Visibility.Visible;
                }
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.A)  _keyLeft  = false;
        if (e.Key is Key.Right or Key.D) _keyRight = false;
    }
}
