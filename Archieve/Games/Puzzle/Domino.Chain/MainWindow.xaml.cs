using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DominoChain.Engine;
using DominoChain.Entities;
using DominoChain.Levels;

namespace DominoChain;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 게임 치수 ──────────────────────────────────────────
    private const double ViewW  = 844;
    private const double ViewH  = 520;
    private const double FloorY = 390;   // 바닥선 Y

    // ── 게임 상태 ──────────────────────────────────────────
    private enum GameState { Title, Edit, Running, Clear, GameComplete }
    private GameState _state = GameState.Title;
    private int _currentLevel = 1;

    // ── 레벨 데이터 ────────────────────────────────────────
    private LevelDef _levelDef = null!;

    // ── 도미노 및 목표 ──────────────────────────────────────
    private readonly List<Domino>  _dominoes = [];
    private Target                 _target   = null!;
    private int                    _placedCount;   // 사용자가 배치한 수

    // ── 슬롯 관리 ──────────────────────────────────────────
    // 슬롯은 사용자가 클릭하면 도미노가 채워지는 위치
    // _slotFilled[slotIndex] = true 이면 이미 채워짐
    private bool[] _slotFilled = [];

    // ── 렌더링 요소 ────────────────────────────────────────
    private readonly List<(Rectangle Rect, Domino Dom)> _domRects   = [];
    private readonly List<(Rectangle Rect, int SlotIdx)> _slotRects = [];
    private UIElement?  _targetVisual;
    private Line?       _floorLine;
    private Rectangle?  _firstHighlight;  // 첫 도미노 강조

    // ── 파티클 ────────────────────────────────────────────
    private readonly List<(Ellipse E, double Vx, double Vy, double Life)> _particles = [];

    // ── 타이머 / 연출 ──────────────────────────────────────
    private double _chainPulse;
    private double _clearTimer;
    private const double ClearDelay = 1.8;  // 클리어 연출 대기

    // ── 게임루프 ──────────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly Random   _rng  = new();

    // ── 색상 상수 ─────────────────────────────────────────
    private static readonly Color ColFixed   = Color.FromRgb(0x60, 0x60, 0x88);
    private static readonly Color ColPlaced  = Color.FromRgb(0x00, 0xD2, 0xA0);
    private static readonly Color ColSlot    = Color.FromRgb(0x33, 0x33, 0x55);
    private static readonly Color ColFallen  = Color.FromRgb(0x44, 0x44, 0x66);
    private static readonly Color ColFloor   = Color.FromRgb(0x33, 0x33, 0x55);
    private static readonly Color ColTarget  = Color.FromRgb(0xFF, 0xA0, 0x00);
    private static readonly Color ColTargetH = Color.FromRgb(0xFF, 0xD7, 0x00);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyDarkTitleBar();
            DrawBackground();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 다크 타이틀바 ─────────────────────────────────────

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource src)
        {
            int v = 1;
            DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int));
        }
    }

    // ── 배경 ──────────────────────────────────────────────

    private void DrawBackground()
    {
        // 격자 배경
        for (int x = 0; x < ViewW; x += 40)
        {
            var line = new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = FloorY,
                Stroke = new SolidColorBrush(Color.FromArgb(20, 0x55, 0x55, 0x88)),
                StrokeThickness = 0.5
            };
            GameCanvas.Children.Add(line);
        }
        for (int y = 0; y < FloorY; y += 40)
        {
            var line = new Line
            {
                X1 = 0, Y1 = y, X2 = ViewW, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(20, 0x55, 0x55, 0x88)),
                StrokeThickness = 0.5
            };
            GameCanvas.Children.Add(line);
        }

        // 바닥선
        _floorLine = new Line
        {
            X1 = 0, Y1 = FloorY, X2 = ViewW, Y2 = FloorY,
            Stroke = new SolidColorBrush(ColFloor),
            StrokeThickness = 2
        };
        GameCanvas.Children.Add(_floorLine);
    }

    // ── 레벨 로드 ─────────────────────────────────────────

    private void LoadLevel(int levelNum)
    {
        ClearLevelVisuals();

        _levelDef    = LevelData.Get(levelNum);
        _dominoes.Clear();
        _placedCount = 0;
        _slotFilled  = new bool[_levelDef.SlotCount];
        _target      = _levelDef.Target;
        _target.Reset();
        _chainPulse  = 0;
        _clearTimer  = 0;

        // 도미노 생성
        foreach (var entry in _levelDef.Entries)
        {
            var dom = new Domino
            {
                PivotX    = entry.PivotX,
                PivotY    = entry.PivotY,
                Kind      = entry.Kind,
                IsSlot    = entry.Kind == DominoKind.Placed,
                SlotIndex = entry.SlotIndex
            };
            // 슬롯은 처음에 비어있으므로 목록에서 제외 (나중에 채울 때 추가)
            _dominoes.Add(dom);
        }

        // 비주얼 구성
        BuildFloorDecoration();
        BuildTargetVisual();
        BuildDominoVisuals();

        _state = GameState.Edit;
        UpdateHud();
        UpdateHintBar();

        HudPanel.Visibility          = Visibility.Visible;
        HintBar.Visibility           = Visibility.Visible;
        TitlePanel.Visibility        = Visibility.Collapsed;
        ClearOverlay.Visibility      = Visibility.Collapsed;
        GameCompleteOverlay.Visibility = Visibility.Collapsed;
        FailFlash.Visibility         = Visibility.Collapsed;
    }

    // ── 비주얼 구성 ───────────────────────────────────────

    private void BuildFloorDecoration()
    {
        // 바닥 음영 띠
        var strip = new Rectangle
        {
            Width  = ViewW,
            Height = ViewH - FloorY,
            Fill   = new SolidColorBrush(Color.FromArgb(80, 0x11, 0x11, 0x22))
        };
        Canvas.SetLeft(strip, 0);
        Canvas.SetTop(strip, FloorY);
        GameCanvas.Children.Add(strip);
    }

    private void BuildTargetVisual()
    {
        var t = _target;
        UIElement visual;

        if (t.Kind == TargetKind.Candle)
        {
            // 초: 직사각형 + 불꽃 원
            var g = new Grid { Width = t.W, Height = t.H };
            var body = new Rectangle
            {
                Width = t.W * 0.6, Height = t.H * 0.75,
                Fill = new SolidColorBrush(ColTarget),
                RadiusX = 2, RadiusY = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Bottom
            };
            var flame = new Ellipse
            {
                Width = t.W * 0.5, Height = t.W * 0.6,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top
            };
            g.Children.Add(body);
            g.Children.Add(flame);
            visual = g;
        }
        else if (t.Kind == TargetKind.Button)
        {
            // 버튼: 둥근 직사각형
            visual = new Rectangle
            {
                Width   = t.W, Height = t.H,
                Fill    = new SolidColorBrush(ColTarget),
                RadiusX = 6, RadiusY = 6
            };
        }
        else
        {
            // 공: 원
            visual = new Ellipse
            {
                Width  = t.W, Height = t.W,
                Fill   = new SolidColorBrush(ColTarget)
            };
        }

        Canvas.SetLeft(visual, t.X - t.W * 0.5);
        Canvas.SetTop(visual,  t.Y - t.H);
        GameCanvas.Children.Add(visual);
        _targetVisual = visual;
    }

    private void BuildDominoVisuals()
    {
        _domRects.Clear();
        _slotRects.Clear();

        foreach (var dom in _dominoes)
        {
            if (dom.IsSlot)
            {
                // 슬롯: 점선 테두리 직사각형 (빈 공간 표시)
                var slotRect = new Rectangle
                {
                    Width           = dom.W,
                    Height          = dom.H,
                    Fill            = new SolidColorBrush(Color.FromArgb(30, 0x00, 0xD2, 0xA0)),
                    Stroke          = new SolidColorBrush(Color.FromArgb(120, 0x00, 0xD2, 0xA0)),
                    StrokeDashArray = new DoubleCollection([4, 3]),
                    StrokeThickness = 1.5,
                    RadiusX         = 2, RadiusY = 2,
                    Cursor          = Cursors.Hand
                };

                // 슬롯 번호 텍스트
                var slotNum = new TextBlock
                {
                    Text       = (dom.SlotIndex + 1).ToString(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 0x00, 0xD2, 0xA0)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                PositionDomino(slotRect, dom);
                GameCanvas.Children.Add(slotRect);
                _slotRects.Add((slotRect, dom.SlotIndex));
                continue;
            }

            // 일반 도미노
            var rect = new Rectangle
            {
                Width   = dom.W,
                Height  = dom.H,
                Fill    = new SolidColorBrush(dom.Kind == DominoKind.Fixed ? ColFixed : ColPlaced),
                RadiusX = 2, RadiusY = 2,
                RenderTransformOrigin = new Point(0.5, 1.0)  // 하단 중심이 피벗
            };

            // 도미노 중앙 선 장식 (실제 도미노 느낌)
            var midLine = new Rectangle
            {
                Width   = dom.W * 0.7,
                Height  = 1.5,
                Fill    = new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xFF, 0xFF)),
                RenderTransformOrigin = new Point(0.5, 1.0)
            };

            PositionDomino(rect, dom);
            GameCanvas.Children.Add(rect);
            _domRects.Add((rect, dom));

            // 첫 번째 고정 도미노 강조 (편집 모드에서 클릭 대상)
            if (dom == _dominoes.FirstOrDefault(d => !d.IsSlot && d.Kind == DominoKind.Fixed))
                _firstHighlight = rect;
        }

        // 첫 도미노 테두리 강조
        UpdateFirstHighlight();
    }

    private void UpdateFirstHighlight()
    {
        if (_firstHighlight is null) return;
        if (_state == GameState.Edit)
        {
            _firstHighlight.Stroke          = new SolidColorBrush(Color.FromArgb(200, 0xFF, 0xD7, 0x00));
            _firstHighlight.StrokeThickness = 1.5;
        }
        else
        {
            _firstHighlight.Stroke = null;
        }
    }

    private void PositionDomino(UIElement elem, Domino dom)
    {
        // 피벗은 하단 중심 → Canvas 좌상단 기준으로 변환
        Canvas.SetLeft(elem, dom.PivotX - dom.W * 0.5);
        Canvas.SetTop(elem,  dom.PivotY - dom.H);
    }

    // ── 슬롯 채우기 ───────────────────────────────────────

    private void TryFillSlot(Point clickPos)
    {
        if (_state != GameState.Edit) return;

        foreach (var (rect, slotIdx) in _slotRects)
        {
            if (_slotFilled[slotIdx]) continue;

            // 클릭이 슬롯 위에 있는지 확인 (히트 박스 여유 포함)
            double left = Canvas.GetLeft(rect) - 8;
            double top  = Canvas.GetTop(rect)  - 8;
            if (clickPos.X >= left && clickPos.X <= left + rect.Width  + 16 &&
                clickPos.Y >= top  && clickPos.Y <= top  + rect.Height + 16)
            {
                FillSlot(slotIdx, rect);
                return;
            }
        }
    }

    private void FillSlot(int slotIdx, Rectangle slotRect)
    {
        _slotFilled[slotIdx] = true;
        _placedCount++;

        // 슬롯에 해당하는 도미노 찾기
        var dom = _dominoes.First(d => d.IsSlot && d.SlotIndex == slotIdx);
        dom.IsSlot = false;  // 이제 실제 도미노

        // 슬롯 비주얼 → 실제 도미노 비주얼로 교체
        slotRect.Fill            = new SolidColorBrush(ColPlaced);
        slotRect.Stroke          = null;
        slotRect.StrokeDashArray = null;
        slotRect.StrokeThickness = 0;
        slotRect.Fill            = new SolidColorBrush(ColPlaced);

        // slotRects에서 제거하고 domRects에 추가
        var idx = _slotRects.FindIndex(s => s.SlotIdx == slotIdx);
        if (idx >= 0) _slotRects.RemoveAt(idx);
        _domRects.Add((slotRect, dom));

        // 배치 파티클
        SpawnPlaceParticles(dom.PivotX, dom.PivotY - dom.H * 0.5);

        UpdateHud();
        UpdateHintBar();
    }

    // ── 시뮬레이션 시작 ──────────────────────────────────

    private void StartSimulation()
    {
        if (_state != GameState.Edit) return;

        // 첫 번째 도미노(슬롯 아닌 것 중 맨 앞)를 쓰러뜨림
        var first = _dominoes.FirstOrDefault(d => !d.IsSlot && d.State == DominoState.Standing);
        if (first is null) return;

        Physics.Topple(first, +1);

        _state = GameState.Running;
        ModeText.Text = "[ RUN ]";
        ModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0xA0));

        // 첫 도미노 강조 제거
        if (_firstHighlight is not null) _firstHighlight.Stroke = null;

        UpdateHintBar();
    }

    // ── 리셋 ─────────────────────────────────────────────

    private void ResetLevel()
    {
        LoadLevel(_currentLevel);
    }

    // ── 업데이트 ──────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state == GameState.Running)
        {
            // 시뮬레이션 스텝 (슬롯이 아닌 도미노만)
            var activeDoms = _dominoes.Where(d => !d.IsSlot).ToList();
            Physics.Step(activeDoms, dt);

            // 목표 충돌 체크
            CheckTargetHit(activeDoms);

            // 클리어 딜레이
            if (_clearTimer > 0)
            {
                _clearTimer -= dt;
                if (_clearTimer <= 0)
                    ShowClear();
            }
        }

        _chainPulse += dt * 2.5;
        UpdateParticles(dt);
    }

    private void CheckTargetHit(List<Domino> activeDoms)
    {
        if (_target.IsHit) return;

        // 마지막 쓰러지는 도미노의 상단 모서리가 목표에 닿으면 클리어
        var lastFalling = activeDoms.LastOrDefault(d => d.State == DominoState.Falling);
        if (lastFalling is null)
        {
            // 모든 도미노가 Fallen or Standing — 실패 또는 다음 도미노 확인
            var lastFallen = activeDoms.LastOrDefault(d => d.State == DominoState.Fallen);
            if (lastFallen is not null)
            {
                var (tx, ty) = lastFallen.TopCenter;
                if (_target.CheckCollision(tx, ty))
                    TriggerClear();
                else if (activeDoms.All(d => d.State != DominoState.Falling))
                    TriggerFail();
            }
            return;
        }

        var (topX, topY) = lastFalling.TopCenter;
        if (_target.CheckCollision(topX, topY))
            TriggerClear();
    }

    private void TriggerClear()
    {
        if (_target.IsHit) return;
        _target.IsHit = true;

        // 목표 색상 변경
        if (_targetVisual is Rectangle r) r.Fill = new SolidColorBrush(ColTargetH);
        if (_targetVisual is Grid g)
        {
            foreach (UIElement c in g.Children)
                if (c is Rectangle rb) rb.Fill = new SolidColorBrush(ColTargetH);
        }
        if (_targetVisual is Ellipse e) e.Fill = new SolidColorBrush(ColTargetH);

        // 파티클 폭발
        SpawnClearParticles(_target.X, _target.Y - _target.H * 0.5);

        _clearTimer = ClearDelay;
    }

    private void TriggerFail()
    {
        if (_target.IsHit) return;
        // 체인이 끊어졌을 때 빨간 플래시
        FailFlash.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        timer.Tick += (_, _) => { FailFlash.Visibility = Visibility.Collapsed; timer.Stop(); };
        timer.Start();
    }

    private void ShowClear()
    {
        if (_currentLevel >= LevelData.MaxLevel)
        {
            _state = GameState.GameComplete;
            GameCompleteOverlay.Visibility = Visibility.Visible;
            HudPanel.Visibility = Visibility.Collapsed;
            HintBar.Visibility  = Visibility.Collapsed;
        }
        else
        {
            _state = GameState.Clear;
            ClearTitle.Text = "CHAIN COMPLETE!";
            ClearSub.Text   = $"레벨 {_currentLevel} 클리어!";
            ClearOverlay.Visibility = Visibility.Visible;
            HudPanel.Visibility     = Visibility.Collapsed;
            HintBar.Visibility      = Visibility.Collapsed;
        }
    }

    // ── 렌더링 ────────────────────────────────────────────

    private void OnRender()
    {
        if (_state is GameState.Title or GameState.GameComplete) return;

        RenderDominoes();
        RenderSlots();
        PulseTarget();
    }

    private void RenderDominoes()
    {
        foreach (var (rect, dom) in _domRects)
        {
            if (dom.State == DominoState.Falling || dom.State == DominoState.Fallen)
            {
                // 기울기 반영 (하단 중심 피벗)
                double deg = dom.Angle * (180.0 / Math.PI);
                rect.RenderTransform = new RotateTransform(deg);

                // 쓰러진 도미노 색상 어둡게
                if (dom.State == DominoState.Fallen)
                    rect.Fill = new SolidColorBrush(ColFallen);
            }
        }
    }

    private void RenderSlots()
    {
        // 슬롯 깜빡임 (편집 모드에서만)
        if (_state != GameState.Edit) return;
        double alpha = (byte)(Math.Abs(Math.Sin(_chainPulse)) * 80 + 20);
        foreach (var (rect, _) in _slotRects)
            rect.Opacity = 0.5 + 0.5 * Math.Abs(Math.Sin(_chainPulse * 0.8));
    }

    private void PulseTarget()
    {
        if (_targetVisual is null) return;
        double pulse = 0.75 + 0.25 * Math.Sin(_chainPulse * 1.2);
        if (!_target.IsHit)
            _targetVisual.Opacity = pulse;
        else
            _targetVisual.Opacity = 1.0;
    }

    // ── 파티클 ────────────────────────────────────────────

    private void SpawnPlaceParticles(double x, double y)
    {
        for (int i = 0; i < 8; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 40 + _rng.NextDouble() * 80;
            var e = new Ellipse
            {
                Width = 3, Height = 3,
                Fill  = new SolidColorBrush(ColPlaced)
            };
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0.5));
        }
    }

    private void SpawnClearParticles(double x, double y)
    {
        for (int i = 0; i < 30; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 80 + _rng.NextDouble() * 180;
            var col = i % 3 == 0 ? ColTargetH : (i % 3 == 1 ? ColPlaced : Color.FromRgb(0xFF, 0x66, 0xAA));
            var e = new Ellipse
            {
                Width = 4 + _rng.NextDouble() * 4,
                Height = 4 + _rng.NextDouble() * 4,
                Fill = new SolidColorBrush(col)
            };
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            GameCanvas.Children.Add(e);
            _particles.Add((e, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 1.2));
        }
    }

    private void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (e, vx, vy, life) = _particles[i];
            double newLife = life - dt;
            if (newLife <= 0)
            {
                GameCanvas.Children.Remove(e);
                _particles.RemoveAt(i);
                continue;
            }
            Canvas.SetLeft(e, Canvas.GetLeft(e) + vx * dt);
            Canvas.SetTop(e,  Canvas.GetTop(e)  + (vy + 200 * dt) * dt);  // 중력 약하게
            e.Opacity = Math.Max(0, newLife);
            _particles[i] = (e, vx, vy + 200 * dt, newLife);
        }
    }

    // ── 클리어 비주얼 ─────────────────────────────────────

    private void ClearLevelVisuals()
    {
        foreach (var (r, _) in _domRects)  GameCanvas.Children.Remove(r);
        foreach (var (r, _) in _slotRects) GameCanvas.Children.Remove(r);
        foreach (var (e, _, _, _) in _particles) GameCanvas.Children.Remove(e);
        if (_targetVisual is not null) GameCanvas.Children.Remove(_targetVisual);

        _domRects.Clear();
        _slotRects.Clear();
        _particles.Clear();
        _targetVisual    = null;
        _firstHighlight  = null;

        // 바닥 이후에 추가된 요소들만 제거 (바닥선/격자는 유지)
        // 방법: 특정 Children 범위를 지우는 대신 명시적 참조 관리로 충분
    }

    // ── HUD / 힌트 ────────────────────────────────────────

    private void UpdateHud()
    {
        if (!IsLoaded) return;
        LevelNumText.Text   = _currentLevel.ToString();
        LevelTitleText.Text = $" — {_levelDef.Title}";
        ModeText.Text       = _state == GameState.Running ? "[ RUN ]" : "[ EDIT ]";
        ModeText.Foreground = _state == GameState.Running
            ? new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0xA0))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));

        int remaining = _levelDef.SlotCount - _placedCount;
        RemainingText.Text = remaining.ToString();
        RemainingText.Foreground = remaining == 0
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0xA0));
    }

    private void UpdateHintBar()
    {
        if (!IsLoaded) return;
        if (_state == GameState.Edit)
        {
            int rem = _levelDef.SlotCount - _placedCount;
            HintBar.Text = rem > 0
                ? $"빈 슬롯({rem}개)을 클릭해 도미노 배치 → SPACE: 실행  R: 리셋  ESC: 타이틀"
                : $"도미노 배치 완료! SPACE: 실행  R: 리셋  ESC: 타이틀  |  힌트: {_levelDef.Hint}";
        }
        else
        {
            HintBar.Text = "R: 리셋  ESC: 타이틀";
        }
    }

    // ── 입력 ──────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _state == GameState.Title:
                _currentLevel = 1;
                LoadLevel(_currentLevel);
                break;

            case Key.Enter when _state == GameState.Clear:
                _currentLevel++;
                LoadLevel(_currentLevel);
                break;

            case Key.Enter when _state == GameState.GameComplete:
                _state = GameState.Title;
                GameCompleteOverlay.Visibility = Visibility.Collapsed;
                HudPanel.Visibility = Visibility.Collapsed;
                HintBar.Visibility  = Visibility.Collapsed;
                TitlePanel.Visibility = Visibility.Visible;
                break;

            case Key.Space when _state == GameState.Edit:
                StartSimulation();
                break;

            case Key.Space when _state == GameState.Running:
                // 실행 중 SPACE → 편집 모드로 복귀 (리셋)
                ResetLevel();
                break;

            case Key.R when (_state == GameState.Edit || _state == GameState.Running):
                ResetLevel();
                break;

            case Key.Escape when _state is GameState.Edit or GameState.Running or GameState.Clear:
                ShowTitle();
                break;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Edit) return;
        var pos = e.GetPosition(GameCanvas);
        TryFillSlot(pos);
    }

    private void ShowTitle()
    {
        _state = GameState.Title;
        ClearLevelVisuals();
        HudPanel.Visibility          = Visibility.Collapsed;
        HintBar.Visibility           = Visibility.Collapsed;
        ClearOverlay.Visibility      = Visibility.Collapsed;
        GameCompleteOverlay.Visibility = Visibility.Collapsed;
        FailFlash.Visibility         = Visibility.Collapsed;
        TitlePanel.Visibility        = Visibility.Visible;
    }
}
