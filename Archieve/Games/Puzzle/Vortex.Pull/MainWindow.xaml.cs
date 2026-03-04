using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VortexPull.Engine;
using VortexPull.Entities;
using VortexPull.Levels;

namespace VortexPull;

public partial class MainWindow : Window
{
    // ── 화면 상수 ──────────────────────────────────────────
    private const double ViewW = 844;
    private const double ViewH = 520;

    // ── 게임 상태 ──────────────────────────────────────────
    private enum GameState { Title, Edit, Running, Clear, GameComplete }
    private GameState _state    = GameState.Title;
    private int       _curLevel = 1;

    // ── 레벨 데이터 ────────────────────────────────────────
    private LevelDef         _levelDef = null!;
    private List<GeneratorSlot> _slots = [];
    private Ship             _ship    = new();

    // ── 렌더링 요소 ────────────────────────────────────────
    // 슬롯 비주얼: (슬롯, Ellipse 원, TextBlock 레이블)
    private readonly List<(GeneratorSlot S, Ellipse Ring, TextBlock Lbl)> _slotVisuals = [];
    // 발생기 비주얼: (슬롯, Path/Ellipse 아이콘)
    private readonly List<(GeneratorSlot S, UIElement Icon)> _genVisuals  = [];
    // 궤도 예측선
    private Polyline? _previewLine;
    // 실제 이동 궤적
    private Polyline? _trailLine;
    // 우주선 비주얼
    private Polygon?  _shipPoly;
    // 포털 비주얼
    private Ellipse?  _portalOuter, _portalInner;
    // 장애물 비주얼
    private readonly List<Ellipse> _obstacleVis = [];
    // 별빛 (정적)
    private readonly List<Ellipse> _stars = [];
    // 파티클
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)> _particles = [];

    // ── 게임루프 ───────────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly Random   _rng  = new();
    private double _animTime;

    // ── 실행 중 적분 상태 ──────────────────────────────────
    private double _simX, _simY, _simVx, _simVy;
    private List<Generator> _activeGens = [];

    // ── 색상 ───────────────────────────────────────────────
    private static readonly Color ColAttract = Color.FromRgb(0x5D, 0xAD, 0xE2); // 청색
    private static readonly Color ColRepel   = Color.FromRgb(0xE7, 0x4C, 0x3C); // 적색
    private static readonly Color ColVortex  = Color.FromRgb(0xA5, 0x69, 0xBD); // 보라
    private static readonly Color ColPortal  = Color.FromRgb(0x00, 0xFF, 0xC0); // 청록
    private static readonly Color ColShip    = Color.FromRgb(0xFF, 0xE0, 0x80); // 황금
    private static readonly Color ColTrail   = Color.FromRgb(0xFF, 0xE0, 0x80);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 레벨 로드 ─────────────────────────────────────────

    private void LoadLevel(int level)
    {
        ClearLevel();

        _levelDef  = LevelData.Get(level);
        _curLevel  = level;
        _state     = GameState.Edit;
        _animTime  = 0;

        // 슬롯 생성
        _slots.Clear();
        foreach (var sd in _levelDef.Slots)
            _slots.Add(new GeneratorSlot { X = sd.X, Y = sd.Y });

        // 우주선 초기화
        _ship.Reset(_levelDef.ShipX, _levelDef.ShipY, _levelDef.ShipVx, _levelDef.ShipVy);

        // 배경 별
        BuildStarfield();
        // 장애물
        BuildObstacles();
        // 포털
        BuildPortal();
        // 우주선
        BuildShip();
        // 슬롯 비주얼
        BuildSlotVisuals();

        // HUD
        HudPanel.Visibility      = Visibility.Visible;
        HintBar.Visibility       = Visibility.Visible;
        LegendPanel.Visibility   = Visibility.Visible;
        TitlePanel.Visibility    = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        UpdateHud();
        UpdateHintBar();
        RebuildPreview();
    }

    // ── 배경 별빛 ─────────────────────────────────────────

    private void BuildStarfield()
    {
        _stars.Clear();
        for (int i = 0; i < 120; i++)
        {
            double x = _rng.NextDouble() * ViewW;
            double y = _rng.NextDouble() * ViewH;
            double r = 0.5 + _rng.NextDouble() * 1.5;
            byte   a = (byte)(40 + _rng.Next(180));
            var star = new Ellipse
            {
                Width = r*2, Height = r*2,
                Fill  = new SolidColorBrush(Color.FromArgb(a, 0xCC, 0xDD, 0xFF)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(star, x - r);
            Canvas.SetTop(star,  y - r);
            GameCanvas.Children.Add(star);
            _stars.Add(star);
        }
    }

    // ── 장애물 ────────────────────────────────────────────

    private void BuildObstacles()
    {
        _obstacleVis.Clear();
        foreach (var obs in _levelDef.Obstacles)
        {
            var e = new Ellipse
            {
                Width  = obs.Radius * 2,
                Height = obs.Radius * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(200, 0x8B, 0x00, 0x00)),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 0xFF, 0x44, 0x44)),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(e, obs.X - obs.Radius);
            Canvas.SetTop(e,  obs.Y - obs.Radius);
            GameCanvas.Children.Add(e);
            _obstacleVis.Add(e);
        }
    }

    // ── 포털 ──────────────────────────────────────────────

    private void BuildPortal()
    {
        double r  = _levelDef.PortalRadius;
        double x  = _levelDef.PortalX;
        double y  = _levelDef.PortalY;

        _portalOuter = new Ellipse
        {
            Width = r*2 + 14, Height = r*2 + 14,
            Stroke = new SolidColorBrush(Color.FromArgb(160, 0x00, 0xFF, 0xC0)),
            StrokeThickness = 2,
            Fill  = new SolidColorBrush(Color.FromArgb(10, 0x00, 0xFF, 0xC0)),
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = ColPortal, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(_portalOuter, x - r - 7);
        Canvas.SetTop(_portalOuter,  y - r - 7);
        GameCanvas.Children.Add(_portalOuter);

        _portalInner = new Ellipse
        {
            Width = r*2, Height = r*2,
            Fill  = new SolidColorBrush(Color.FromArgb(50, 0x00, 0xFF, 0xC0)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_portalInner, x - r);
        Canvas.SetTop(_portalInner,  y - r);
        GameCanvas.Children.Add(_portalInner);

        var lbl = new TextBlock
        {
            Text = "PORTAL", FontFamily = new FontFamily("Consolas"),
            FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(160, 0x00, 0xFF, 0xC0)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(lbl, x - 18);
        Canvas.SetTop(lbl,  y + r + 4);
        GameCanvas.Children.Add(lbl);
    }

    // ── 우주선 ────────────────────────────────────────────

    private void BuildShip()
    {
        _shipPoly = new Polygon
        {
            Points = new PointCollection
            {
                new(0, -9), new(6, 6), new(0, 3), new(-6, 6)
            },
            Fill   = new SolidColorBrush(ColShip),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 0xFF, 0xFF, 0xCC)),
            StrokeThickness = 0.8,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = ColShip, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7 }
        };
        var rt = new RotateTransform(0);
        var tt = new TranslateTransform(_ship.X, _ship.Y);
        var tg = new TransformGroup();
        tg.Children.Add(rt);
        tg.Children.Add(tt);
        _shipPoly.RenderTransform = tg;
        GameCanvas.Children.Add(_shipPoly);

        // 이동 궤적
        _trailLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xE0, 0x80)),
            StrokeThickness = 1.2,
            IsHitTestVisible = false
        };
        GameCanvas.Children.Add(_trailLine);
    }

    // ── 슬롯 비주얼 ───────────────────────────────────────

    private void BuildSlotVisuals()
    {
        _slotVisuals.Clear();
        foreach (var slot in _slots)
        {
            var ring = new Ellipse
            {
                Width = 46, Height = 46,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 0x4A, 0x6A, 0x9A)),
                StrokeDashArray = new DoubleCollection([4, 3]),
                StrokeThickness = 1.5,
                Fill  = new SolidColorBrush(Color.FromArgb(12, 0x4A, 0x6A, 0x9A)),
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(ring, slot.X - 23);
            Canvas.SetTop(ring,  slot.Y - 23);
            GameCanvas.Children.Add(ring);

            var lbl = new TextBlock
            {
                Text = "SLOT", FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 0x4A, 0x6A, 0x9A)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, slot.X - 12);
            Canvas.SetTop(lbl,  slot.Y + 13);
            GameCanvas.Children.Add(lbl);

            _slotVisuals.Add((slot, ring, lbl));
        }
    }

    // ── 발생기 비주얼 업데이트 ────────────────────────────

    private void RefreshGenVisual(GeneratorSlot slot)
    {
        // 기존 비주얼 제거
        var toRemove = _genVisuals.Where(v => v.S == slot).ToList();
        foreach (var (_, icon) in toRemove)
        {
            GameCanvas.Children.Remove(icon);
        }
        _genVisuals.RemoveAll(v => v.S == slot);

        if (slot.Kind is null) return;

        var col = slot.Kind.Value switch
        {
            GeneratorKind.Attract => ColAttract,
            GeneratorKind.Repel   => ColRepel,
            GeneratorKind.Vortex  => ColVortex,
            _                     => Colors.Gray
        };

        // 발생기 원형 아이콘
        var e = new Ellipse
        {
            Width = 36, Height = 36,
            Fill = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(255, col.R, col.G, col.B)),
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = col, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(e, slot.X - 18);
        Canvas.SetTop(e,  slot.Y - 18);
        GameCanvas.Children.Add(e);
        _genVisuals.Add((slot, e));

        // 텍스트 라벨
        string txt = slot.Kind.Value switch
        {
            GeneratorKind.Attract => "A",
            GeneratorKind.Repel   => "R",
            GeneratorKind.Vortex  => "V",
            _                     => ""
        };
        var lbl = new TextBlock
        {
            Text = txt, FontFamily = new FontFamily("Consolas"),
            FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(lbl, slot.X - 6);
        Canvas.SetTop(lbl,  slot.Y - 10);
        GameCanvas.Children.Add(lbl);
        _genVisuals.Add((slot, lbl));
    }

    // ── 궤도 예측선 ───────────────────────────────────────

    private void RebuildPreview()
    {
        if (_previewLine is not null)
        {
            GameCanvas.Children.Remove(_previewLine);
            _previewLine = null;
        }

        var gens = _slots
            .Select(s => s.ToGenerator())
            .Where(g => g is not null)
            .Select(g => g!)
            .ToList();

        var pts = Simulator.PreviewOrbit(
            _ship.X, _ship.Y,
            _levelDef.ShipVx, _levelDef.ShipVy,
            gens, steps: 350, dt: 0.028);

        _previewLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(60, 0x7A, 0xC0, 0xFF)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection([4, 3]),
            IsHitTestVisible = false
        };
        foreach (var (px, py) in pts)
            _previewLine.Points.Add(new Point(px, py));

        // 미리보기는 슬롯보다 아래 레이어 (인덱스 0 근처에 삽입)
        GameCanvas.Children.Insert(Math.Max(0, _stars.Count + _obstacleVis.Count), _previewLine);
    }

    // ── 마우스 입력 ───────────────────────────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Edit) return;
        var pos = e.GetPosition(GameCanvas);

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            double dx = pos.X - slot.X, dy = pos.Y - slot.Y;
            if (Math.Sqrt(dx*dx + dy*dy) > 28) continue;

            slot.Cycle();
            RefreshGenVisual(slot);
            UpdateSlotLabel(i);
            RebuildPreview();
            return;
        }
    }

    private void UpdateSlotLabel(int idx)
    {
        if (idx >= _slotVisuals.Count) return;
        var (slot, ring, lbl) = _slotVisuals[idx];

        string txt = slot.Kind switch
        {
            null                  => "SLOT",
            GeneratorKind.Attract => "인력",
            GeneratorKind.Repel   => "척력",
            GeneratorKind.Vortex  => "소용돌이",
            _                     => ""
        };
        lbl.Text = txt;

        ring.Visibility = slot.Kind is null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 실행 ──────────────────────────────────────────────

    private void StartSimulation()
    {
        if (_state != GameState.Edit) return;

        _activeGens = _slots
            .Select(s => s.ToGenerator())
            .Where(g => g is not null)
            .Select(g => g!)
            .ToList();

        _simX  = _levelDef.ShipX;
        _simY  = _levelDef.ShipY;
        _simVx = _levelDef.ShipVx;
        _simVy = _levelDef.ShipVy;
        _ship.Reset(_simX, _simY, _simVx, _simVy);
        // 예측선 숨기기
        if (_previewLine is not null) _previewLine.Visibility = Visibility.Collapsed;

        // 궤적 초기화
        if (_trailLine is not null) _trailLine.Points.Clear();

        _state = GameState.Running;
        ModeText.Text = "[ RUN ]";
        ModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0xA0));
    }

    // ── 업데이트 ──────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        _animTime += dt;

        if (_state == GameState.Running)
            SimStep(dt);

        UpdateParticles(dt);
    }

    private void SimStep(double dt)
    {
        // 서브스텝
        const int SubSteps = 3;
        double sub = dt / SubSteps;

        for (int i = 0; i < SubSteps; i++)
        {
            (_simX, _simY, _simVx, _simVy) =
                Simulator.Step(_simX, _simY, _simVx, _simVy, _activeGens, sub);

            // 궤적 기록 (매 서브스텝은 과하니 매 dt마다)
        }

        _ship.X = _simX; _ship.Y = _simY;
        _ship.Vx = _simVx; _ship.Vy = _simVy;
        _ship.Trail.Add((_simX, _simY));

        // 화면 밖 이탈
        if (_simX < -50 || _simX > ViewW + 50 || _simY < -50 || _simY > ViewH + 50)
        {
            EndSimulation(false, "우주 밖으로 탈출!");
            return;
        }

        // 장애물 충돌
        foreach (var obs in _levelDef.Obstacles)
        {
            double dx = _simX - obs.X, dy = _simY - obs.Y;
            if (Math.Sqrt(dx*dx + dy*dy) < obs.Radius + 5)
            {
                EndSimulation(false, "장애물 충돌!");
                return;
            }
        }

        // 포털 도착
        {
            double dx = _simX - _levelDef.PortalX;
            double dy = _simY - _levelDef.PortalY;
            if (Math.Sqrt(dx*dx + dy*dy) < _levelDef.PortalRadius + 4)
            {
                EndSimulation(true, "");
                return;
            }
        }
    }

    private void EndSimulation(bool success, string failReason)
    {
        _state = success ? GameState.Clear : GameState.Edit;
        SpawnEndParticles(success);

        // 잠시 후 결과 표시
        double delay = success ? 400 : 600;
        var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(delay) };
        t.Tick += (_, _) => { ShowResult(success, failReason); t.Stop(); };
        t.Start();
    }

    private void ShowResult(bool success, string failReason)
    {
        if (success)
        {
            if (_curLevel >= LevelData.MaxLevel)
            {
                _state = GameState.GameComplete;
                ResultTitle.Text       = "MISSION COMPLETE!";
                ResultTitle.Foreground = new SolidColorBrush(ColPortal);
                ResultGlow.Color       = ColPortal;
                ResultSub.Text         = "모든 궤도를 완성했습니다!";
                ResultAction.Text      = "[ ENTER → 처음부터 ]";
            }
            else
            {
                ResultTitle.Text       = "PORTAL REACHED!";
                ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                ResultGlow.Color       = Color.FromRgb(0xFF, 0xD7, 0x00);
                ResultSub.Text         = $"레벨 {_curLevel} 클리어!\n다음: {LevelData.Get(_curLevel + 1).Name}";
                ResultAction.Text      = "[ ENTER → 다음 레벨 ]  [ R → 재도전 ]";
            }
        }
        else
        {
            ResultTitle.Text       = "궤도 이탈!";
            ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            ResultGlow.Color       = Color.FromRgb(0xE7, 0x4C, 0x3C);
            ResultSub.Text         = failReason;
            ResultAction.Text      = "[ R → 리셋 ]  [ 발생기를 다시 배치하세요 ]";
        }

        ResultOverlay.Visibility = Visibility.Visible;

        if (!success)
        {
            var t2 = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(2.5) };
            t2.Tick += (_, _) =>
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                ModeText.Text            = "[ EDIT ]";
                ModeText.Foreground      = new SolidColorBrush(Color.FromRgb(0x7A, 0xC0, 0xFF));
                if (_previewLine is not null) _previewLine.Visibility = Visibility.Visible;
                t2.Stop();
            };
            t2.Start();
        }
    }

    // ── 렌더링 ────────────────────────────────────────────

    private void OnRender()
    {
        if (_state == GameState.Title) return;

        // 우주선 위치 갱신
        if (_shipPoly is not null)
        {
            var tg = (TransformGroup)_shipPoly.RenderTransform;
            var rt = (RotateTransform)tg.Children[0];
            var tt = (TranslateTransform)tg.Children[1];

            double angle = Math.Atan2(_ship.Vy, _ship.Vx) * 180 / Math.PI + 90;
            rt.Angle = angle;
            tt.X = _ship.X;
            tt.Y = _ship.Y;
        }

        // 이동 궤적 갱신
        if (_trailLine is not null && _state == GameState.Running)
        {
            _trailLine.Points.Clear();
            int skip = Math.Max(1, _ship.Trail.Count / 200);
            for (int i = 0; i < _ship.Trail.Count; i += skip)
                _trailLine.Points.Add(new Point(_ship.Trail[i].X, _ship.Trail[i].Y));
        }

        // 포털 점멸
        if (_portalOuter is not null)
        {
            double pulse = 0.6 + 0.4 * Math.Sin(_animTime * 2.5);
            _portalOuter.Opacity = pulse;
        }

        // 슬롯 링 점멸 (편집 모드)
        if (_state == GameState.Edit)
        {
            double pulse = 0.4 + 0.6 * Math.Abs(Math.Sin(_animTime * 1.5));
            foreach (var (_, ring, _) in _slotVisuals)
                if (ring.Visibility == Visibility.Visible)
                    ring.Opacity = pulse;
        }
    }

    // ── 파티클 ────────────────────────────────────────────

    private void SpawnEndParticles(bool success)
    {
        int n = success ? 60 : 20;
        for (int i = 0; i < n; i++)
        {
            double a = _rng.NextDouble() * Math.PI * 2;
            double s = (success ? 80 : 40) + _rng.NextDouble() * 160;
            var col = success
                ? (i % 2 == 0 ? ColPortal : ColShip)
                : Color.FromRgb(0xE7, 0x4C, 0x3C);
            var e = new Ellipse
            {
                Width  = 2 + _rng.NextDouble() * (success ? 6 : 4),
                Height = 2 + _rng.NextDouble() * (success ? 6 : 4),
                Fill   = new SolidColorBrush(col)
            };
            Canvas.SetLeft(e, _ship.X);
            Canvas.SetTop(e,  _ship.Y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(a)*s, Math.Sin(a)*s, success ? 1.5 : 0.8));
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
            Canvas.SetTop(e,  Canvas.GetTop(e)  + (vy + 120 * dt) * dt);
            e.Opacity = Math.Max(0, nl);
            _particles[i] = (e, vx, vy + 120 * dt, nl);
        }
    }

    // ── 비주얼 클리어 ─────────────────────────────────────

    private void ClearLevel()
    {
        _slotVisuals.Clear();
        _genVisuals.Clear();
        _obstacleVis.Clear();
        _stars.Clear();
        _previewLine  = null;
        _trailLine    = null;
        _shipPoly     = null;
        _portalOuter  = null;
        _portalInner  = null;
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
        HintBarTop.Text    = $"슬롯: {_slots.Count}";
    }

    private void UpdateHintBar()
    {
        if (!IsLoaded) return;
        HintBar.Text = $"힌트: {_levelDef.Hint}  |  SPACE: 실행  R: 리셋  ESC: 타이틀";
    }

    // ── 입력 ──────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _state == GameState.Title:
                _curLevel = 1;
                LoadLevel(_curLevel);
                break;

            case Key.Enter when _state == GameState.Clear:
                _curLevel++;
                LoadLevel(_curLevel);
                break;

            case Key.Enter when _state == GameState.GameComplete:
                ShowTitle();
                break;

            case Key.Space when _state == GameState.Edit:
                StartSimulation();
                break;

            case Key.Space when _state == GameState.Running:
                LoadLevel(_curLevel);
                break;

            case Key.R when _state is GameState.Edit or GameState.Running:
                LoadLevel(_curLevel);
                break;

            case Key.Escape when _state is not GameState.Title:
                ShowTitle();
                break;
        }
    }

    private void ShowTitle()
    {
        _state = GameState.Title;
        ClearLevel();
        HudPanel.Visibility      = Visibility.Collapsed;
        HintBar.Visibility       = Visibility.Collapsed;
        LegendPanel.Visibility   = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility    = Visibility.Visible;
    }
}
