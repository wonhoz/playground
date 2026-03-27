using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OrbitCraft.Engine;
using OrbitCraft.Levels;

namespace OrbitCraft;

public partial class MainWindow : Window
{
    // ── 화면 상수 ──────────────────────────────────────────
    private const double ViewW      = 844;
    private const double ViewH      = 520;
    private const double MaxSimTime = 120.0;

    // ── 게임 상태 ──────────────────────────────────────────
    private enum GameState { Title, LevelSelect, Aim, Running, Clear, GameComplete }
    private GameState _state         = GameState.Title;
    private int       _curLevel      = 1;
    private int       _selectedLevel = 1;
    private bool      _helpVisible   = false;

    // ── 레벨 데이터 ────────────────────────────────────────
    private LevelDef     _levelDef     = null!;
    private List<Planet> _planets      = [];
    private Planet       _targetPlanet = null!;
    private Probe        _probe        = new();

    // ── 조준 상태 (Aim) ───────────────────────────────────
    private double _probeStartX, _probeStartY;
    private double _aimX, _aimY;
    private double _speedMultiplier = 1.0;
    private int    _launchCount     = 0;

    // ── 시뮬레이션 상태 (Running) ─────────────────────────
    private double _simX, _simY, _simVx, _simVy;
    private double _prevAngle, _totalAngle;
    private int    _revolutions;
    private double _simTime;

    // ── 렌더링 요소 ────────────────────────────────────────
    private readonly List<(Planet P, Ellipse Glow, Ellipse Body, TextBlock Lbl)> _planetVis = [];
    private readonly List<Ellipse>                                                 _stars     = [];
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)>         _particles = [];
    private Polyline? _previewLine;
    private Polyline? _trailLine;
    private Polygon?  _shipPoly;
    private Line?     _aimLine;

    // ── 레벨 셀렉트 UI ─────────────────────────────────────
    private readonly List<TextBlock> _levelItems = [];

    // ── 저장 데이터 ────────────────────────────────────────
    private readonly SaveData _saveData = SaveData.Load();

    // ── 게임루프 ───────────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly Random   _rng  = new();
    private double _animTime;

    // ── 색상 ───────────────────────────────────────────────
    private static readonly Color ColUI     = Color.FromRgb(0x5E, 0xEA, 0xD4); // UI 강조 (청록)
    private static readonly Color ColStar   = Color.FromRgb(0xFF, 0xD0, 0x60); // 항성 (금)
    private static readonly Color ColPlanet = Color.FromRgb(0x5A, 0x9A, 0xCA); // 행성 (청)
    private static readonly Color ColTarget = Color.FromRgb(0x00, 0xFF, 0xC0); // 목표 (청록)
    private static readonly Color ColProbe  = Color.FromRgb(0xFF, 0xF0, 0xA0); // 탐사선 (황)
    private static readonly Color ColPreview= Color.FromRgb(0x88, 0xCC, 0xFF); // 예측선 (하늘)
    private static readonly Color ColTrail  = Color.FromRgb(0xFF, 0xD0, 0x60); // 궤적 (금)
    private static readonly Color ColAim    = Color.FromRgb(0x5E, 0xEA, 0xD4); // 조준선 (청록)

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            BuildTitleStarfield();
            Focus();
        };
    }

    // ── 레벨 로드 ─────────────────────────────────────────

    private void LoadLevel(int level)
    {
        ClearLevel();
        _levelDef    = LevelData.Get(level);
        _curLevel    = level;
        _state       = GameState.Aim;
        _animTime    = 0;
        _launchCount = 0;

        _planets.Clear();
        foreach (var pd in _levelDef.Planets)
            _planets.Add(new Planet
            {
                X = pd.X, Y = pd.Y,
                Mass = pd.Mass, Radius = pd.Radius,
                IsTarget = pd.IsTarget, Name = pd.Name
            });
        _targetPlanet = _planets[_levelDef.TargetPlanetIdx];

        _probeStartX = _levelDef.ProbeX;
        _probeStartY = _levelDef.ProbeY;
        ResetAim();
        _probe.Reset(_probeStartX, _probeStartY, 0, 0);

        BuildStarfield();
        BuildPlanetVisuals();
        BuildShipVisual();
        BuildAimVisual();

        HudPanel.Visibility           = Visibility.Visible;
        HintBar.Visibility            = Visibility.Visible;
        TitlePanel.Visibility         = Visibility.Collapsed;
        LevelSelectOverlay.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility      = Visibility.Collapsed;
        ModeText.Text       = "[ AIM ]";
        ModeText.Foreground = new SolidColorBrush(ColUI);
        UpdateHud();
        UpdateHintBar();
        RebuildPreview();
    }

    private void ResetAim()
    {
        _aimX = _probeStartX + _levelDef.DefaultVx;
        _aimY = _probeStartY + _levelDef.DefaultVy;
    }

    // ── 타이틀 별빛 ───────────────────────────────────────

    private void BuildTitleStarfield()
    {
        GameCanvas.Children.Clear();
        _stars.Clear();
        for (int i = 0; i < 180; i++)
        {
            double x = _rng.NextDouble() * ViewW;
            double y = _rng.NextDouble() * ViewH;
            double r = 0.4 + _rng.NextDouble() * 1.8;
            byte   a = (byte)(30 + _rng.Next(200));
            byte   g = (byte)(180 + _rng.Next(76));
            var star = new Ellipse
            {
                Width  = r * 2, Height = r * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(a, g, g, 255)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(star, x - r); Canvas.SetTop(star, y - r);
            Canvas.SetZIndex(star, 0);
            GameCanvas.Children.Add(star);
            _stars.Add(star);
        }
    }

    // ── 레벨 중 배경 별빛 ────────────────────────────────

    private void BuildStarfield()
    {
        _stars.Clear();
        for (int i = 0; i < 160; i++)
        {
            double x = _rng.NextDouble() * ViewW;
            double y = _rng.NextDouble() * ViewH;
            double r = 0.4 + _rng.NextDouble() * 1.8;
            byte   a = (byte)(30 + _rng.Next(200));
            byte   g = (byte)(180 + _rng.Next(76));
            var star = new Ellipse
            {
                Width  = r * 2, Height = r * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(a, g, g, 255)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(star, x - r); Canvas.SetTop(star, y - r);
            Canvas.SetZIndex(star, 0);
            GameCanvas.Children.Add(star);
            _stars.Add(star);
        }
    }

    // ── 행성 비주얼 ───────────────────────────────────────

    private void BuildPlanetVisuals()
    {
        _planetVis.Clear();
        foreach (var p in _planets)
        {
            var baseCol = p.IsTarget ? ColTarget
                        : p.Mass >= 100 ? ColStar
                        : ColPlanet;

            double glowR = p.Radius * 2.2;
            var glow = new Ellipse
            {
                Width  = glowR * 2, Height = glowR * 2,
                Fill   = new RadialGradientBrush(
                    Color.FromArgb(100, baseCol.R, baseCol.G, baseCol.B),
                    Color.FromArgb(0,   baseCol.R, baseCol.G, baseCol.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, p.X - glowR); Canvas.SetTop(glow, p.Y - glowR);
            Canvas.SetZIndex(glow, 10);
            GameCanvas.Children.Add(glow);

            var body = new Ellipse
            {
                Width  = p.Radius * 2, Height = p.Radius * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(240, baseCol.R, baseCol.G, baseCol.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness  = p.IsTarget ? 1.5 : 0.8,
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                    { Color = baseCol, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(body, p.X - p.Radius); Canvas.SetTop(body, p.Y - p.Radius);
            Canvas.SetZIndex(body, 20);
            GameCanvas.Children.Add(body);

            if (p.IsTarget)
            {
                double hintR = p.Radius + 12;
                var ring = new Ellipse
                {
                    Width  = hintR * 2, Height = hintR * 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 255, 192)),
                    StrokeDashArray = new DoubleCollection([3, 4]),
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ring, p.X - hintR); Canvas.SetTop(ring, p.Y - hintR);
                Canvas.SetZIndex(ring, 15);
                GameCanvas.Children.Add(ring);
            }

            var lbl = new TextBlock
            {
                Text = p.Name, FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(160,
                    baseCol.R, baseCol.G, baseCol.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, p.X - p.Name.Length * 3.5);
            Canvas.SetTop(lbl,  p.Y + p.Radius + 4);
            Canvas.SetZIndex(lbl, 20);
            GameCanvas.Children.Add(lbl);

            _planetVis.Add((p, glow, body, lbl));
        }
    }

    // ── 탐사선 비주얼 ─────────────────────────────────────

    private void BuildShipVisual()
    {
        _shipPoly = new Polygon
        {
            Points = new PointCollection { new(0, -8), new(5, 5), new(0, 2), new(-5, 5) },
            Fill   = new SolidColorBrush(ColProbe),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 220)),
            StrokeThickness  = 0.7,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = ColProbe, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
        };
        UpdateShipTransform(_probeStartX, _probeStartY,
            _levelDef.DefaultVx, _levelDef.DefaultVy);
        Canvas.SetZIndex(_shipPoly, 40);
        GameCanvas.Children.Add(_shipPoly);

        _trailLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(100, ColTrail.R, ColTrail.G, ColTrail.B)),
            StrokeThickness = 1.2,
            IsHitTestVisible = false
        };
        Canvas.SetZIndex(_trailLine, 25);
        GameCanvas.Children.Add(_trailLine);
    }

    private void UpdateShipTransform(double x, double y, double vx, double vy)
    {
        if (_shipPoly is null) return;
        double angle = (vx == 0 && vy == 0)
            ? 0
            : Math.Atan2(vy, vx) * 180.0 / Math.PI + 90.0;
        var grp = new TransformGroup();
        grp.Children.Add(new RotateTransform(angle));
        grp.Children.Add(new TranslateTransform(x, y));
        _shipPoly.RenderTransform = grp;
    }

    // ── 조준선 비주얼 ─────────────────────────────────────

    private void BuildAimVisual()
    {
        _aimLine = new Line
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, ColAim.R, ColAim.G, ColAim.B)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection([5, 3]),
            IsHitTestVisible = false
        };
        Canvas.SetZIndex(_aimLine, 35);
        GameCanvas.Children.Add(_aimLine);
        RefreshAimLine();
    }

    private void RefreshAimLine()
    {
        if (_aimLine is null) return;
        _aimLine.X1 = _probeStartX; _aimLine.Y1 = _probeStartY;
        _aimLine.X2 = _aimX;        _aimLine.Y2 = _aimY;
    }

    // ── 궤도 예측선 ───────────────────────────────────────

    private void RebuildPreview()
    {
        if (_previewLine is not null)
        {
            GameCanvas.Children.Remove(_previewLine);
            _previewLine = null;
        }

        double vx = (_aimX - _probeStartX) * _speedMultiplier;
        double vy = (_aimY - _probeStartY) * _speedMultiplier;
        if (vx * vx + vy * vy < 0.25) return;

        var pts = OrbitalSim.PreviewOrbit(
            _probeStartX, _probeStartY, vx, vy, _planets);

        _previewLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(55, ColPreview.R, ColPreview.G, ColPreview.B)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection([4, 3]),
            IsHitTestVisible = false
        };
        foreach (var (px, py) in pts)
            _previewLine.Points.Add(new Point(px, py));

        Canvas.SetZIndex(_previewLine, 8);
        GameCanvas.Children.Add(_previewLine);
    }

    // ── 마우스 이동 (조준) ────────────────────────────────

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_state != GameState.Aim) return;
        var pos = e.GetPosition(GameCanvas);
        _aimX = pos.X;
        _aimY = pos.Y;
        RefreshAimLine();
        RebuildPreview();
        UpdateShipTransform(_probeStartX, _probeStartY,
            _aimX - _probeStartX, _aimY - _probeStartY);
        UpdateHud();
    }

    // ── 시뮬레이션 시작 ───────────────────────────────────

    private void StartSimulation()
    {
        if (_state != GameState.Aim) return;
        double vx = (_aimX - _probeStartX) * _speedMultiplier;
        double vy = (_aimY - _probeStartY) * _speedMultiplier;
        if (vx * vx + vy * vy < 0.1) return;

        _launchCount++;
        _simX  = _probeStartX; _simY  = _probeStartY;
        _simVx = vx;           _simVy = vy;
        _probe.Reset(_simX, _simY, _simVx, _simVy);

        _prevAngle   = Math.Atan2(_simY - _targetPlanet.Y, _simX - _targetPlanet.X);
        _totalAngle  = 0;
        _revolutions = 0;
        _simTime     = 0;

        if (_previewLine is not null) _previewLine.Visibility = Visibility.Collapsed;
        if (_aimLine     is not null) _aimLine.Visibility     = Visibility.Collapsed;
        if (_trailLine   is not null) _trailLine.Points.Clear();

        _state = GameState.Running;
        ModeText.Text       = "[ RUN ]";
        ModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0xA0));
        UpdateHud();
        PlayLaunch();
    }

    // ── 물리 스텝 ─────────────────────────────────────────

    private void SimStep(double dt)
    {
        const int SubSteps = 4;
        double sub = dt / SubSteps;

        for (int i = 0; i < SubSteps; i++)
            (_simX, _simY, _simVx, _simVy) =
                OrbitalSim.Step(_simX, _simY, _simVx, _simVy, _planets, sub);

        _simTime += dt;
        _probe.X  = _simX; _probe.Y  = _simY;
        _probe.Vx = _simVx; _probe.Vy = _simVy;
        _probe.Trail.Add((_simX, _simY));
        if (_probe.Trail.Count > Probe.MaxTrail)
            _probe.Trail.RemoveAt(0);

        double newAngle = Math.Atan2(_simY - _targetPlanet.Y, _simX - _targetPlanet.X);
        double dA = newAngle - _prevAngle;
        if (dA >  Math.PI) dA -= 2 * Math.PI;
        if (dA < -Math.PI) dA += 2 * Math.PI;
        _totalAngle  += dA;
        _prevAngle    = newAngle;
        _revolutions  = (int)(Math.Abs(_totalAngle) / (2 * Math.PI));

        if (_revolutions >= _levelDef.RequiredRevolutions)
        {
            EndSimulation(true, "");
            return;
        }
        if (_simTime > MaxSimTime)
        {
            EndSimulation(false, "시간 초과! 궤도를 다시 설계하세요.");
            return;
        }
        if (_simX < -400 || _simX > ViewW + 400 || _simY < -400 || _simY > ViewH + 400)
        {
            EndSimulation(false, "우주 미아! 탈출 속도를 초과했습니다.");
            return;
        }
        foreach (var p in _planets)
        {
            double dx = _simX - p.X, dy = _simY - p.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < p.Radius + 4)
            {
                EndSimulation(false, $"{p.Name}에 충돌!");
                return;
            }
        }
    }

    // ── 시뮬레이션 종료 ───────────────────────────────────

    private void EndSimulation(bool success, string reason)
    {
        _state = success ? (_curLevel >= LevelData.MaxLevel
            ? GameState.GameComplete : GameState.Clear) : GameState.Aim;

        if (success)
        {
            int stars = SaveData.CalcStars(_launchCount);
            var rec   = _saveData.GetOrCreate(_curLevel);
            if (!rec.Cleared || stars > rec.BestStars)
            {
                rec.Cleared    = true;
                rec.BestStars  = stars;
                rec.BestLaunch = _launchCount;
            }
            _saveData.Save();
            PlayClear();
        }
        else
        {
            PlayFail();
        }

        SpawnEndParticles(success);

        var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(success ? 500 : 700) };
        t.Tick += (_, _) => { ShowResult(success, reason); t.Stop(); };
        t.Start();
    }

    private void ShowResult(bool success, string reason)
    {
        if (success)
        {
            int stars      = SaveData.CalcStars(_launchCount);
            StarsText.Text = SaveData.StarsString(stars);

            if (_state == GameState.GameComplete)
            {
                ResultTitle.Text       = "MISSION COMPLETE!";
                ResultTitle.Foreground = new SolidColorBrush(ColTarget);
                ResultGlow.Color       = ColTarget;
                ResultSub.Text         = $"모든 궤도 완성!  발사 {_launchCount}회\n케플러가 박수를 보냅니다.";
                ResultAction.Text      = "[ ENTER → 처음부터 ]";
            }
            else
            {
                ResultTitle.Text       = "ORBIT ACHIEVED!";
                ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                ResultGlow.Color       = Color.FromRgb(0xFF, 0xD7, 0x00);
                ResultSub.Text = $"레벨 {_curLevel} 클리어!  {_revolutions}회 공전  발사 {_launchCount}회\n"
                               + $"다음: {LevelData.Get(_curLevel + 1).Name}";
                ResultAction.Text = "[ ENTER → 다음 레벨 ]  [ R → 재도전 ]";
            }
        }
        else
        {
            StarsText.Text = "";
            _state = GameState.Aim;
            ResultTitle.Text       = "궤도 실패";
            ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            ResultGlow.Color       = Color.FromRgb(0xE7, 0x4C, 0x3C);
            ResultSub.Text         = reason;
            ResultAction.Text      = "[ 마우스로 재조준 후 SPACE 발사 ]";
        }

        ResultOverlay.Visibility = Visibility.Visible;

        if (!success)
        {
            if (_aimLine     is not null) _aimLine.Visibility     = Visibility.Visible;
            if (_previewLine is not null) _previewLine.Visibility = Visibility.Visible;

            var t2 = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(2.5) };
            t2.Tick += (_, _) =>
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                ModeText.Text            = "[ AIM ]";
                ModeText.Foreground      = new SolidColorBrush(ColUI);
                t2.Stop();
            };
            t2.Start();
        }
    }

    // ── 업데이트 ──────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        _animTime += dt;
        if (_state == GameState.Running) SimStep(dt);
        UpdateParticles(dt);
    }

    // ── 렌더링 ────────────────────────────────────────────

    private void OnRender()
    {
        if (_state is GameState.Title or GameState.LevelSelect) return;

        if (_state == GameState.Running)
        {
            UpdateShipTransform(_probe.X, _probe.Y, _probe.Vx, _probe.Vy);

            if (_trailLine is not null)
            {
                _trailLine.Points.Clear();
                int start = Math.Max(0, _probe.Trail.Count - 300);
                for (int i = start; i < _probe.Trail.Count; i++)
                    _trailLine.Points.Add(new Point(_probe.Trail[i].X, _probe.Trail[i].Y));
            }
        }

        var tv = _planetVis.FirstOrDefault(v => v.P.IsTarget);
        if (tv != default)
        {
            double pulse = 0.6 + 0.4 * Math.Sin(_animTime * 2.0);
            tv.Glow.Opacity = pulse;
        }

        if (_state == GameState.Running) UpdateHudRun();
    }

    // ── 파티클 ────────────────────────────────────────────

    private void SpawnEndParticles(bool success)
    {
        int n = success ? 60 : 18;
        for (int i = 0; i < n; i++)
        {
            double a = _rng.NextDouble() * Math.PI * 2;
            double s = (success ? 70 : 35) + _rng.NextDouble() * 180;
            var col = success
                ? (i % 3 == 0 ? ColTarget : i % 3 == 1 ? ColStar : ColProbe)
                : Color.FromRgb(0xE7, 0x4C, 0x3C);
            var e = new Ellipse
            {
                Width  = 2 + _rng.NextDouble() * (success ? 6 : 3),
                Height = 2 + _rng.NextDouble() * (success ? 6 : 3),
                Fill   = new SolidColorBrush(col)
            };
            Canvas.SetLeft(e, _probe.X); Canvas.SetTop(e, _probe.Y);
            Canvas.SetZIndex(e, 50);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(a) * s, Math.Sin(a) * s, success ? 1.6 : 0.7));
        }
    }

    private void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (e, vx, vy, life) = _particles[i];
            double nl = life - dt;
            if (nl <= 0)
            {
                GameCanvas.Children.Remove(e);
                _particles.RemoveAt(i);
                continue;
            }
            Canvas.SetLeft(e, Canvas.GetLeft(e) + vx * dt);
            Canvas.SetTop(e,  Canvas.GetTop(e)  + (vy + 100 * dt) * dt);
            e.Opacity = Math.Max(0, nl);
            _particles[i] = (e, vx, vy + 100 * dt, nl);
        }
    }

    // ── 비주얼 클리어 ─────────────────────────────────────

    private void ClearLevel()
    {
        _planetVis.Clear();
        _stars.Clear();
        _previewLine = null;
        _trailLine   = null;
        _shipPoly    = null;
        _aimLine     = null;
        foreach (var (e, _, _, _) in _particles) GameCanvas.Children.Remove(e);
        _particles.Clear();
        GameCanvas.Children.Clear();
    }

    // ── HUD ───────────────────────────────────────────────

    private void UpdateHud()
    {
        if (!IsLoaded) return;
        LevelNumText.Text  = _curLevel.ToString();
        LevelNameText.Text = $" — {_levelDef.Name}";
        LaunchText.Text    = $"발사 {_launchCount}회";

        double vx    = (_aimX - _probeStartX) * _speedMultiplier;
        double vy    = (_aimY - _probeStartY) * _speedMultiplier;
        double spd   = Math.Sqrt(vx * vx + vy * vy);
        double vEsc  = OrbitalSim.EscapeVelocity(_probeStartX, _probeStartY, _targetPlanet);
        double angle = Math.Atan2(vy, vx) * 180 / Math.PI;

        SpeedText.Text = $"속도: {spd:0.0}  (탈출: {vEsc:0.0})";
        OrbitText.Text = $"방향: {angle:0}°   목표: {_levelDef.RequiredRevolutions}회 공전";
    }

    private void UpdateHudRun()
    {
        if (!IsLoaded) return;
        LaunchText.Text = $"발사 {_launchCount}회";
        SpeedText.Text  = $"속도: {_probe.Speed:0.0}   시간: {_simTime:0.0}초";
        OrbitText.Text  = $"공전: {_revolutions}/{_levelDef.RequiredRevolutions}  ({_totalAngle * 180 / Math.PI:0}°)";
    }

    private void UpdateHintBar()
    {
        if (!IsLoaded) return;
        HintBar.Text = $"힌트: {_levelDef.Hint}  |  SPACE: 발사  R: 재조준  ESC: 타이틀  H: 도움말";
    }

    // ── 입력 ──────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // H — 도움말 토글 (어느 상태에서도)
        if (e.Key == Key.H)
        {
            _helpVisible = !_helpVisible;
            HelpOverlay.Visibility = _helpVisible ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        // 도움말 중 ESC → 도움말만 닫기
        if (e.Key == Key.Escape && _helpVisible)
        {
            _helpVisible = false;
            HelpOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        if (_helpVisible) return;

        switch (e.Key)
        {
            // 타이틀
            case Key.Enter when _state == GameState.Title:
                ShowLevelSelect();
                break;

            // 레벨 셀렉트
            case Key.Enter when _state == GameState.LevelSelect:
                LoadLevel(_selectedLevel);
                break;

            case Key.Up when _state == GameState.LevelSelect:
                _selectedLevel = Math.Max(1, _selectedLevel - 1);
                UpdateLevelSelectUI();
                break;

            case Key.Down when _state == GameState.LevelSelect:
                _selectedLevel = Math.Min(LevelData.MaxLevel, _selectedLevel + 1);
                UpdateLevelSelectUI();
                break;

            // 게임 플레이
            case Key.Enter when _state == GameState.Clear:
                _curLevel++;
                LoadLevel(_curLevel);
                break;

            case Key.Enter when _state == GameState.GameComplete:
                ShowTitle();
                break;

            case Key.Space when _state == GameState.Aim:
                StartSimulation();
                break;

            case Key.Space when _state == GameState.Running:
                LoadLevel(_curLevel);
                break;

            case Key.R when _state == GameState.Aim:
                ResetAim();
                RefreshAimLine();
                RebuildPreview();
                UpdateShipTransform(_probeStartX, _probeStartY,
                    _levelDef.DefaultVx, _levelDef.DefaultVy);
                UpdateHud();
                break;

            case Key.R when _state == GameState.Running:
            case Key.R when _state == GameState.Clear:
                LoadLevel(_curLevel);
                break;

            case Key.Escape when _state is not GameState.Title:
                ShowTitle();
                break;
        }
    }

    // ── 타이틀 ────────────────────────────────────────────

    private void ShowTitle()
    {
        _state       = GameState.Title;
        _helpVisible = false;
        ClearLevel();
        BuildTitleStarfield();
        HudPanel.Visibility           = Visibility.Collapsed;
        HintBar.Visibility            = Visibility.Collapsed;
        ResultOverlay.Visibility      = Visibility.Collapsed;
        LevelSelectOverlay.Visibility = Visibility.Collapsed;
        HelpOverlay.Visibility        = Visibility.Collapsed;
        TitlePanel.Visibility         = Visibility.Visible;
    }

    // ── 레벨 셀렉트 ───────────────────────────────────────

    private void ShowLevelSelect()
    {
        _state         = GameState.LevelSelect;
        _selectedLevel = Math.Max(1, _curLevel);

        LevelListPanel.Children.Clear();
        _levelItems.Clear();

        for (int i = 1; i <= LevelData.MaxLevel; i++)
        {
            var def  = LevelData.Get(i);
            var rec  = _saveData.Levels.TryGetValue(i, out var r) ? r : null;
            string stars = rec?.Cleared == true
                ? "  " + SaveData.StarsString(rec.BestStars)
                : "";

            var tb = new TextBlock
            {
                Text       = $"  {i:00}.  {def.Name}{stars}",
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 14,
                Padding    = new Thickness(10, 3, 10, 3),
                Foreground = rec?.Cleared == true
                    ? new SolidColorBrush(Color.FromRgb(0xA0, 0xE8, 0xD0))
                    : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
                IsHitTestVisible = false
            };
            LevelListPanel.Children.Add(tb);
            _levelItems.Add(tb);
        }

        UpdateLevelSelectUI();
        TitlePanel.Visibility         = Visibility.Collapsed;
        LevelSelectOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateLevelSelectUI()
    {
        for (int i = 0; i < _levelItems.Count; i++)
        {
            bool sel = (i + 1) == _selectedLevel;
            var  tb  = _levelItems[i];
            var  rec = _saveData.Levels.TryGetValue(i + 1, out var r) ? r : null;

            tb.Background = sel
                ? new SolidColorBrush(Color.FromArgb(60, 94, 234, 212))
                : null;
            tb.Foreground = sel
                ? new SolidColorBrush(ColUI)
                : (rec?.Cleared == true
                    ? new SolidColorBrush(Color.FromRgb(0xA0, 0xE8, 0xD0))
                    : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)));
            tb.FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    // ── 슬라이더 이벤트 ───────────────────────────────────

    private void SpeedSlider_ValueChanged(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        _speedMultiplier = e.NewValue;
        if (SpeedSliderLabel is not null)
            SpeedSliderLabel.Text = $" ×{_speedMultiplier:0.0}";
        if (_state == GameState.Aim) RebuildPreview();
    }

    // ── 사운드 ────────────────────────────────────────────

    private static void PlayLaunch() => PlayToneAsync(520, 90, 0.30, 8.0);

    private static void PlayFail()   => PlayToneAsync(200, 180, 0.35, 3.5);

    private static void PlayClear()
    {
        Task.Run(() =>
        {
            PlayToneSync(523, 110, 0.38);
            PlayToneSync(659, 110, 0.38);
            PlayToneSync(784, 220, 0.45);
        });
    }

    private static void PlayToneAsync(int freq, int durationMs, double volume, double decay)
    {
        Task.Run(() => PlayToneCore(freq, durationMs, volume, decay));
    }

    private static void PlayToneSync(int freq, int durationMs, double volume)
    {
        PlayToneCore(freq, durationMs, volume, 5.0);
    }

    private static void PlayToneCore(int freq, int durationMs, double volume, double decay)
    {
        try
        {
            const int sampleRate = 22050;
            int samples = sampleRate * durationMs / 1000;
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);
            bw.Write("RIFF".ToCharArray()); bw.Write(36 + samples * 2);
            bw.Write("WAVE".ToCharArray()); bw.Write("fmt ".ToCharArray());
            bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(sampleRate); bw.Write(sampleRate * 2);
            bw.Write((short)2); bw.Write((short)16);
            bw.Write("data".ToCharArray()); bw.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                double t   = (double)i / sampleRate;
                double env = Math.Exp(-t * decay);
                bw.Write((short)(env * 32767 * volume * Math.Sin(2 * Math.PI * freq * t)));
            }
            ms.Position = 0;
            using var player = new System.Media.SoundPlayer(ms);
            player.PlaySync();
        }
        catch { }
    }
}
