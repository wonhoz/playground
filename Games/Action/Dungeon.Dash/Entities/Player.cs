using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DungeonDash.Engine;

namespace DungeonDash.Entities;

public sealed class Player
{
    public double X { get; set; }
    public double Y { get; set; }
    public const double Size = 18;
    public const double Speed = 140;
    public double DashSpeed = 450;

    public int MaxHp { get; set; } = 100;
    public int Hp { get; set; } = 100;
    public int Atk { get; set; } = 15;
    public int Score { get; set; }
    public int Floor { get; set; } = 1;
    public bool IsAlive => Hp > 0;

    // 대시
    public bool IsDashing { get; private set; }
    private double _dashTimer;
    private double _dashCooldown;
    private double _dashDirX, _dashDirY;
    private const double DashDuration = 0.15;
    private const double DashCooldownTime = 0.6;

    // 공격
    public bool IsAttacking { get; private set; }
    private double _attackTimer;
    private double _attackCooldown;
    public Rect? AttackHitbox { get; private set; }
    public double FacingX { get; private set; } = 1;
    public double FacingY { get; private set; }

    // 무적
    public double InvincibleTimer { get; set; }

    // 스킬 (범위 공격)
    public bool IsSkilling { get; private set; }
    private double _skillTimer;
    private double _skillCooldown;
    private const double SkillCooldownTime = 3.0;
    public bool SkillReady => _skillCooldown <= 0;

    // 비주얼
    public Canvas Visual { get; }

    private readonly KeyInput _input;
    private readonly Rectangle _head;
    private readonly Rectangle _body;
    private readonly Rectangle _armL, _armR;
    private readonly Rectangle _weapon;

    public Player(KeyInput input, double startX, double startY)
    {
        _input = input;
        X = startX;
        Y = startY;

        Visual = new Canvas { Width = Size, Height = Size };

        // 팔 (몸통 양 옆)
        _armL = Rect(3, 7, Color.FromRgb(0x3A, 0x86, 0xFF), 1);
        _armR = Rect(3, 7, Color.FromRgb(0x3A, 0x86, 0xFF), 1);
        Canvas.SetLeft(_armL, 0);  Canvas.SetTop(_armL, 5);
        Canvas.SetLeft(_armR, 15); Canvas.SetTop(_armR, 5);

        // 몸통
        _body = Rect(12, 8, Color.FromRgb(0x3A, 0x86, 0xFF), 2);
        Canvas.SetLeft(_body, 3); Canvas.SetTop(_body, 5);

        // 머리 (밝은 파란색)
        _head = Rect(8, 5, Color.FromRgb(0x60, 0xA8, 0xFF), 2);
        Canvas.SetLeft(_head, 5); Canvas.SetTop(_head, 0);

        // 무기 (공격 시 회전 표시)
        _weapon = new Rectangle
        {
            Width = 3, Height = 11,
            Fill = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xEE)),
            RadiusX = 1, RadiusY = 1,
            Visibility = Visibility.Collapsed,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
        };

        Visual.Children.Add(_armL);
        Visual.Children.Add(_armR);
        Visual.Children.Add(_body);
        Visual.Children.Add(_head);
        Visual.Children.Add(_weapon);
    }

    public void Update(double dt, Tile[,] map)
    {
        if (!IsAlive) return;

        // 타이머
        if (_dashCooldown > 0) _dashCooldown -= dt;
        if (_attackCooldown > 0) _attackCooldown -= dt;
        if (_skillCooldown > 0) _skillCooldown -= dt;
        if (InvincibleTimer > 0) InvincibleTimer -= dt;

        // 대시 중
        if (IsDashing)
        {
            _dashTimer -= dt;
            if (_dashTimer <= 0) IsDashing = false;
            else
            {
                TryMove(_dashDirX * DashSpeed * dt, _dashDirY * DashSpeed * dt, map);
                return;
            }
        }

        // 스킬 중
        if (IsSkilling)
        {
            _skillTimer -= dt;
            if (_skillTimer <= 0) IsSkilling = false;
        }

        // 공격 중
        if (IsAttacking)
        {
            _attackTimer -= dt;
            if (_attackTimer <= 0)
            {
                IsAttacking = false;
                AttackHitbox = null;
                _weapon.Visibility = Visibility.Collapsed;
            }
            return; // 공격 중 이동 불가
        }

        // 이동
        double dx = 0, dy = 0;
        if (_input.Left) dx -= 1;
        if (_input.Right) dx += 1;
        if (_input.Up) dy -= 1;
        if (_input.Down) dy += 1;

        if (dx != 0 || dy != 0)
        {
            double len = Math.Sqrt(dx * dx + dy * dy);
            dx /= len; dy /= len;
            FacingX = dx; FacingY = dy;
            TryMove(dx * Speed * dt, dy * Speed * dt, map);
        }

        // 대시
        if (_input.Dash && _dashCooldown <= 0 && (dx != 0 || dy != 0))
        {
            IsDashing = true;
            _dashTimer = DashDuration;
            _dashCooldown = DashCooldownTime;
            _dashDirX = dx == 0 ? FacingX : dx;
            _dashDirY = dy == 0 ? FacingY : dy;
        }

        // 공격
        if (_input.Attack && _attackCooldown <= 0)
        {
            IsAttacking = true;
            _attackTimer = 0.2;
            _attackCooldown = 0.3;

            double hx = X + Size / 2 + FacingX * 14;
            double hy = Y + Size / 2 + FacingY * 14;
            AttackHitbox = new Rect(hx - 12, hy - 12, 24, 24);

            // 무기를 공격 방향으로 회전해서 표시
            double wAngle = Math.Atan2(FacingY, FacingX) * 180 / Math.PI + 90;
            _weapon.RenderTransform = new RotateTransform(wAngle);
            Canvas.SetLeft(_weapon, Size / 2 - 1.5 + FacingX * 3);
            Canvas.SetTop(_weapon, Size / 2 - 5.5 + FacingY * 3);
            _weapon.Visibility = Visibility.Visible;
        }

        // 스킬 (범위 공격)
        if (_input.Skill && _skillCooldown <= 0)
        {
            IsSkilling = true;
            _skillTimer = 0.3;
            _skillCooldown = SkillCooldownTime;
        }

        UpdateVisual();
    }

    private void TryMove(double dx, double dy, Tile[,] map)
    {
        double newX = X + dx;
        double newY = Y + dy;

        // X 이동
        if (!IsBlocked(newX, Y, map)) X = newX;
        // Y 이동
        if (!IsBlocked(X, newY, map)) Y = newY;
    }

    private static bool IsBlocked(double px, double py, Tile[,] map)
    {
        const double tileSize = 20;
        int mapW = map.GetLength(0), mapH = map.GetLength(1);

        // 4 모서리 체크
        int[] xs = [(int)(px / tileSize), (int)((px + Size - 1) / tileSize)];
        int[] ys = [(int)(py / tileSize), (int)((py + Size - 1) / tileSize)];

        foreach (int tx in xs)
            foreach (int ty in ys)
            {
                if (tx < 0 || tx >= mapW || ty < 0 || ty >= mapH) return true;
                if (map[tx, ty] == Tile.Wall) return true;
            }
        return false;
    }

    public void TakeDamage(int damage)
    {
        if (InvincibleTimer > 0 || IsDashing) return;
        Hp = Math.Max(0, Hp - damage);
        InvincibleTimer = 0.8;
    }

    public void Heal(int amount) => Hp = Math.Min(MaxHp, Hp + amount);

    private static Rectangle Rect(double w, double h, Color fill, double r) =>
        new() { Width = w, Height = h, Fill = new SolidColorBrush(fill), RadiusX = r, RadiusY = r };

    private void UpdateVisual()
    {
        Visual.Opacity = InvincibleTimer > 0 ? 0.5 + 0.5 * Math.Sin(InvincibleTimer * 20) : 1.0;

        Color main, head;
        if (IsDashing)
        {
            main = Color.FromRgb(0xAA, 0xDD, 0xFF);
            head = Color.FromRgb(0xCC, 0xEE, 0xFF);
        }
        else if (IsSkilling)
        {
            main = Color.FromRgb(0xFF, 0xD7, 0x00);
            head = Color.FromRgb(0xFF, 0xE8, 0x55);
        }
        else
        {
            main = Color.FromRgb(0x3A, 0x86, 0xFF);
            head = Color.FromRgb(0x60, 0xA8, 0xFF);
        }

        var mainBrush = new SolidColorBrush(main);
        _body.Fill  = mainBrush;
        _armL.Fill  = mainBrush;
        _armR.Fill  = mainBrush;
        _head.Fill  = new SolidColorBrush(head);
    }

    public void SyncPosition(double camX, double camY)
    {
        Canvas.SetLeft(Visual, X - camX);
        Canvas.SetTop(Visual, Y - camY);
    }

    public Rect Bounds => new(X, Y, Size, Size);

    // 타일 위치
    public int TileX => (int)(X + Size / 2) / 20;
    public int TileY => (int)(Y + Size / 2) / 20;
}
