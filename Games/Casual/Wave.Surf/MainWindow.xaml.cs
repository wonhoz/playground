using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WaveSurf.Engine;

namespace WaveSurf;

public partial class MainWindow : Window
{
    // ── P/Invoke (다크 타이틀바) ─────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 게임 상태 ────────────────────────────────────────────────────────────
    private enum GameState { Title, Playing, GameOver }
    private GameState _state = GameState.Title;

    // ── 엔진 ────────────────────────────────────────────────────────────────
    private readonly GameLoop      _loop    = new();
    private readonly WavePhysics   _wave    = new();
    private readonly SurferPhysics _surfer  = new();
    private readonly TrickSystem   _tricks  = new();

    // ── 렌더링 캐시 ─────────────────────────────────────────────────────────
    private readonly StreamGeometry _waveGeo = new();
    private readonly PointCollection _foamPoints = [];
    private double _cw, _ch; // canvas width / height

    // ── 테마 ────────────────────────────────────────────────────────────────
    private int _themeIdx = 0;
    private WaveTheme Theme => WaveTheme.All[_themeIdx];

    // ── 세션 타이머 ─────────────────────────────────────────────────────────
    private double _sessionSec;
    private int    _maxCombo;

    // ── 묘기 배너 ─────────────────────────────────────────────────────────
    private int    _trickBannerTimer; // 프레임 카운트
    private int    _captureFrame = -1; // 캡처 예약 프레임

    // ── 파티클 (와이프아웃 물보라) ──────────────────────────────────────────
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)> _particles = [];

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        _cw = GameCanvas.ActualWidth;
        _ch = GameCanvas.ActualHeight;
        _wave.CanvasWidth  = _cw;
        _wave.CanvasHeight = _ch;

        // WaveGeo를 Path에 연결
        WavePath.Data = _waveGeo;
        WavePath.Fill = new SolidColorBrush(Theme.WaveLight);

        // 캔버스 크기 변화 대응
        GameCanvas.SizeChanged += (_, _) =>
        {
            _cw = GameCanvas.ActualWidth;
            _ch = GameCanvas.ActualHeight;
            _wave.CanvasWidth  = _cw;
            _wave.CanvasHeight = _ch;
            LayoutHud();
        };
        LayoutHud();

        // 타이틀 화면에서도 파도 애니메이션
        _loop.OnTick += Tick;
        _loop.Start();
    }

    // ── 게임 시작/재시작 ─────────────────────────────────────────────────────
    private void StartGame()
    {
        _wave.Reset();
        _surfer.ResetOnWave(_wave);
        _tricks.Reset();
        _sessionSec = 0;
        _maxCombo = 0;
        _particles.Clear();

        TitleScreen.Visibility   = Visibility.Collapsed;
        GameOverScreen.Visibility = Visibility.Collapsed;
        TrickBanner.Visibility    = Visibility.Collapsed;
        TxtWipeout.Visibility     = Visibility.Collapsed;
        TxtCombo.Text = "";
        TxtAirTrick.Text = "";

        _state = GameState.Playing;
    }

    // ── 메인 틱 ──────────────────────────────────────────────────────────────
    private void Tick(double dt)
    {
        _wave.Update(dt);

        if (_state == GameState.Playing)
            UpdateGame(dt);

        Render();
        UpdateHud();
    }

    private void UpdateGame(double dt)
    {
        bool wasAir = _surfer.IsAirborne;

        // 입력 전달
        _surfer.LeanLeft  = _leanLeft;
        _surfer.LeanRight = _leanRight;
        _surfer.SpinLeft  = _spinLeft;
        _surfer.SpinRight = _spinRight;

        _surfer.Update(dt, _wave);

        // 공중에서 착지 직후 체크 (Land()는 SurferPhysics 내부에서 호출)
        if (wasAir && !_surfer.IsAirborne && _surfer.State == SurferState.OnWave)
            OnCleanLanding();

        // 와이프아웃 전환
        if (_surfer.State == SurferState.Wiped)
            OnWipeout();

        // 생존 점수
        if (_surfer.State == SurferState.OnWave || _surfer.State == SurferState.InAir)
        {
            _tricks.AddSurvivalScore(dt);
            _sessionSec += dt;
        }

        // 파티클 업데이트
        UpdateParticles(dt);

        // 배너 타이머
        if (_trickBannerTimer > 0)
        {
            _trickBannerTimer--;
            if (_trickBannerTimer == 0)
                TrickBanner.Visibility = Visibility.Collapsed;
        }

        // 캡처 예약 처리 (착지 다음 프레임에 캡처)
        if (_captureFrame == 0)
        {
            DoCapture();
            _captureFrame = -1;
        }
        else if (_captureFrame > 0) _captureFrame--;
    }

    private void OnCleanLanding()
    {
        var result = _tricks.ProcessLanding(_surfer.TrickRotation);
        if (result == null) return;

        // 배너 표시
        TxtTrickName.Text  = result.Name;
        TxtTrickScore.Text = $"+{result.FinalScore:N0}  ×{result.Multiplier:F1}콤보";
        TrickBanner.Visibility = Visibility.Visible;
        _trickBannerTimer  = 150; // ~2.5초

        // 캡처 1프레임 후 예약
        _captureFrame = 1;

        if (result.Combo > _maxCombo) _maxCombo = result.Combo;
    }

    private void OnWipeout()
    {
        _tricks.BreakCombo();

        // 물보라 파티클 생성
        SpawnSplash();

        _state = GameState.GameOver;

        TxtFinalScore.Text = _tricks.TotalScore.ToString("N0");
        TxtFinalCombo.Text = $"{_maxCombo}콤보";
        TxtFinalTime.Text  = FormatTime(_sessionSec);
        GameOverScreen.Visibility = Visibility.Visible;
        TrickBanner.Visibility    = Visibility.Collapsed;
    }

    // ── 렌더링 ───────────────────────────────────────────────────────────────
    private void Render()
    {
        RenderWave();
        RenderSurfer();
        RenderParticles();
    }

    private void RenderWave()
    {
        if (_cw <= 0 || _ch <= 0) return;

        const int step = 4;
        int count = (int)(_cw / step) + 2;

        // StreamGeometry 갱신 (파도 수체 Path)
        using (var ctx = _waveGeo.Open())
        {
            double y0 = _wave.SurfaceY(0);
            ctx.BeginFigure(new Point(0, y0), isFilled: true, isClosed: true);

            var pts = new Point[count];
            for (int i = 0; i < count; i++)
            {
                double x = i * step;
                pts[i] = new Point(x, _wave.SurfaceY(x));
            }
            ctx.PolyLineTo(pts, isStroked: false, isSmoothJoin: true);
            ctx.LineTo(new Point(_cw, _ch), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(0, _ch), isStroked: false, isSmoothJoin: false);
        }

        // 거품 선 (Polyline)
        var foamPts = new PointCollection(count);
        for (int i = 0; i < count; i++)
        {
            double x = i * step;
            foamPts.Add(new Point(x, _wave.SurfaceY(x)));
        }
        FoamLine.Points = foamPts;
    }

    private void RenderSurfer()
    {
        double sx = _surfer.ScreenX;
        double sy = _surfer.ScreenY;
        double angle = _surfer.Angle * 180.0 / Math.PI;

        // SurferCanvas를 서퍼 위치 중심으로 이동 + 회전
        double canvasW = SurferCanvas.Width;
        double canvasH = SurferCanvas.Height;

        Canvas.SetLeft(SurferCanvas, sx - canvasW / 2.0);
        Canvas.SetTop(SurferCanvas,  sy - canvasH / 2.0 - 10);

        // 기존 TransformGroup이 없으면 설정
        if (SurferCanvas.RenderTransform is not TransformGroup)
        {
            SurferCanvas.RenderTransformOrigin = new Point(0.5, 0.65);
            SurferCanvas.RenderTransform = new RotateTransform();
        }
        ((RotateTransform)SurferCanvas.RenderTransform).Angle = angle;

        // 와이프아웃 시 투명도 점진 증가
        SurferCanvas.Opacity = _surfer.State == SurferState.Wiping
            ? Math.Max(0, 1.0 - (1.0 - _surfer.AirVelocityY / 500.0) * 0.3)
            : 1.0;

        // 균형 게이지 색상
        double b = _surfer.Balance;
        byte r = (byte)Math.Clamp(b * 300, 0, 255);
        byte g = (byte)Math.Clamp((1 - Math.Abs(b)) * 255, 60, 255);
        BalanceFill.Fill = new SolidColorBrush(Color.FromRgb(r, g, 60));

        // 균형 게이지 너비 (80px = 범위 전체)
        double fillW = (b + 1.0) / 2.0 * 80;
        BalanceFill.Width = Math.Clamp(fillW, 2, 80);

        // 균형 게이지 위치 (서퍼 아래)
        Canvas.SetLeft(BalanceBg,   sx - 40);
        Canvas.SetTop(BalanceBg,    sy + 14);
        Canvas.SetLeft(BalanceFill, sx - 40);
        Canvas.SetTop(BalanceFill,  sy + 14);
    }

    private void RenderParticles()
    {
        foreach (var (e, _, _, _) in _particles)
        {
            // 위치는 UpdateParticles에서 설정됨
        }
    }

    // ── HUD 갱신 ─────────────────────────────────────────────────────────────
    private void UpdateHud()
    {
        TxtScore.Text = _tricks.TotalScore.ToString("N0");
        TxtTime.Text  = FormatTime(_sessionSec);

        int combo = _tricks.Combo;
        TxtCombo.Text = combo >= 2 ? $"×{_tricks.Multiplier:F1} {combo}콤보" : "";

        // 공중 묘기 미리보기
        TxtAirTrick.Text = _surfer.CurrentAirTrick();

        // 와이프아웃 텍스트
        TxtWipeout.Visibility = _surfer.State == SurferState.Wiping
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LayoutHud()
    {
        // SkyRect: 전체 화면
        SkyRect.Width  = _cw;
        SkyRect.Height = _ch;

        // 점수 (중앙 상단)
        TxtScore.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(TxtScore, (_cw - TxtScore.DesiredSize.Width) / 2);

        // 시간 (우측 상단)
        TxtTime.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(TxtTime, _cw - TxtTime.DesiredSize.Width - 18);

        // 묘기 배너 (중앙)
        TrickBanner.Width = _cw;
        Canvas.SetLeft(TrickBanner, 0);
        Canvas.SetTop(TrickBanner, _ch * 0.38);

        // 공중 묘기 (중앙)
        TxtAirTrick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(TxtAirTrick, (_cw - TxtAirTrick.DesiredSize.Width) / 2);

        // 와이프아웃 (중앙)
        TxtWipeout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(TxtWipeout, (_cw - TxtWipeout.DesiredSize.Width) / 2);
        Canvas.SetTop(TxtWipeout,  _ch * 0.45);

        // 화면 크기 패널들
        TitleScreen.Width  = _cw;
        TitleScreen.Height = _ch;
        GameOverScreen.Width  = _cw;
        GameOverScreen.Height = _ch;

        // 테마/힌트 (하단)
        TxtTheme.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(TxtTheme, _cw - TxtTheme.DesiredSize.Width - 18);
    }

    // ── 테마 ────────────────────────────────────────────────────────────────
    private void ApplyTheme()
    {
        var t = Theme;
        SkyGradTop.Color   = t.SkyTop;
        SkyGradBot.Color   = t.SkyBottom;
        WavePath.Fill      = new SolidColorBrush(t.WaveLight);
        FoamLine.Stroke    = new SolidColorBrush(t.FoamColor);
        Board.Fill         = new SolidColorBrush(t.SurferColor);
        TxtTheme.Text      = t.Name;
        TxtScore.Foreground = new SolidColorBrush(t.HudColor);
        TxtTime.Foreground  = new SolidColorBrush(t.HudColor);
    }

    private void CycleTheme()
    {
        _themeIdx = (_themeIdx + 1) % WaveTheme.All.Length;
        ApplyTheme();
    }

    // ── 파티클 (물보라) ──────────────────────────────────────────────────────
    private void SpawnSplash()
    {
        var rng = new Random();
        for (int i = 0; i < 16; i++)
        {
            double angle  = rng.NextDouble() * Math.PI; // 위쪽 반구
            double speed  = 80 + rng.NextDouble() * 200;
            double vx     = Math.Cos(angle) * speed;
            double vy     = -Math.Abs(Math.Sin(angle)) * speed;
            double size   = 4 + rng.NextDouble() * 8;

            var e = new Ellipse
            {
                Width  = size,
                Height = size,
                Fill   = new SolidColorBrush(Color.FromArgb(200, 160, 220, 255))
            };
            Canvas.SetLeft(e, _surfer.ScreenX - size / 2);
            Canvas.SetTop(e,  _surfer.ScreenY - size / 2);
            GameCanvas.Children.Add(e);
            _particles.Add((e, vx, vy, 1.0));
        }
    }

    private void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (e, vx, vy, life) = _particles[i];
            double nx = Canvas.GetLeft(e) + vx * dt;
            double ny = Canvas.GetTop(e)  + (vy + 300 * dt) * dt;
            double newLife = life - dt * 1.2;

            Canvas.SetLeft(e, nx);
            Canvas.SetTop(e, ny);
            e.Opacity = Math.Clamp(newLife, 0, 1);

            if (newLife <= 0)
            {
                GameCanvas.Children.Remove(e);
                _particles.RemoveAt(i);
            }
            else
            {
                _particles[i] = (e, vx, vy + 300 * dt, newLife);
            }
        }
    }

    // ── 캡처 ────────────────────────────────────────────────────────────────
    private void DoCapture()
    {
        var path = ScreenCapture.Capture(GameCanvas,
            (int)GameCanvas.ActualWidth, (int)GameCanvas.ActualHeight);
        // 성공 시 간단 토스트 (HUD 텍스트 일시 표시)
        if (path != null)
            ShowCaptureToast();
    }

    private void ShowCaptureToast()
    {
        TxtHint.Text    = "📷 캡처 저장됨!";
        TxtHint.Opacity = 0.9;
        var anim = new DoubleAnimation(0.9, 0.3, TimeSpan.FromSeconds(2));
        anim.Completed += (_, _) =>
            TxtHint.Text = "← → 균형 | 공중에서 ← → 회전 | T 테마 변경";
        TxtHint.BeginAnimation(OpacityProperty, anim);
    }

    // ── 입력 ────────────────────────────────────────────────────────────────
    private bool _leanLeft, _leanRight, _spinLeft, _spinRight;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                _leanLeft = true; _spinLeft = true; break;
            case Key.Right:
                _leanRight = true; _spinRight = true; break;
            case Key.Space:
                if (_state != GameState.Playing) StartGame();
                break;
            case Key.T:
                CycleTheme(); break;
            case Key.F1:
                ScreenCapture.OpenFolder(); break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:  _leanLeft  = false; _spinLeft  = false; break;
            case Key.Right: _leanRight = false; _spinRight = false; break;
        }
    }

    private void StartBtn_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => StartGame();

    // ── 유틸 ────────────────────────────────────────────────────────────────
    private static string FormatTime(double sec)
    {
        int m = (int)(sec / 60);
        int s = (int)(sec % 60);
        return $"{m}:{s:D2}";
    }
}
