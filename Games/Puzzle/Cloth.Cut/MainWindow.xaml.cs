using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ClothCut.Engine;
using ClothCut.Entities;
using ClothCut.Levels;

namespace ClothCut;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 화면 상수 ──────────────────────────────────────────
    private const double ViewW   = 844;
    private const double ViewH   = 520;
    private const double ScaleH  = 50;   // 저울 높이
    private const double ScaleY  = 490;  // 저울 바닥 Y
    private const double PinR    = 6;    // 핀 반지름

    // ── 게임 상태 ──────────────────────────────────────────
    private enum GameState { Title, Settling, Cutting, Judging, Clear, GameComplete }
    private GameState _state    = GameState.Title;
    private int       _curLevel = 1;

    // ── 레벨 데이터 ────────────────────────────────────────
    private LevelDef  _levelDef = null!;
    private ClothMesh _mesh     = null!;
    private double    _settleTimer;
    private const double SettleTime = 1.0;  // 초기 안정화 시간

    // ── 절단 경로 ──────────────────────────────────────────
    private bool                         _isCutting;
    private readonly List<(double X, double Y)> _cutPath = [];
    private Polyline?                    _cutLine;

    // ── 렌더링 요소 ────────────────────────────────────────
    private Polygon[]   _cellPolygons = [];   // 천 면 Polygon
    private Ellipse[]   _pinEllipses  = [];   // 핀 시각화
    private UIElement[] _scaleVisuals = [];   // 저울 UI
    private TextBlock[] _scaleLabels  = [];   // 저울 비율 텍스트
    private Path?       _cutMarkPath;          // 절단 흔적

    // ── 파티클 ────────────────────────────────────────────
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)> _particles = [];
    private readonly Random _rng = new();

    // ── 색상 ──────────────────────────────────────────────
    private static readonly Color ColCloth    = Color.FromRgb(0xD4, 0xA5, 0x74);
    private static readonly Color ColElastic  = Color.FromRgb(0x6A, 0xAD, 0xFF);
    private static readonly Color ColCanvas   = Color.FromRgb(0xC8, 0xB8, 0x9A);
    private static readonly Color ColScaleOk  = Color.FromRgb(0x00, 0xD2, 0xA0);
    private static readonly Color ColScaleFail= Color.FromRgb(0xE7, 0x4C, 0x3C);
    private static readonly Color ColPin      = Color.FromRgb(0xFF, 0xD7, 0x00);
    private static readonly Color ColCutLine  = Color.FromRgb(0xFF, 0x44, 0x44);

    // ── 게임 루프 ──────────────────────────────────────────
    private readonly GameLoop _loop = new();
    private double _animTime;

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

        _levelDef    = LevelData.Get(level);
        _curLevel    = level;
        _state       = GameState.Settling;
        _settleTimer = SettleTime;
        _animTime    = 0;
        _cutPath.Clear();
        _isCutting   = false;

        // 저울 초기화
        foreach (var sc in _levelDef.Scales) sc.Reset();

        // 천 생성
        _mesh = ClothMesh.Create(
            _levelDef.Cols, _levelDef.Rows, _levelDef.Spacing,
            _levelDef.StartX, _levelDef.StartY,
            _levelDef.PinnedCols, _levelDef.Stiffness);

        // 비주얼 구성
        BuildBackground();
        BuildScaleVisuals();
        BuildClothVisuals();
        BuildCutLine();

        // HUD
        HudPanel.Visibility       = Visibility.Visible;
        HintBar.Visibility        = Visibility.Visible;
        TitlePanel.Visibility     = Visibility.Collapsed;
        ResultOverlay.Visibility  = Visibility.Collapsed;
        SettlingText.Visibility   = Visibility.Visible;

        UpdateHud();
        UpdateHintBar();
    }

    // ── 비주얼 구성 ───────────────────────────────────────

    private void BuildBackground()
    {
        // 격자 배경 (미세 점선)
        for (int x = 0; x < ViewW; x += 50)
        {
            var l = new Line { X1=x, Y1=0, X2=x, Y2=ScaleY-ScaleH-10,
                Stroke = new SolidColorBrush(Color.FromArgb(14,0x55,0x55,0x99)), StrokeThickness=0.5 };
            GameCanvas.Children.Add(l);
        }
        for (int y = 0; y < ScaleY - ScaleH - 10; y += 50)
        {
            var l = new Line { X1=0, Y1=y, X2=ViewW, Y2=y,
                Stroke = new SolidColorBrush(Color.FromArgb(14,0x55,0x55,0x99)), StrokeThickness=0.5 };
            GameCanvas.Children.Add(l);
        }
    }

    private void BuildScaleVisuals()
    {
        var scales = _levelDef.Scales;
        _scaleVisuals = new UIElement[scales.Length];
        _scaleLabels  = new TextBlock[scales.Length];

        for (int i = 0; i < scales.Length; i++)
        {
            var sc = scales[i];

            // 저울 컨테이너 (테두리)
            var border = new Rectangle
            {
                Width           = sc.Width,
                Height          = ScaleH,
                Fill            = new SolidColorBrush(Color.FromArgb(40, 0x44, 0x44, 0x88)),
                Stroke          = new SolidColorBrush(Color.FromArgb(120, 0x66, 0x66, 0xAA)),
                StrokeThickness = 1.5,
                RadiusX = 4, RadiusY = 4
            };
            Canvas.SetLeft(border, sc.CenterX - sc.Width * 0.5);
            Canvas.SetTop(border,  ScaleY - ScaleH);
            GameCanvas.Children.Add(border);
            _scaleVisuals[i] = border;

            // 저울 레이블
            var lblName = new TextBlock
            {
                Text       = sc.Label,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 0xAA, 0xAA, 0xCC))
            };
            Canvas.SetLeft(lblName, sc.CenterX - 20);
            Canvas.SetTop(lblName,  ScaleY - ScaleH + 6);
            GameCanvas.Children.Add(lblName);

            // 목표 범위 표시
            var lblRange = new TextBlock
            {
                Text       = $"{sc.MinRatio*100:0}~{sc.MaxRatio*100:0}%",
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 0x88, 0x88, 0xAA))
            };
            Canvas.SetLeft(lblRange, sc.CenterX - 22);
            Canvas.SetTop(lblRange,  ScaleY - ScaleH + 20);
            GameCanvas.Children.Add(lblRange);

            // 결과 비율 텍스트
            var lblResult = new TextBlock
            {
                Text       = "",
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(0, 0xFF, 0xFF, 0xFF))
            };
            Canvas.SetLeft(lblResult, sc.CenterX - 24);
            Canvas.SetTop(lblResult,  ScaleY - ScaleH + 36);
            GameCanvas.Children.Add(lblResult);
            _scaleLabels[i] = lblResult;
        }
    }

    private void BuildClothVisuals()
    {
        int cols = _levelDef.Cols, rows = _levelDef.Rows;
        int cellCount = (rows - 1) * (cols - 1);

        // 재질에 따른 색상
        var clothColor = _levelDef.MaterialName switch
        {
            "탄성 직물" => ColElastic,
            "캔버스"    => ColCanvas,
            _           => ColCloth
        };

        _cellPolygons = new Polygon[cellCount];
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            int idx  = r * (cols - 1) + c;
            var poly = new Polygon
            {
                Fill   = new SolidColorBrush(Color.FromArgb(220, clothColor.R, clothColor.G, clothColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(60, 0x00, 0x00, 0x00)),
                StrokeThickness = 0.3,
                Points = [new(0,0), new(0,0), new(0,0), new(0,0)]
            };
            GameCanvas.Children.Add(poly);
            _cellPolygons[idx] = poly;
        }

        // 핀 비주얼
        _pinEllipses = new Ellipse[_levelDef.PinnedCols.Length];
        for (int i = 0; i < _levelDef.PinnedCols.Length; i++)
        {
            int pc   = _levelDef.PinnedCols[i];
            var node = _mesh.NodeAt(0, pc);
            var pin  = new Ellipse
            {
                Width  = PinR * 2, Height = PinR * 2,
                Fill   = new SolidColorBrush(ColPin),
                Stroke = new SolidColorBrush(Color.FromRgb(0xAA, 0x88, 0x00)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(pin, node.X - PinR);
            Canvas.SetTop(pin,  node.Y - PinR);
            GameCanvas.Children.Add(pin);
            _pinEllipses[i] = pin;
        }
    }

    private void BuildCutLine()
    {
        _cutLine = new Polyline
        {
            Stroke          = new SolidColorBrush(ColCutLine),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([5, 3]),
            IsHitTestVisible = false
        };
        GameCanvas.Children.Add(_cutLine);

        _cutMarkPath = new Path
        {
            Stroke          = new SolidColorBrush(Color.FromArgb(120, 0xFF, 0x44, 0x44)),
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        GameCanvas.Children.Add(_cutMarkPath);
    }

    // ── 업데이트 ──────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        _animTime += dt;

        if (_state == GameState.Settling)
        {
            ClothSim.Step(_mesh, dt);
            _settleTimer -= dt;
            if (_settleTimer <= 0)
            {
                _state = GameState.Cutting;
                SettlingText.Visibility = Visibility.Collapsed;
                UpdateHud();
                UpdateHintBar();
            }
        }
        else if (_state == GameState.Cutting)
        {
            ClothSim.Step(_mesh, dt);
        }
        else if (_state == GameState.Judging)
        {
            ClothSim.Step(_mesh, dt);
        }

        UpdateParticles(dt);
    }

    // ── 렌더링 ────────────────────────────────────────────

    private void OnRender()
    {
        if (_state is GameState.Title or GameState.Clear or GameState.GameComplete) return;

        RenderCloth();
        UpdatePins();
    }

    private void RenderCloth()
    {
        int cols = _levelDef.Cols, rows = _levelDef.Rows;

        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            int idx  = r * (cols - 1) + c;
            var poly = _cellPolygons[idx];

            bool broken = _mesh.IsCellBroken(r, c);
            if (broken)
            {
                poly.Visibility = Visibility.Collapsed;
                continue;
            }

            poly.Visibility = Visibility.Visible;

            int i0 = _mesh.NodeIndex(r,     c);
            int i1 = _mesh.NodeIndex(r,     c + 1);
            int i2 = _mesh.NodeIndex(r + 1, c + 1);
            int i3 = _mesh.NodeIndex(r + 1, c);

            var nodes = _mesh.Nodes;
            poly.Points[0] = new Point(nodes[i0].X, nodes[i0].Y);
            poly.Points[1] = new Point(nodes[i1].X, nodes[i1].Y);
            poly.Points[2] = new Point(nodes[i2].X, nodes[i2].Y);
            poly.Points[3] = new Point(nodes[i3].X, nodes[i3].Y);
        }
    }

    private void UpdatePins()
    {
        for (int i = 0; i < _levelDef.PinnedCols.Length && i < _pinEllipses.Length; i++)
        {
            int pc   = _levelDef.PinnedCols[i];
            var node = _mesh.NodeAt(0, pc);
            Canvas.SetLeft(_pinEllipses[i], node.X - PinR);
            Canvas.SetTop(_pinEllipses[i],  node.Y - PinR);
        }
    }

    // ── 마우스 절단 ───────────────────────────────────────

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Cutting) return;
        var pos = e.GetPosition(GameCanvas);
        _isCutting = true;
        _cutPath.Clear();
        _cutPath.Add((pos.X, pos.Y));
        if (_cutLine is not null)
        {
            _cutLine.Points.Clear();
            _cutLine.Points.Add(pos);
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCutting || _state != GameState.Cutting) return;
        var pos = e.GetPosition(GameCanvas);

        // 최소 거리 이상 이동 시에만 경로 추가 (노이즈 방지)
        if (_cutPath.Count > 0)
        {
            var last = _cutPath[^1];
            double dx = pos.X - last.X, dy = pos.Y - last.Y;
            if (dx * dx + dy * dy < 16) return;
        }

        _cutPath.Add((pos.X, pos.Y));
        _cutLine?.Points.Add(pos);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCutting || _state != GameState.Cutting) return;
        _isCutting = false;

        if (_cutPath.Count < 2) { _cutLine?.Points.Clear(); return; }

        int cutCount = ClothSim.Cut(_mesh, _cutPath);

        if (cutCount > 0)
        {
            SpawnCutParticles();
            DrawCutMark();
        }

        _cutLine?.Points.Clear();
        _cutPath.Clear();

        // 절단이 유효하면 판정 단계로 전환
        if (cutCount > 0)
            JudgeResult();
    }

    // ── 판정 ──────────────────────────────────────────────

    private void JudgeResult()
    {
        _state = GameState.Judging;

        // 컴포넌트 계산
        var components = _mesh.RecalculateComponents();
        int totalNodes = _mesh.Nodes.Length;

        // 핀 연결 컴포넌트 식별 (고정 노드 포함 여부로 판단)
        // 핀에 연결된 조각은 저울 귀속에서 제외
        var freeParts = components
            .Where(comp => comp.All(n => !n.IsPinned))
            .ToList();

        // 총 자유 노드 수
        int freeTotal = freeParts.Sum(c => c.Count);
        if (freeTotal == 0)
        {
            // 아직 자르지 않았거나 모두 핀에 연결됨 → 계속 절단 허용
            _state = GameState.Cutting;
            return;
        }

        // 저울별 귀속
        var scales = _levelDef.Scales;
        foreach (var sc in scales) sc.Reset();

        foreach (var comp in freeParts)
        {
            double cx = comp.Average(n => n.X);
            foreach (var sc in scales)
            {
                if (sc.Contains(cx))
                {
                    sc.ReceivedNodes += comp.Count;
                    break;
                }
            }
        }

        // 비율 계산 및 성공 판정
        bool allSuccess = true;
        for (int i = 0; i < scales.Length; i++)
        {
            var sc = scales[i];
            sc.ActualRatio = freeTotal > 0 ? (double)sc.ReceivedNodes / freeTotal : 0;
            sc.IsSuccess   = sc.ActualRatio >= sc.MinRatio && sc.ActualRatio <= sc.MaxRatio;
            if (!sc.IsSuccess) allSuccess = false;

            // 저울 비율 텍스트 업데이트
            if (i < _scaleLabels.Length)
            {
                _scaleLabels[i].Text       = $"{sc.ActualRatio * 100:0}%";
                _scaleLabels[i].Foreground = new SolidColorBrush(
                    sc.IsSuccess ? ColScaleOk : ColScaleFail);
                _scaleLabels[i].Foreground.Freeze();
            }

            // 저울 테두리 색상 변경
            if (i < _scaleVisuals.Length && _scaleVisuals[i] is Rectangle r)
            {
                r.Stroke = new SolidColorBrush(
                    sc.IsSuccess ? ColScaleOk : ColScaleFail);
            }
        }

        ShowResult(allSuccess);
    }

    private void ShowResult(bool success)
    {
        if (success)
        {
            if (_curLevel >= LevelData.MaxLevel)
            {
                _state = GameState.GameComplete;
                ResultTitle.Text       = "PERFECT CUT!";
                ResultTitle.Foreground = new SolidColorBrush(ColScaleOk);
                ResultSub.Text         = "모든 레벨을 완주했습니다!";
                ResultAction.Text      = "[ ENTER → 처음부터 ]";
            }
            else
            {
                _state = GameState.Clear;
                ResultTitle.Text       = "PERFECT CUT!";
                ResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                ResultSub.Text         = $"레벨 {_curLevel} 클리어!\n다음: {LevelData.Get(_curLevel + 1).Name}";
                ResultAction.Text      = "[ ENTER → 다음 레벨 ]  [ R → 재도전 ]";
            }

            SpawnSuccessParticles();
        }
        else
        {
            // 실패 — 다시 절단 허용
            _state = GameState.Cutting;
            ResultTitle.Text       = "아직 멀었어요!";
            ResultTitle.Foreground = new SolidColorBrush(ColScaleFail);
            ResultSub.Text         = "저울 비율을 다시 확인하고 추가 절단하거나 리셋하세요.";
            ResultAction.Text      = "[ R → 리셋 ]  [ 계속 절단 가능 ]";
        }

        ResultOverlay.Visibility = Visibility.Visible;
        HintBar.Visibility       = Visibility.Collapsed;

        // 실패 시 3초 후 오버레이 자동 숨김
        if (!success)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                ResultOverlay.Visibility = Visibility.Collapsed;
                HintBar.Visibility       = Visibility.Visible;
                timer.Stop();
            };
            timer.Start();
        }
    }

    // ── 절단 흔적 시각화 ─────────────────────────────────

    private void DrawCutMark()
    {
        if (_cutMarkPath is null || _cutPath.Count < 2) return;

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(_cutPath[0].X, _cutPath[0].Y), false, false);
            for (int i = 1; i < _cutPath.Count; i++)
                ctx.LineTo(new Point(_cutPath[i].X, _cutPath[i].Y), true, false);
        }
        sg.Freeze();
        _cutMarkPath.Data = sg;
    }

    // ── 파티클 ────────────────────────────────────────────

    private void SpawnCutParticles()
    {
        if (_cutPath.Count == 0) return;
        var mid = _cutPath[_cutPath.Count / 2];
        for (int i = 0; i < 12; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 50 + _rng.NextDouble() * 100;
            var e = new Ellipse { Width=3, Height=3, Fill=new SolidColorBrush(ColCutLine) };
            Canvas.SetLeft(e, mid.X); Canvas.SetTop(e, mid.Y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(angle)*speed, Math.Sin(angle)*speed, 0.5));
        }
    }

    private void SpawnSuccessParticles()
    {
        for (int i = 0; i < 40; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 60 + _rng.NextDouble() * 200;
            var col = i % 3 == 0 ? ColScaleOk
                    : i % 3 == 1 ? Color.FromRgb(0xFF,0xD7,0x00)
                    : ColCloth;
            var e = new Ellipse
            {
                Width  = 3 + _rng.NextDouble() * 5,
                Height = 3 + _rng.NextDouble() * 5,
                Fill   = new SolidColorBrush(col)
            };
            double x = ViewW * 0.5, y = ViewH * 0.4;
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(angle)*speed, Math.Sin(angle)*speed, 1.5));
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
            Canvas.SetTop(e,  Canvas.GetTop(e)  + (vy + 300 * dt) * dt);
            e.Opacity = Math.Max(0, nl);
            _particles[i] = (e, vx, vy + 300 * dt, nl);
        }
    }

    // ── 비주얼 클리어 ─────────────────────────────────────

    private void ClearLevelVisuals()
    {
        foreach (var p in _cellPolygons)  GameCanvas.Children.Remove(p);
        foreach (var p in _pinEllipses)   GameCanvas.Children.Remove(p);
        foreach (var (e, _, _, _) in _particles) GameCanvas.Children.Remove(e);
        if (_cutLine     is not null) GameCanvas.Children.Remove(_cutLine);
        if (_cutMarkPath is not null) GameCanvas.Children.Remove(_cutMarkPath);

        // 저울 비주얼 제거 (이전 레벨)
        foreach (var sv in _scaleVisuals) GameCanvas.Children.Remove(sv);
        foreach (var sl in _scaleLabels)  GameCanvas.Children.Remove(sl);

        // 배경 제거 (Canvas를 통째로 지우는 대신 명시 참조 관리)
        // → 배경은 다시 BuildBackground()로 재생성

        _cellPolygons = [];
        _pinEllipses  = [];
        _scaleVisuals = [];
        _scaleLabels  = [];
        _particles.Clear();
        _cutLine     = null;
        _cutMarkPath = null;

        // 남은 배경 요소(선/사각형) 전체 제거
        GameCanvas.Children.Clear();
    }

    // ── HUD / 힌트 ────────────────────────────────────────

    private void UpdateHud()
    {
        if (!IsLoaded) return;
        LevelNumText.Text   = _curLevel.ToString();
        MaterialText.Text   = $" — {_levelDef.MaterialName}";
        LevelTitleText.Text = _levelDef.Name;
        StateText.Text      = _state == GameState.Settling ? "안정화 중..."
                            : _state == GameState.Cutting   ? "절단 가능"
                            : "";
    }

    private void UpdateHintBar()
    {
        if (!IsLoaded) return;
        HintBar.Text = $"마우스 드래그로 절단 | 힌트: {_levelDef.Hint}  |  R: 리셋  ESC: 타이틀";
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

            case Key.R when _state is GameState.Settling or GameState.Cutting
                         or GameState.Judging or GameState.Clear:
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
        SettlingText.Visibility  = Visibility.Collapsed;
        TitlePanel.Visibility    = Visibility.Visible;
    }
}
