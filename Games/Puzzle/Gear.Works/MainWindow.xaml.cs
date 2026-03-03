using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using GearWorks.Engine;
using GearWorks.Entities;
using GearWorks.Levels;

namespace GearWorks;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 화면 상수 ──────────────────────────────────────────
    private const double ViewW = 844;
    private const double ViewH = 520;

    // ── 게임 상태 ──────────────────────────────────────────
    private enum GameState { Title, Edit, Running, Clear, GameComplete }
    private GameState _state    = GameState.Title;
    private int       _curLevel = 1;
    private bool      _runResult;  // 실행 결과 (성공 여부)

    // ── 레벨 데이터 ────────────────────────────────────────
    private LevelDef    _levelDef = null!;
    private List<Gear>  _fixedGears = [];  // Motor / Fixed / Output
    private List<GearSlot> _slots   = [];
    private Gear        _outputGear = null!;

    // ── 렌더링 — 기어 Path 목록 ───────────────────────────
    // 각 기어는 Path 1개 (원점 기준 기어 이빨 Geometry + TransformGroup)
    private readonly List<(Gear G, System.Windows.Shapes.Path P, RotateTransform Rot)> _gearVisuals = [];
    // 슬롯 비주얼 (Ellipse 점선 + 레이블)
    private readonly List<(GearSlot Slot, Ellipse SlotRing, TextBlock SizeLabel)>       _slotVisuals = [];
    // 연결 선 (기어 간 접선 시각화)
    private readonly List<Line> _meshLines = [];
    // 파티클
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)> _particles = [];

    // ── 타이머 ─────────────────────────────────────────────
    private double _animTime;

    // ── 게임루프 ───────────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly Random   _rng  = new();

    // ── 색상 ───────────────────────────────────────────────
    private static readonly Color ColMotor  = Color.FromRgb(0xD4, 0xA0, 0x30);  // 골드
    private static readonly Color ColFixed  = Color.FromRgb(0x5A, 0x6A, 0x8A);  // 블루그레이
    private static readonly Color ColSlot   = Color.FromRgb(0x4A, 0x9A, 0x7A);  // 초록
    private static readonly Color ColOutput = Color.FromRgb(0x9A, 0x4A, 0xCA);  // 보라
    private static readonly Color ColSlotEmpty = Color.FromArgb(60, 0x4A, 0x9A, 0x7A);
    private static readonly Color ColMesh   = Color.FromArgb(80, 0xFF, 0xD7, 0x00);
    private static readonly Color ColBg     = Color.FromRgb(0x1A, 0x1A, 0x2E);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyDarkTitleBar();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource src)
        { int v = 1; DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int)); }
    }

    // ── 레벨 로드 ─────────────────────────────────────────

    private void LoadLevel(int level)
    {
        ClearLevelVisuals();

        _levelDef  = LevelData.Get(level);
        _curLevel  = level;
        _state     = GameState.Edit;
        _animTime  = 0;
        _runResult = false;

        // 고정 기어 생성
        _fixedGears.Clear();
        foreach (var fd in _levelDef.FixedGears)
        {
            var g = new Gear
            {
                X = fd.X, Y = fd.Y, Radius = fd.Radius, Role = fd.Role,
                AngularVelocity = fd.Role == GearRole.Motor
                    ? -fd.MotorRpm * Math.PI * 2 / 60.0  // CCW (음수)
                    : 0
            };
            _fixedGears.Add(g);
        }
        _outputGear = _fixedGears.First(g => g.Role == GearRole.Output);

        // 슬롯 생성
        _slots.Clear();
        foreach (var sd in _levelDef.Slots)
            _slots.Add(new GearSlot { X = sd.X, Y = sd.Y });

        // 배경 격자
        BuildBackground();
        // 슬롯 비주얼
        BuildSlotVisuals();
        // 고정 기어 비주얼
        foreach (var g in _fixedGears)
            AddGearVisual(g);

        // HUD
        HudPanel.Visibility      = Visibility.Visible;
        HintBar.Visibility       = Visibility.Visible;
        TitlePanel.Visibility    = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        UpdateHud();
        UpdateHintBar();
    }

    // ── 배경 격자 ─────────────────────────────────────────

    private void BuildBackground()
    {
        for (int x = 0; x < ViewW; x += 44)
        {
            var l = new Line { X1=x, Y1=0, X2=x, Y2=ViewH,
                Stroke = new SolidColorBrush(Color.FromArgb(12, 0x55,0x55,0x88)), StrokeThickness=0.5 };
            GameCanvas.Children.Add(l);
        }
        for (int y = 0; y < ViewH; y += 44)
        {
            var l = new Line { X1=0, Y1=y, X2=ViewW, Y2=y,
                Stroke = new SolidColorBrush(Color.FromArgb(12, 0x55,0x55,0x88)), StrokeThickness=0.5 };
            GameCanvas.Children.Add(l);
        }
    }

    // ── 슬롯 비주얼 ───────────────────────────────────────

    private void BuildSlotVisuals()
    {
        _slotVisuals.Clear();
        foreach (var slot in _slots)
        {
            double maxR = GearSlot.RadiusOf(GearSize.Large);
            var ring = new Ellipse
            {
                Width = maxR * 2, Height = maxR * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 0x4A, 0x9A, 0x7A)),
                StrokeDashArray = new DoubleCollection([4, 3]),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(15, 0x4A, 0x9A, 0x7A)),
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(ring, slot.X - maxR);
            Canvas.SetTop(ring,  slot.Y - maxR);
            GameCanvas.Children.Add(ring);

            var lbl = new TextBlock
            {
                Text = "클릭", FontFamily = new FontFamily("Consolas"),
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(120, 0x4A, 0x9A, 0x7A)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, slot.X - 12);
            Canvas.SetTop(lbl,  slot.Y + 6);
            GameCanvas.Children.Add(lbl);

            _slotVisuals.Add((slot, ring, lbl));
        }
    }

    // ── 기어 이빨 Geometry 생성 (원점 기준) ─────────────────

    private static Geometry BuildGearGeometry(double radius)
    {
        int    n        = Math.Max(8, (int)(radius * 0.55));
        double da       = Math.PI * 2 / n;
        double halfTooth = da * 0.22;
        double halfValley = da * 0.28;
        double rInner   = radius * 0.80;
        double rOuter   = radius;
        double rHub     = radius * 0.20;   // 중앙 허브

        var sg = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = sg.Open())
        {
            // 기어 외곽 (이빨 포함)
            bool first = true;
            for (int i = 0; i < n; i++)
            {
                double c = da * i;
                var p0 = P(rInner, c - halfValley - halfTooth);
                var p1 = P(rOuter, c - halfTooth);
                var p2 = P(rOuter, c + halfTooth);
                var p3 = P(rInner, c + halfTooth + halfValley);

                if (first) { ctx.BeginFigure(p0, true, true); first = false; }
                else         ctx.LineTo(p0, true, false);

                ctx.LineTo(p1, true, false);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
            }

            // 중앙 구멍 (EvenOdd로 뚫림)
            ctx.BeginFigure(new Point(rHub, 0), true, true);
            ctx.ArcTo(new Point(-rHub,  0), new Size(rHub, rHub), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.ArcTo(new Point( rHub,  0), new Size(rHub, rHub), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        sg.Freeze();
        return sg;
    }

    private static Point P(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));

    // ── 기어 비주얼 추가 ──────────────────────────────────

    private void AddGearVisual(Gear g)
    {
        var color = g.Role switch
        {
            GearRole.Motor  => ColMotor,
            GearRole.Output => ColOutput,
            GearRole.Slot   => ColSlot,
            _               => ColFixed
        };

        var path = new System.Windows.Shapes.Path
        {
            Data   = BuildGearGeometry(g.Radius),
            Fill   = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            StrokeThickness  = 0.5,
            IsHitTestVisible = false
        };

        var rot   = new RotateTransform(g.Angle * 180 / Math.PI);
        var trans = new TranslateTransform(g.X, g.Y);
        var group = new TransformGroup();
        group.Children.Add(rot);
        group.Children.Add(trans);
        path.RenderTransform = group;

        GameCanvas.Children.Add(path);
        _gearVisuals.Add((g, path, rot));

        // 역할 레이블 (Motor / Output)
        if (g.Role is GearRole.Motor or GearRole.Output)
        {
            var lbl = new TextBlock
            {
                Text = g.Role == GearRole.Motor ? "MOTOR" : "OUTPUT",
                FontFamily = new FontFamily("Consolas"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 0xEE, 0xCC, 0x88)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, g.X - 18);
            Canvas.SetTop(lbl,  g.Y - g.Radius - 16);
            GameCanvas.Children.Add(lbl);
        }
    }

    // ── 슬롯 클릭 처리 ────────────────────────────────────

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Edit) return;
        var pos = e.GetPosition(GameCanvas);

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            double dx = pos.X - slot.X, dy = pos.Y - slot.Y;
            double dist = Math.Sqrt(dx*dx + dy*dy);
            if (dist > GearSlot.RadiusOf(GearSize.Large) + 12) continue;

            // 기존 배치 기어 비주얼 제거
            RemoveSlotGearVisual(slot);

            // 크기 순환
            slot.Cycle();

            // 새 기어 비주얼 추가
            if (slot.Placed is not null)
            {
                AddGearVisual(slot.Placed);
                SpawnPlaceParticles(slot.X, slot.Y);
            }

            // 슬롯 링 크기 업데이트 (선택된 크기로)
            UpdateSlotRing(i);
            UpdateHud();
            return;
        }
    }

    private void RemoveSlotGearVisual(GearSlot slot)
    {
        var toRemove = _gearVisuals.Where(v => v.G == slot.Placed).ToList();
        foreach (var (_, p, _) in toRemove)
        {
            GameCanvas.Children.Remove(p);
            _gearVisuals.Remove((toRemove[0].G, p, toRemove[0].Rot));
        }
        // 더 안전한 제거
        for (int i = _gearVisuals.Count - 1; i >= 0; i--)
            if (_gearVisuals[i].G == slot.Placed)
            {
                GameCanvas.Children.Remove(_gearVisuals[i].P);
                _gearVisuals.RemoveAt(i);
            }
    }

    private void UpdateSlotRing(int slotIdx)
    {
        if (slotIdx >= _slotVisuals.Count) return;
        var (slot, ring, lbl) = _slotVisuals[slotIdx];

        double r = slot.Size == GearSize.None
            ? GearSlot.RadiusOf(GearSize.Large)
            : GearSlot.RadiusOf(slot.Size);

        string sizeText = slot.Size switch
        {
            GearSize.None   => "클릭",
            GearSize.Small  => "소(24)",
            GearSize.Medium => "중(36)",
            GearSize.Large  => "대(52)",
            _               => ""
        };

        lbl.Text = sizeText;
        Canvas.SetLeft(lbl, slot.X - 16);

        if (slot.Size != GearSize.None)
        {
            ring.Visibility = Visibility.Collapsed;
        }
        else
        {
            ring.Visibility = Visibility.Visible;
        }
    }

    // ── 실행 / 리셋 ───────────────────────────────────────

    private void RunSimulation()
    {
        if (_state != GameState.Edit) return;

        // 전체 기어 목록 구성 (고정 + 배치된 슬롯)
        var allGears = new List<Gear>(_fixedGears);
        foreach (var slot in _slots)
            if (slot.Placed is not null)
                allGears.Add(slot.Placed);

        // BFS 풀기
        GearSolver.Solve(allGears);

        // 판정
        bool success = GearSolver.CheckClear(_outputGear, _levelDef.TargetSign);
        _runResult = success;

        // 맞물림 선 그리기
        DrawMeshLines(allGears);

        _state = GameState.Running;
        ModeText.Text       = "[ RUN ]";
        ModeText.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x00, 0xD2, 0xA0)
            : Color.FromRgb(0xE7, 0x4C, 0x3C));

        // 결과 오버레이 (0.5초 후 표시)
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => { ShowResult(success); timer.Stop(); };
        timer.Start();

        // 연결 안된 기어 강조
        HighlightUnconnected(allGears);
    }

    private void DrawMeshLines(List<Gear> gears)
    {
        foreach (var l in _meshLines) GameCanvas.Children.Remove(l);
        _meshLines.Clear();

        for (int i = 0; i < gears.Count; i++)
        for (int j = i + 1; j < gears.Count; j++)
        {
            if (!GearSolver.CanMesh(gears[i], gears[j])) continue;
            var line = new Line
            {
                X1 = gears[i].X, Y1 = gears[i].Y,
                X2 = gears[j].X, Y2 = gears[j].Y,
                Stroke = new SolidColorBrush(ColMesh),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection([3, 3]),
                IsHitTestVisible = false
            };
            GameCanvas.Children.Add(line);
            _meshLines.Add(line);
        }
    }

    private void HighlightUnconnected(List<Gear> gears)
    {
        foreach (var (g, p, _) in _gearVisuals)
        {
            if (!gears.Contains(g)) continue;
            if (!g.IsConnected && g.Role != GearRole.Motor)
            {
                p.Stroke          = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                p.StrokeThickness = 2;
            }
        }
    }

    private void ShowResult(bool success)
    {
        if (success)
        {
            if (_curLevel >= LevelData.MaxLevel)
            {
                _state = GameState.GameComplete;
                ResultTitle.Text       = "GEAR COMPLETE!";
                ResultTitle.Foreground = new SolidColorBrush(ColMotor);
                ResultSub.Text         = "모든 기계를 완성했습니다!";
                ResultAction.Text      = "[ ENTER → 처음부터 ]";
            }
            else
            {
                _state = GameState.Clear;
                ResultTitle.Text       = "GEAR COMPLETE!";
                ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                ResultSub.Text         = $"레벨 {_curLevel} 클리어!\n다음: {LevelData.Get(_curLevel + 1).Name}";
                ResultAction.Text      = "[ ENTER → 다음 레벨 ]  [ R → 재도전 ]";
            }
            SpawnSuccessParticles();
        }
        else
        {
            _state = GameState.Edit;  // 편집으로 복귀
            ResultTitle.Text       = "연결 실패!";
            ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

            string dir = _levelDef.TargetSign switch { +1 => "CW", -1 => "CCW", _ => "회전" };
            string outDir = _outputGear.IsSolved
                ? $"출력: {_outputGear.DirectionLabel} (목표: {dir})"
                : "출력 기어에 도달하지 못했습니다";
            ResultSub.Text    = outDir;
            ResultAction.Text = "[ R → 리셋 ]  [ 기어를 다시 배치하세요 ]";
        }

        ResultOverlay.Visibility = Visibility.Visible;
        HintBar.Visibility       = Visibility.Collapsed;

        if (!success)
        {
            var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) =>
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                HintBar.Visibility       = Visibility.Visible;
                ModeText.Text            = "[ EDIT ]";
                ModeText.Foreground      = new SolidColorBrush(Color.FromRgb(0xB8, 0xA0, 0x60));
                foreach (var l in _meshLines) GameCanvas.Children.Remove(l);
                _meshLines.Clear();
                // 연결 강조 제거
                foreach (var (_, p, _) in _gearVisuals)
                {
                    p.Stroke = null;
                    p.StrokeThickness = 0.5;
                }
                t.Stop();
            };
            t.Start();
        }
    }

    // ── 업데이트 / 렌더링 ─────────────────────────────────

    private void OnUpdate(double dt)
    {
        _animTime += dt;

        if (_state is GameState.Running or GameState.Clear or GameState.GameComplete)
            AdvanceGearAngles(dt);

        UpdateParticles(dt);
    }

    private void AdvanceGearAngles(double dt)
    {
        foreach (var (g, _, _) in _gearVisuals)
            if (g.IsSolved || g.Role == GearRole.Motor)
                g.Angle += g.AngularVelocity * dt;
    }

    private void OnRender()
    {
        if (_state == GameState.Title) return;

        // 기어 회전 반영
        foreach (var (g, _, rot) in _gearVisuals)
            rot.Angle = g.Angle * (180.0 / Math.PI);

        // 출력 기어 RPM 표시 (실행 중)
        if (_state is GameState.Running or GameState.Clear or GameState.GameComplete)
        {
            string dir = _outputGear.DirectionLabel;
            TargetText.Text = $"Output: {dir} {_outputGear.Rpm:0} RPM";
        }

        // 슬롯 링 점멸 (편집 모드)
        if (_state == GameState.Edit)
        {
            double pulse = 0.5 + 0.5 * Math.Abs(Math.Sin(_animTime * 1.2));
            foreach (var (_, ring, _) in _slotVisuals)
                if (ring.Visibility == Visibility.Visible)
                    ring.Opacity = pulse;
        }
    }

    // ── 파티클 ────────────────────────────────────────────

    private void SpawnPlaceParticles(double x, double y)
    {
        for (int i = 0; i < 10; i++)
        {
            double a = _rng.NextDouble() * Math.PI * 2;
            double s = 40 + _rng.NextDouble() * 80;
            var e = new Ellipse { Width=3, Height=3, Fill=new SolidColorBrush(ColSlot) };
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(a)*s, Math.Sin(a)*s, 0.5));
        }
    }

    private void SpawnSuccessParticles()
    {
        for (int i = 0; i < 50; i++)
        {
            double a = _rng.NextDouble() * Math.PI * 2;
            double s = 80 + _rng.NextDouble() * 220;
            var col = i % 3 == 0 ? ColMotor : i % 3 == 1 ? ColSlot : ColOutput;
            var e = new Ellipse
            {
                Width  = 3 + _rng.NextDouble() * 5,
                Height = 3 + _rng.NextDouble() * 5,
                Fill   = new SolidColorBrush(col)
            };
            double x = ViewW * 0.5, y = ViewH * 0.45;
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(a)*s, Math.Sin(a)*s, 1.5));
        }
    }

    private void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (e, vx, vy, life) = _particles[i];
            double nl = life - dt;
            if (nl <= 0) { GameCanvas.Children.Remove(e); _particles.RemoveAt(i); continue; }
            Canvas.SetLeft(e, Canvas.GetLeft(e) + vx * dt);
            Canvas.SetTop(e,  Canvas.GetTop(e)  + (vy + 250 * dt) * dt);
            e.Opacity = Math.Max(0, nl);
            _particles[i] = (e, vx, vy + 250 * dt, nl);
        }
    }

    // ── 비주얼 클리어 ─────────────────────────────────────

    private void ClearLevelVisuals()
    {
        _gearVisuals.Clear();
        _slotVisuals.Clear();
        foreach (var l in _meshLines) GameCanvas.Children.Remove(l);
        _meshLines.Clear();
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

        string dir = _levelDef.TargetSign switch { +1 => "CW", -1 => "CCW", _ => "회전" };
        TargetText.Text = $"목표: {dir}";

        int placed = _slots.Count(s => s.Placed is not null);
        string state = _state == GameState.Edit ? "[ EDIT ]" : "[ RUN ]";
        ModeText.Text = state;
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
                _state = GameState.Title;
                ResultOverlay.Visibility = Visibility.Collapsed;
                HudPanel.Visibility      = Visibility.Collapsed;
                HintBar.Visibility       = Visibility.Collapsed;
                TitlePanel.Visibility    = Visibility.Visible;
                break;

            case Key.Space when _state == GameState.Edit:
                RunSimulation();
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
        ClearLevelVisuals();
        HudPanel.Visibility      = Visibility.Collapsed;
        HintBar.Visibility       = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility    = Visibility.Visible;
    }
}
