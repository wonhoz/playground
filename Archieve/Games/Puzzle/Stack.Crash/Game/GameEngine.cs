using StackCrash.Models;
using StackCrash.Levels;

namespace StackCrash.Game;

public enum WinState { Playing, Won, Lost }

public class GameEngine
{
    // ── 물리 상수 ────────────────────────────────────────────────────
    private const double PPM            = 60.0;
    private const float  GRAVITY        = -9.8f;
    private const int    VEL_ITERATIONS = 8;
    private const int    POS_ITERATIONS = 3;
    private const float  SETTLE_VEL     = 0.08f;   // 정착 판정 속도 임계값
    private const float  COLLAPSE_Y     = 0.3f;    // 붕괴 판정 Y (미터)
    private const float  WIN_RATIO      = 0.75f;   // 승리 조건: 75% 이상 붕괴

    // ── 폭발 상수 ───────────────────────────────────────────────────
    private const float EXPLOSION_RADIUS = 2.5f;
    private const float EXPLOSION_FORCE  = 60000f;

    // ── 내부 상태 ────────────────────────────────────────────────────
    private World           _world   = null!;
    private List<GameBlock> _blocks  = [];
    private LevelDef?       _level;
    private int             _totalBlocks;

    // ── 이벤트 ──────────────────────────────────────────────────────
    public event Action<GameBlock>? BlockDestroyed;
    public event Action<GameBlock>? BlockExploded;

    // ── 공개 속성 ────────────────────────────────────────────────────
    public IReadOnlyList<GameBlock> Blocks    => _blocks;
    public bool                     IsRunning { get; private set; }

    // ── 레벨 로드 ────────────────────────────────────────────────────
    public void LoadLevel(LevelDef level)
    {
        _world  = new World(new AetherVector2(0f, GRAVITY));
        _blocks = [];
        _level  = level;
        IsRunning = false;

        // 지면 (Y=0 이 상면)
        var ground = _world.CreateBody(new AetherVector2(0f, -0.25f), 0f, BodyType.Static);
        ground.CreateRectangle(200f, 0.5f, 1f, AetherVector2.Zero);

        // 측벽
        var wallL = _world.CreateBody(new AetherVector2(-12f, 5f), 0f, BodyType.Static);
        wallL.CreateRectangle(0.5f, 20f, 1f, AetherVector2.Zero);
        var wallR = _world.CreateBody(new AetherVector2( 12f, 5f), 0f, BodyType.Static);
        wallR.CreateRectangle(0.5f, 20f, 1f, AetherVector2.Zero);

        _totalBlocks = level.Blocks.Count;
    }

    // ── 블록 생성 ────────────────────────────────────────────────────
    public GameBlock CreateBlock(BlockDef def, Canvas canvas,
                                 double groundScreenY, double canvasCenterX)
    {
        var mat = Materials.Get(def.Material);

        var body = _world.CreateBody(
            new AetherVector2(def.X, def.Y),
            def.Angle * (float)(Math.PI / 180.0),
            BodyType.Dynamic);

        var fixture = body.CreateRectangle(def.W, def.H, mat.Density, AetherVector2.Zero);
        fixture.Friction    = mat.Friction;
        fixture.Restitution = mat.Restitution;
        body.LinearDamping  = 0.05f;
        body.AngularDamping = 0.05f;

        var visual = BuildVisual(def.W, def.H, mat, def.Material);

        var block = new GameBlock
        {
            Body     = body,
            PhysW    = def.W,
            PhysH    = def.H,
            Material = def.Material,
            Hp       = mat.MaxHp,
            Visual   = visual,
        };
        visual.Tag = block;

        canvas.Children.Add(visual);
        block.SyncVisual(groundScreenY, canvasCenterX);

        _blocks.Add(block);
        return block;
    }

    // ── 시뮬레이션 제어 ─────────────────────────────────────────────
    public void StartSimulation() => IsRunning = true;
    public void StopSimulation()  => IsRunning = false;

    public void Step(float dt)
    {
        if (!IsRunning) return;
        _world.Step(dt);
    }

    // ── 블록 제거 ────────────────────────────────────────────────────
    public void RemoveBlock(GameBlock block, Canvas canvas)
    {
        if (block.IsRemoved) return;
        block.IsRemoved = true;
        _world.Remove(block.Body);
        canvas.Children.Remove(block.Visual);
        BlockDestroyed?.Invoke(block);

        if (block.Material == BlockMaterial.Explosive)
            TriggerExplosion(block.Body.Position, canvas, block);
    }

    // ── 폭발 ─────────────────────────────────────────────────────────
    private void TriggerExplosion(AetherVector2 center, Canvas canvas, GameBlock? source)
    {
        BlockExploded?.Invoke(source!);

        foreach (var b in _blocks.ToList())
        {
            if (b.IsRemoved) continue;
            var diff = b.Body.Position - center;
            float dist = diff.Length();
            if (dist > EXPLOSION_RADIUS) continue;

            float forceMag = EXPLOSION_FORCE * (1f - dist / EXPLOSION_RADIUS);
            AetherVector2 impulse = dist > 0.01f
                ? diff * (forceMag / dist)
                : new AetherVector2(0f, forceMag);
            b.Body.ApplyLinearImpulse(impulse);

            // 폭발 충격 데미지
            if (b.TakeDamage(3) && !b.IsExploding)
            {
                if (b.Material == BlockMaterial.Explosive)
                {
                    b.IsExploding = true;
                    TriggerExplosion(b.Body.Position, canvas, b);
                }
                RemoveBlock(b, canvas);
            }
        }
    }

    // ── 정착 판정 ────────────────────────────────────────────────────
    public bool IsSettled()
    {
        foreach (var b in _blocks)
        {
            if (b.IsRemoved) continue;
            if (b.Body.LinearVelocity.Length()   > SETTLE_VEL) return false;
            if (Math.Abs(b.Body.AngularVelocity) > SETTLE_VEL) return false;
        }
        return true;
    }

    // ── HP 0 블록 제거 ───────────────────────────────────────────────
    public void PurgeDeadBlocks(Canvas canvas)
    {
        foreach (var b in _blocks.ToList())
            if (!b.IsRemoved && b.Hp <= 0)
                RemoveBlock(b, canvas);
    }

    // ── 승리 판정 ────────────────────────────────────────────────────
    public WinState CheckWin(int movesUsed)
    {
        if (_level is null) return WinState.Playing;

        int collapsed = _blocks.Count(b =>
            b.IsRemoved || b.Body.Position.Y < COLLAPSE_Y);
        float ratio = _totalBlocks > 0 ? (float)collapsed / _totalBlocks : 0f;

        if (ratio >= WIN_RATIO) return WinState.Won;

        bool noMoves = _level.MaxMoves > 0 && movesUsed >= _level.MaxMoves;
        int  standing = _blocks.Count(b => !b.IsRemoved);
        if (noMoves && standing > 0 && ratio < WIN_RATIO) return WinState.Lost;

        return WinState.Playing;
    }

    public int CalcStars(int movesUsed)
    {
        if (_level is null) return 0;
        if (movesUsed <= _level.Star3Moves) return 3;
        if (movesUsed <= _level.Star2Moves) return 2;
        return 1;
    }

    // ── WPF 비주얼 빌드 ─────────────────────────────────────────────
    private static Grid BuildVisual(float physW, float physH,
                                    Materials.Def mat, BlockMaterial material)
    {
        double w = physW * PPM;
        double h = physH * PPM;

        var fill   = ParseBrush(mat.Fill);
        var stroke = ParseBrush(mat.Stroke);
        var dark   = ParseBrush(mat.FillDark);

        UIElement? inner = material switch
        {
            BlockMaterial.Wood      => MakeWoodGrain(w, h, dark),
            BlockMaterial.Metal     => MakeMetalLabel(),
            BlockMaterial.Explosive => MakeExplosiveLabel(),
            _                       => null,
        };

        var main = new Border
        {
            Width           = w,
            Height          = h,
            Background      = fill,
            BorderBrush     = stroke,
            BorderThickness = new Thickness(1.5),
            CornerRadius    = new CornerRadius(2),
            Child           = inner,
        };

        var crack = new Border
        {
            Width            = w,
            Height           = h,
            Opacity          = 0,
            Background       = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            CornerRadius     = new CornerRadius(2),
            IsHitTestVisible = false,
        };

        var grid = new Grid
        {
            Width  = w,
            Height = h,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform       = new RotateTransform(0),
        };
        grid.Children.Add(main);
        grid.Children.Add(crack);
        return grid;
    }

    private static SolidColorBrush ParseBrush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;

    private static UIElement MakeWoodGrain(double w, double h, SolidColorBrush dark)
    {
        var canvas = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
        int lines = Math.Max(2, (int)(h / 8));
        for (int i = 1; i < lines; i++)
        {
            double y = i * (h / lines);
            var ln = new System.Windows.Shapes.Line
            {
                X1 = 3, Y1 = y, X2 = w - 3, Y2 = y,
                Stroke = dark, StrokeThickness = 1, Opacity = 0.5,
            };
            canvas.Children.Add(ln);
        }
        return canvas;
    }

    private static UIElement MakeMetalLabel() => new TextBlock
    {
        Text = "▬",
        Foreground = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment   = VerticalAlignment.Center,
        IsHitTestVisible    = false,
    };

    private static UIElement MakeExplosiveLabel() => new TextBlock
    {
        Text = "💥",
        FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment   = VerticalAlignment.Center,
        IsHitTestVisible    = false,
    };
}
