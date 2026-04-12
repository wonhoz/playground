using System.Windows;
using System.Windows.Media;
using NeonSlice.Models;
using NeonSlice.Services;

namespace NeonSlice.Engine;

/// <summary>WPF DrawingVisual 기반 게임 렌더러 (FrameworkElement)</summary>
public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly DrawingVisual _visual = new();

    public DrawingVisualHost()
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;
    protected override Size MeasureOverride(Size availableSize) => availableSize;
    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public DrawingContext Open() => _visual.RenderOpen();
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Neon.Slice 게임 엔진</summary>
public sealed class GameEngine
{
    // ── 외부 참조 ──────────────────────────────────────────────────────────
    private readonly DrawingVisualHost _host;
    private double _width;
    private double _height;

    // ── 게임 상태 ──────────────────────────────────────────────────────────
    public GameMode   Mode       { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsPaused  { get; private set; }
    public bool IsFever   => _inFever;
    private bool _gameOver;

    public int  Score     { get; private set; }
    public int  Combo     { get; private set; }
    public int  MaxCombo  { get; private set; }
    public int  Lives     { get; private set; }   // Classic
    public int  Sliced    { get; private set; }
    public int  Missed    { get; private set; }
    public double TimeLeft { get; private set; }   // TimeAttack(초)
    public int  ZenSlicesLeft { get; private set; } // Zen 모드 남은 슬라이스

    private const int MaxLives    = 3;
    private const int ZenTotal    = 30; // Zen 모드 총 슬라이스 수

    // ── 슬로모션 ───────────────────────────────────────────────────────────
    private double _slowMoRemaining;   // 남은 슬로모션 시간(초)
    private bool   _inFever;           // 콤보 피버 여부
    private const double FeverDuration = 1.5;
    private const double IceDuration   = 3.0;

    // ── 폭탄 플래시 ────────────────────────────────────────────────────────
    private double _bombFlashRemaining; // 남은 폭탄 플래시 시간(초)
    private const double BombFlashDuration = 0.25;

    // ── 콤보 이정표 플래시 ─────────────────────────────────────────────────
    private double _comboFlashRemaining;
    private const double ComboFlashDuration = 0.40;

    // ── 게임 오브젝트 ───────────────────────────────────────────────────────
    private readonly List<NeonShape>   _shapes    = [];
    private readonly List<Particle>    _particles = [];
    private readonly List<SlicedHalf>  _halves    = [];

    // ── 스폰 ───────────────────────────────────────────────────────────────
    private double _spawnTimer;
    private double _spawnInterval = 1.4;   // 초 (난이도와 함께 감소)
    private double _spawnIntervalBase = 1.4; // 난이도별 기준 간격
    private double _elapsedGame;

    // ── 마우스 궤적 ────────────────────────────────────────────────────────
    private readonly Queue<Point> _trail = new();
    private const int TrailMaxLen = 6;
    private Point _lastMousePos;
    private bool  _isSlicing;

    // ── 렌더링 ─────────────────────────────────────────────────────────────
    private DateTime _lastFrame = DateTime.Now;

    // ── 이벤트 ─────────────────────────────────────────────────────────────
    public event Action<GameResult>? GameOver;
    public event Action? StateChanged; // HUD 갱신
    public Action<SoundCue>? PlaySound; // 사운드 콜백 (MainWindow에서 SoundService.Play 할당)

    // ── 색상 팔레트 ────────────────────────────────────────────────────────
    private static readonly Color[] NeonPalette =
    [
        Color.FromRgb(0,   255, 255), // 시안
        Color.FromRgb(255, 45,  120), // 핑크
        Color.FromRgb(74,  222, 128), // 그린
        Color.FromRgb(191, 95,  255), // 퍼플
        Color.FromRgb(255, 107, 0),   // 오렌지
        Color.FromRgb(255, 230, 0),   // 옐로
    ];

    private static readonly Random Rng = new();

    // ── 렌더링 캐시 (매 프레임 생성 방지) ────────────────────────────────────
    private static readonly Pen GridPen =
        new(new SolidColorBrush(Color.FromArgb(20, 0, 200, 255)), 0.5);
    private static readonly SolidColorBrush BgBrush =
        new(Color.FromRgb(10, 10, 15));
    private static readonly SolidColorBrush CursorCenterBrush =
        new(Color.FromArgb(230, 200, 255, 255));
    private static readonly Pen CursorRingPen =
        new(new SolidColorBrush(Color.FromArgb(180, 0, 255, 255)), 1.5);
    // 슬라이스 궤적 펜 캐시 (TrailMaxLen-1 개, 인덱스 0=맨 끝/가장 가늘고 투명)
    private static readonly Pen[] TrailPens = Enumerable.Range(1, TrailMaxLen - 1)
        .Select(i => { var p = new Pen(Brushes.White, 2.5 - i * 0.3); p.Freeze(); return p; })
        .ToArray();

    // DrawShape 브러시·펜 캐시 (Color 키 — NeonPalette 6가지 → 소량)
    private static readonly Dictionary<(Color col, byte a1), RadialGradientBrush> _radialGlowCache  = new();
    private static readonly Dictionary<Color, RadialGradientBrush>                _circleBodyCache  = new();
    private static readonly Dictionary<Color, LinearGradientBrush>                _polyBodyCache    = new();
    private static readonly Dictionary<Color, Pen>                                _borderPenCache   = new();
    private static readonly Dictionary<Color, Pen>                                _lightningPenCache = new();

    // DrawHalf/Particle 캐시 — PushOpacity로 페이드 처리하므로 전체 알파 기준 고정
    private static readonly Dictionary<Color, Pen>              _halfPenCache   = new();
    private static readonly Dictionary<Color, SolidColorBrush>  _halfFillCache  = new();
    private static readonly Dictionary<Color, SolidColorBrush>  _particleCache  = new();

    private static RadialGradientBrush GetRadialGlow(Color col, byte outerAlpha)
    {
        var key = (col, outerAlpha);
        if (_radialGlowCache.TryGetValue(key, out var b)) return b;
        b = new RadialGradientBrush(Color.FromArgb(outerAlpha, col.R, col.G, col.B),
                                    Color.FromArgb(0, col.R, col.G, col.B));
        b.Freeze();
        return _radialGlowCache[key] = b;
    }
    private static RadialGradientBrush GetCircleBody(Color col)
    {
        if (_circleBodyCache.TryGetValue(col, out var b)) return b;
        b = new RadialGradientBrush(Color.FromArgb(200, col.R, col.G, col.B),
                                    Color.FromArgb(80, (byte)(col.R / 2), (byte)(col.G / 2), (byte)(col.B / 2)));
        b.Freeze();
        return _circleBodyCache[col] = b;
    }
    private static LinearGradientBrush GetPolyBody(Color col)
    {
        if (_polyBodyCache.TryGetValue(col, out var b)) return b;
        b = new LinearGradientBrush(Color.FromArgb(180, col.R, col.G, col.B),
                                    Color.FromArgb(60, (byte)(col.R / 2), (byte)(col.G / 2), (byte)(col.B / 2)), 90);
        b.Freeze();
        return _polyBodyCache[col] = b;
    }
    private static Pen GetBorderPen(Color col)
    {
        if (_borderPenCache.TryGetValue(col, out var p)) return p;
        p = new Pen(new SolidColorBrush(Color.FromArgb(255, col.R, col.G, col.B)), 2);
        p.Freeze();
        return _borderPenCache[col] = p;
    }
    private static Pen GetLightningPen(Color col)
    {
        if (_lightningPenCache.TryGetValue(col, out var p)) return p;
        p = new Pen(new SolidColorBrush(Color.FromArgb(255, col.R, col.G, col.B)), 2.5);
        p.Freeze();
        return _lightningPenCache[col] = p;
    }
    private static Pen GetHalfPen(Color col)
    {
        if (_halfPenCache.TryGetValue(col, out var p)) return p;
        p = new Pen(new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)), 1.5);
        p.Freeze();
        return _halfPenCache[col] = p;
    }
    private static SolidColorBrush GetHalfFill(Color col)
    {
        if (_halfFillCache.TryGetValue(col, out var b)) return b;
        b = new SolidColorBrush(Color.FromArgb(120, col.R, col.G, col.B));
        b.Freeze();
        return _halfFillCache[col] = b;
    }
    private static SolidColorBrush GetParticleBrush(Color col)
    {
        if (_particleCache.TryGetValue(col, out var b)) return b;
        b = new SolidColorBrush(Color.FromArgb(255, col.R, col.G, col.B));
        b.Freeze();
        return _particleCache[col] = b;
    }

    // ─────────────────────────────────────────────────────────────────────
    public GameEngine(DrawingVisualHost host)
    {
        _host = host;
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────
    public void StartGame(GameMode mode, Difficulty difficulty = Difficulty.Normal)
    {
        Mode       = mode;
        Difficulty = difficulty;
        Score       = 0;
        Combo       = 0;
        MaxCombo    = 0;
        Lives       = MaxLives;
        Sliced      = 0;
        Missed      = 0;
        TimeLeft    = 60;
        ZenSlicesLeft = ZenTotal;
        _elapsedGame    = 0;
        _spawnTimer     = 0;
        _spawnInterval = _spawnIntervalBase = difficulty switch
        {
            Difficulty.Easy => 2.0,
            Difficulty.Hard => 1.0,
            _               => 1.4,
        };
        _slowMoRemaining = 0;
        _inFever        = false;
        _gameOver       = false;
        _bombFlashRemaining = 0;
        _comboFlashRemaining = 0;
        _shapes.Clear();
        _particles.Clear();
        _halves.Clear();
        _trail.Clear();

        IsRunning = true;
        IsPaused  = false;
        _lastFrame = DateTime.Now;
    }

    public void Pause()  { IsPaused = true;  }
    public void Resume() { IsPaused = false; _lastFrame = DateTime.Now; }

    // ── CompositionTarget.Rendering 콜백 ─────────────────────────────────
    public void OnRender(object? sender, EventArgs e)
    {
        if (!IsRunning || _gameOver) return;

        // 매 프레임 렌더 영역 크기를 _host에서 직접 읽음
        // UpdateLayout() 타이밍에 의존하지 않아 크기=0 문제 완전 방지
        _width  = _host.ActualWidth;
        _height = _host.ActualHeight;

        var now = DateTime.Now;
        var realDt = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        realDt = Math.Min(realDt, 0.05); // 프레임 드랍 방지

        if (IsPaused) { DrawFrame(0); return; }

        // 크기 미확정 시 게임 로직 건너뜀 — 도형 즉시 missed(IsOffScreen) 방지
        if (_width <= 0 || _height <= 0) return;

        // 슬로모션 배율
        var slowFactor = _slowMoRemaining > 0 ? 0.25 : 1.0;
        var dt = realDt * slowFactor;

        if (_slowMoRemaining > 0)
        {
            _slowMoRemaining -= realDt;
            if (_slowMoRemaining < 0) _slowMoRemaining = 0;
        }

        Update(dt, realDt);
        DrawFrame(dt);
    }

    // ── 마우스 입력 ───────────────────────────────────────────────────────
    public void OnMouseMove(Point pos)
    {
        // 슬라이싱 여부와 무관하게 커서 위치 항상 업데이트 (커서 dot 렌더용)
        _lastMousePos = pos;

        if (_isSlicing)
        {
            _trail.Enqueue(pos);
            if (_trail.Count > TrailMaxLen) _trail.Dequeue();

            if (_trail.Count >= 2)
                CheckSlice(_trail.ElementAt(_trail.Count - 2), pos);
        }
    }

    public void OnMouseDown(Point pos)
    {
        _isSlicing = true;
        _trail.Clear();
        _trail.Enqueue(pos);
        _lastMousePos = pos;
    }

    public void OnMouseUp()
    {
        _isSlicing = false;
        _trail.Clear();
    }

    // ── 업데이트 ──────────────────────────────────────────────────────────
    private void Update(double dt, double realDt)
    {
        _elapsedGame += realDt;

        if (_bombFlashRemaining > 0)
        {
            _bombFlashRemaining -= realDt;
            if (_bombFlashRemaining < 0) _bombFlashRemaining = 0;
        }

        if (_comboFlashRemaining > 0)
        {
            _comboFlashRemaining -= realDt;
            if (_comboFlashRemaining < 0) _comboFlashRemaining = 0;
        }

        // 모드별 업데이트
        if (Mode == GameMode.TimeAttack)
        {
            TimeLeft -= realDt;
            if (TimeLeft <= 0) { TimeLeft = 0; EndGame(); return; }
        }

        // 스폰
        _spawnTimer -= realDt;
        if (_spawnTimer <= 0)
        {
            SpawnShape();
            _spawnTimer = _spawnInterval * (0.85 + Rng.NextDouble() * 0.3);
            // 난이도 증가 — 난이도별 기준에서 감소, 최솟값도 난이도별로 차등
            var minInterval = _spawnIntervalBase switch { >= 1.8 => 0.80, <= 1.1 => 0.40, _ => 0.55 };
            _spawnInterval = Math.Max(minInterval, _spawnIntervalBase - _elapsedGame * 0.012);
        }

        // 도형 업데이트
        foreach (var s in _shapes)
            s.Update(dt);

        // 화면 밖 처리 (CheckSlice에서 IsSliced 처리가 완료된 것은 여기서 제거)
        for (var i = _shapes.Count - 1; i >= 0; i--)
        {
            var s = _shapes[i];
            // CheckSlice 3단계에서 제거되지 않은 IsSliced 도형 정리
            if (s.IsSliced) { _shapes.RemoveAt(i); continue; }
            if (!s.IsOffScreen(_height)) continue;

            // 특수 도형은 목숨 소모 없음
            if (s.Type != ShapeType.Bomb && s.Type != ShapeType.Lightning &&
                s.Type != ShapeType.Ice  && s.Type != ShapeType.Star)
            {
                Missed++;
                Combo = 0; // 콤보 리셋
                PlaySound?.Invoke(SoundCue.Miss);
                if (Mode == GameMode.Classic)
                {
                    Lives--;
                    StateChanged?.Invoke();
                    if (Lives <= 0) { _shapes.RemoveAt(i); EndGame(); return; }
                }
            }
            _shapes.RemoveAt(i);
        }

        // 반쪽 + 파티클 업데이트
        foreach (var h in _halves) h.Update(dt);
        foreach (var p in _particles) p.Update(dt);
        _halves.RemoveAll(h => h.IsDead);
        _particles.RemoveAll(p => p.IsDead);

        StateChanged?.Invoke();
    }

    // ── 스폰 ─────────────────────────────────────────────────────────────
    private void SpawnShape()
    {
        // 특수 도형 출현 확률 (Easy: 폭탄 절반)
        var roll = Rng.NextDouble();
        ShapeType type;
        var bombThreshold = Difficulty == Difficulty.Easy ? 0.04 : 0.08;
        if      (roll < bombThreshold)           type = ShapeType.Bomb;
        else if (roll < bombThreshold + 0.03)    type = ShapeType.Lightning;
        else if (roll < bombThreshold + 0.06)    type = ShapeType.Ice;
        else if (roll < bombThreshold + 0.10)    type = ShapeType.Star;
        else
        {
            var shapes = new[] { ShapeType.Circle, ShapeType.Triangle, ShapeType.Square, ShapeType.Pentagon };
            type = shapes[Rng.Next(shapes.Length)];
        }

        // 특수 도형은 의미 전달을 위해 고정 색상 (Star=옐로, Ice=아이스블루)
        var color = type switch
        {
            ShapeType.Star => Color.FromRgb(255, 230, 0),
            ShapeType.Ice  => Color.FromRgb(120, 220, 255),
            _              => NeonPalette[Rng.Next(NeonPalette.Length)],
        };
        var radius = type == ShapeType.Bomb ? 22.0 : 28 + Rng.NextDouble() * 14;

        // 화면 하단 중간에서 솟아오름
        var x = _width * (0.15 + Rng.NextDouble() * 0.7);
        var spawnY = _height + radius + 10;

        // 상향 속도 (난이도별 조정)
        var vyBase = Difficulty switch
        {
            Difficulty.Easy => 420.0,
            Difficulty.Hard => 540.0,
            _               => 480.0,
        };
        var vy = -(vyBase + Rng.NextDouble() * 180); // 음수 = 위쪽
        var vx = (Rng.NextDouble() - 0.5) * 220;
        var angVel = (Rng.NextDouble() - 0.5) * 160;

        _shapes.Add(new NeonShape
        {
            Type            = type,
            NeonColor       = color,
            X               = x,
            Y               = spawnY,
            Vx              = vx,
            Vy              = vy,
            Radius          = radius,
            AngularVelocity = angVel,
        });
    }

    // ── 슬라이스 판정 ─────────────────────────────────────────────────────
    private void CheckSlice(Point p1, Point p2)
    {
        // 1단계: 교차 도형 목록 수집 (순회 중 컬렉션 변경 없음)
        List<NeonShape>? hits = null;
        foreach (var s in _shapes)
        {
            if (s.IsSliced || s.IsAboveScreen()) continue;
            if (!LineCircleIntersect(p1, p2, new Point(s.X, s.Y), s.Radius)) continue;
            (hits ??= []).Add(s);
        }
        if (hits is null) return;

        // 2단계: 슬라이스 처리 (ProcessSlice가 _shapes 내부를 수정할 수 있으므로 분리)
        foreach (var s in hits)
        {
            if (s.IsSliced) continue; // Lightning 처리 중 이미 제거된 경우 방지
            s.IsSliced = true;
            ProcessSlice(s);
        }

        // 3단계: IsSliced 도형 일괄 제거
        _shapes.RemoveAll(s => s.IsSliced);
    }

    /// <summary>선분-원 교차 판정</summary>
    private static bool LineCircleIntersect(Point p1, Point p2, Point c, double r)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var fx = p1.X - c.X;
        var fy = p1.Y - c.Y;

        var a = dx * dx + dy * dy;
        if (a < 1e-9) return false;

        var b = 2 * (fx * dx + fy * dy);
        var cv = fx * fx + fy * fy - r * r;
        var disc = b * b - 4 * a * cv;
        if (disc < 0) return false;

        var sqrtDisc = Math.Sqrt(disc);
        var t1 = (-b - sqrtDisc) / (2 * a);
        var t2 = (-b + sqrtDisc) / (2 * a);
        return (t1 >= 0 && t1 <= 1) || (t2 >= 0 && t2 <= 1) || (t1 < 0 && t2 > 1);
    }

    private void ProcessSlice(NeonShape shape)
    {
        switch (shape.Type)
        {
            case ShapeType.Bomb:
                // 폭탄: 감점 + 콤보 리셋 + Classic 모드 목숨 감소 + 적색 플래시
                Score = Math.Max(0, Score - 15);
                Combo = 0;
                if (Mode == GameMode.Classic) Lives--;
                _bombFlashRemaining = BombFlashDuration;
                PlaySound?.Invoke(SoundCue.Bomb);
                SpawnParticles(shape.X, shape.Y, Color.FromRgb(255, 80, 0), 12);
                StateChanged?.Invoke();
                if (Mode == GameMode.Classic && Lives <= 0) { EndGame(); return; }
                return;

            case ShapeType.Lightning:
                // 번개: 화면 클리어 — IsSliced 플래그만 설정, 실제 제거는 CheckSlice 3단계에서
                var cleared = 0;
                foreach (var s in _shapes)
                {
                    if (s.IsSliced || s.Type == ShapeType.Bomb) continue;
                    s.IsSliced = true;
                    SpawnHalves(s);
                    SpawnParticles(s.X, s.Y, s.NeonColor, 6);
                    cleared++;
                }
                Score += cleared * 5;
                Combo += cleared;
                SpawnParticles(shape.X, shape.Y, Color.FromRgb(255, 230, 0), 16);
                if (Combo > MaxCombo) MaxCombo = Combo;
                Sliced++;
                PlaySound?.Invoke(SoundCue.Lightning);
                StateChanged?.Invoke();
                return;

            case ShapeType.Ice:
                // 얼음: 슬로모션 3초
                _slowMoRemaining = IceDuration;
                PlaySound?.Invoke(SoundCue.Ice);
                SpawnParticles(shape.X, shape.Y, Color.FromRgb(120, 220, 255), 10);
                Sliced++;
                StateChanged?.Invoke();
                return;

            case ShapeType.Star:
                // 별: 보너스 +10
                Score += 10;
                Sliced++;
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
                PlaySound?.Invoke(SoundCue.Star);
                SpawnParticles(shape.X, shape.Y, Color.FromRgb(255, 230, 0), 14);
                CheckFever();
                StateChanged?.Invoke();
                return;

            default:
                // 일반 도형
                var pts = 1 + (Combo / 3); // 콤보 보너스
                Score += pts;
                Sliced++;
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
                // 10콤보 이정표마다 화면 플래시 (5콤보는 피버가 따로 처리)
                if (Combo >= 10 && Combo % 10 == 0) _comboFlashRemaining = ComboFlashDuration;
                SpawnHalves(shape);
                SpawnParticles(shape.X, shape.Y, shape.NeonColor, 10);
                PlaySound?.Invoke(_inFever ? SoundCue.SliceFever : SoundCue.Slice);
                CheckFever();

                if (Mode == GameMode.Zen)
                {
                    ZenSlicesLeft--;
                    if (ZenSlicesLeft <= 0) { EndGame(); return; }
                }
                StateChanged?.Invoke();
                break;
        }
    }

    private void CheckFever()
    {
        if (Combo >= 5 && !_inFever)
        {
            _inFever = true;
            _slowMoRemaining = FeverDuration;
            PlaySound?.Invoke(SoundCue.Fever);
        }
        else if (Combo < 5)
        {
            _inFever = false;
        }
    }

    private void SpawnHalves(NeonShape s)
    {
        var spread = 60 + Rng.NextDouble() * 40;
        for (var h = 0; h < 2; h++)
        {
            var vxH = s.Vx + (h == 0 ? spread : -spread);
            var vyH = s.Vy - 40 - Rng.NextDouble() * 40;
            _halves.Add(new SlicedHalf
            {
                X               = s.X,
                Y               = s.Y,
                Vx              = vxH,
                Vy              = vyH,
                Radius          = s.Radius * 0.85,
                Rotation        = s.Rotation,
                AngularVelocity = s.AngularVelocity + (h == 0 ? 80 : -80),
                NeonColor       = s.NeonColor,
                Type            = s.Type,
                Half            = h,
            });
        }
    }

    private void SpawnParticles(double x, double y, Color color, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = Rng.NextDouble() * Math.PI * 2;
            var speed = 80 + Rng.NextDouble() * 200;
            _particles.Add(new Particle
            {
                X       = x,
                Y       = y,
                Vx      = Math.Cos(angle) * speed,
                Vy      = Math.Sin(angle) * speed - 60,
                Color   = color,
                MaxLife = 0.5 + Rng.NextDouble() * 0.4,
                Radius  = 2 + Rng.NextDouble() * 4,
                Life    = 1,
            });
        }
    }

    private void EndGame()
    {
        IsRunning = false;
        _gameOver  = true;
        PlaySound?.Invoke(SoundCue.GameOver);
        DrawFrame(0);
        GameOver?.Invoke(new GameResult
        {
            Mode       = Mode,
            Difficulty = Difficulty,
            Score      = Score,
            MaxCombo   = MaxCombo,
            Sliced     = Sliced,
            Missed     = Missed,
        });
    }

    // ── 렌더링 ────────────────────────────────────────────────────────────
    private void DrawFrame(double dt)
    {
        using var dc = _host.Open();

        // 배경
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, _width, _height));

        // 격자 배경 라인 (사이버펑크 느낌)
        DrawGrid(dc);

        // 슬라이스 궤적
        DrawTrail(dc);

        // 슬라이스된 반쪽
        foreach (var h in _halves)
            DrawHalf(dc, h);

        // 파티클
        foreach (var p in _particles)
            DrawParticle(dc, p);

        // 도형
        foreach (var s in _shapes)
            DrawShape(dc, s);

        // 슬로모션 오버레이
        if (_slowMoRemaining > 0)
            DrawSlowMoOverlay(dc);

        // 폭탄 플래시 오버레이
        if (_bombFlashRemaining > 0)
            DrawBombFlash(dc);

        // 콤보 이정표 플래시
        if (_comboFlashRemaining > 0)
            DrawComboFlash(dc);

        // 커서 dot (항상 최상위)
        DrawCursor(dc);
    }

    private void DrawGrid(DrawingContext dc)
    {
        var step = 60.0;
        for (var x = 0.0; x < _width; x += step)
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, _height));
        for (var y = 0.0; y < _height; y += step)
            dc.DrawLine(GridPen, new Point(0, y), new Point(_width, y));
    }

    private void DrawTrail(DrawingContext dc)
    {
        if (!_isSlicing || _trail.Count < 2) return;
        var pts = _trail.ToArray();
        for (var i = 1; i < pts.Length; i++)
        {
            var opacity = (80 + (i * 170 / pts.Length)) / 255.0;
            dc.PushOpacity(opacity);
            dc.DrawLine(TrailPens[i - 1], pts[i - 1], pts[i]);
            dc.Pop();
        }
    }

    private void DrawShape(DrawingContext dc, NeonShape s)
    {
        var center = new Point(s.X, s.Y);
        var r = s.Radius;
        var col = s.NeonColor;

        dc.PushTransform(new RotateTransform(s.Rotation, s.X, s.Y));

        switch (s.Type)
        {
            case ShapeType.Circle:
                DrawGlowCircle(dc, center, r, col, false);
                break;
            case ShapeType.Star:
                DrawStarShape(dc, center, r, col);
                break;
            case ShapeType.Ice:
                DrawIceShape(dc, center, r, col);
                break;
            case ShapeType.Bomb:
                DrawBomb(dc, center, r);
                break;
            case ShapeType.Lightning:
                DrawLightningShape(dc, center, r, col);
                break;
            default:
                DrawPolygonShape(dc, center, r, col, s.Type);
                break;
        }

        dc.Pop();
    }

    private static void DrawGlowCircle(DrawingContext dc, Point c, double r, Color col, bool bomb)
    {
        dc.DrawEllipse(GetRadialGlow(col, 80), null, c, r * 1.8, r * 1.8);
        dc.DrawEllipse(GetCircleBody(col), GetBorderPen(col), c, r, r);
    }

    // ── 폭탄 캐시 (매 프레임 재생성 방지) ─────────────────────────────────
    private static readonly RadialGradientBrush BombGlowBrush =
        CreateFrozen(new RadialGradientBrush(
            Color.FromArgb(90, 255, 0, 0),
            Color.FromArgb(0, 255, 0, 0)));
    private static readonly RadialGradientBrush BombBodyBrush =
        CreateFrozen(new RadialGradientBrush(
            Color.FromArgb(220, 200, 20, 20),
            Color.FromArgb(180, 80, 0, 0)));
    private static readonly Pen BombBorderPen =
        CreateFrozen(new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 60, 60)), 2));
    private static readonly SolidColorBrush BombTextBrush =
        CreateFrozen(new SolidColorBrush(Colors.White));

    private static T CreateFrozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    private static void DrawBomb(DrawingContext dc, Point c, double r)
    {
        dc.DrawEllipse(BombGlowBrush, null, c, r * 2, r * 2);
        dc.DrawEllipse(BombBodyBrush, BombBorderPen, c, r, r);

        // "!" 텍스트
        var ft = new FormattedText("!", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), r * 1.1,
            BombTextBrush, 1.0);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    // ── Star: 노란색 원 + ★ 심볼 (도움말 범례와 일치) ──────────────────────
    private static void DrawStarShape(DrawingContext dc, Point c, double r, Color col)
    {
        dc.DrawEllipse(GetRadialGlow(col, 100), null, c, r * 1.9, r * 1.9);
        dc.DrawEllipse(GetCircleBody(col), GetBorderPen(col), c, r, r);

        var ft = new FormattedText("★", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Symbol"), r * 1.5,
            GetBorderPen(col).Brush, 1.0);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    // ── Ice: 아이스블루 원 + ❄ 심볼 ─────────────────────────────────────
    private static void DrawIceShape(DrawingContext dc, Point c, double r, Color col)
    {
        dc.DrawEllipse(GetRadialGlow(col, 100), null, c, r * 1.9, r * 1.9);
        dc.DrawEllipse(GetCircleBody(col), GetBorderPen(col), c, r, r);

        var ft = new FormattedText("❄", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Symbol"), r * 1.5,
            GetBorderPen(col).Brush, 1.0);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    private static void DrawLightningShape(DrawingContext dc, Point c, double r, Color col)
    {
        dc.DrawEllipse(GetRadialGlow(col, 90), null, c, r * 1.8, r * 1.8);
        var ft = new FormattedText("⚡", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI Emoji"), r * 1.3,
            GetLightningPen(col).Brush, 1.0);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    private static void DrawPolygonShape(DrawingContext dc, Point c, double r, Color col, ShapeType type)
    {
        var sides = type switch
        {
            ShapeType.Triangle => 3,
            ShapeType.Square   => 4,
            ShapeType.Pentagon => 5,
            _                  => 6
        };

        var geoGlow = BuildPolygon(c, r * 1.7, sides);
        var geo     = BuildPolygon(c, r, sides);

        dc.DrawGeometry(GetRadialGlow(col, 70), null, geoGlow);
        dc.DrawGeometry(GetPolyBody(col), GetBorderPen(col), geo);
    }

    private static PathGeometry BuildPolygon(Point center, double radius, int sides)
    {
        var geo = new PathGeometry();
        var fig = new PathFigure { IsClosed = true };
        for (var i = 0; i < sides; i++)
        {
            var angle = Math.PI * 2 * i / sides - Math.PI / 2;
            var p = new Point(center.X + Math.Cos(angle) * radius,
                              center.Y + Math.Sin(angle) * radius);
            if (i == 0) fig.StartPoint = p;
            else        fig.Segments.Add(new LineSegment(p, true));
        }
        geo.Figures.Add(fig);
        return geo;
    }

    private void DrawHalf(DrawingContext dc, SlicedHalf h)
    {
        if (h.Alpha < 0.02) return;

        dc.PushTransform(new RotateTransform(h.Rotation, h.X, h.Y));
        dc.PushOpacity(h.Alpha);

        var pen    = GetHalfPen(h.NeonColor);
        var fill   = GetHalfFill(h.NeonColor);
        var center = new Point(h.X, h.Y);

        // 클리핑: Half==0 → 우측 반, Half==1 → 좌측 반 (회전 좌표계 기준)
        var clipRect = h.Half == 0
            ? new Rect(h.X,              h.Y - h.Radius - 5, h.Radius + 5, h.Radius * 2 + 10)
            : new Rect(h.X - h.Radius - 5, h.Y - h.Radius - 5, h.Radius + 5, h.Radius * 2 + 10);
        dc.PushClip(new RectangleGeometry(clipRect));

        switch (h.Type)
        {
            case ShapeType.Circle:
            case ShapeType.Ice:
            case ShapeType.Star:
                dc.DrawEllipse(fill, pen, center, h.Radius, h.Radius);
                break;

            default: // Triangle, Square, Pentagon
                var sides = h.Type switch
                {
                    ShapeType.Triangle => 3,
                    ShapeType.Square   => 4,
                    ShapeType.Pentagon => 5,
                    _                  => 6,
                };
                dc.DrawGeometry(fill, pen, BuildPolygon(center, h.Radius, sides));
                break;
        }

        dc.Pop(); // clip
        dc.Pop(); // opacity
        dc.Pop(); // rotate
    }

    private static void DrawParticle(DrawingContext dc, Particle p)
    {
        if (p.Alpha < 0.02) return;
        dc.PushOpacity(p.Alpha);
        dc.DrawEllipse(GetParticleBrush(p.Color), null, new Point(p.X, p.Y), p.Radius, p.Radius);
        dc.Pop();
    }

    private void DrawSlowMoOverlay(DrawingContext dc)
    {
        var alpha = (byte)(30 + Math.Sin(_elapsedGame * 8) * 15);
        var col = _inFever
            ? Color.FromArgb(alpha, 255, 45, 120)
            : Color.FromArgb(alpha, 0,   200, 255);
        dc.DrawRectangle(new SolidColorBrush(col), null, new Rect(0, 0, _width, _height));
    }

    private void DrawBombFlash(DrawingContext dc)
    {
        // 폭탄 슬라이스 시 짧은 적색 플래시 (0.25초 동안 페이드 아웃)
        var ratio = _bombFlashRemaining / BombFlashDuration;
        var alpha = (byte)(ratio * 80);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, 220, 30, 30)),
            null, new Rect(0, 0, _width, _height));
    }

    private void DrawComboFlash(DrawingContext dc)
    {
        // 10콤보 이정표 달성 시 시안 플래시 (0.4초 동안 페이드 아웃)
        var ratio = _comboFlashRemaining / ComboFlashDuration;
        var alpha = (byte)(ratio * 70);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, 0, 255, 255)),
            null, new Rect(0, 0, _width, _height));
    }

    private static readonly RadialGradientBrush CursorGlowBrush =
        new(Color.FromArgb(60, 0, 255, 255), Color.FromArgb(0, 0, 255, 255));

    private void DrawCursor(DrawingContext dc)
    {
        var c = _lastMousePos;
        // 외곽 글로우
        dc.DrawEllipse(CursorGlowBrush, null, c, 14, 14);

        // 테두리 링
        dc.DrawEllipse(null, CursorRingPen, c, 9, 9);

        // 중앙 dot
        dc.DrawEllipse(CursorCenterBrush, null, c, 2.5, 2.5);
    }
}
