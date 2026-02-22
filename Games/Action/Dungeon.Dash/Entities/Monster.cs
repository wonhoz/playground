using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DungeonDash.Engine;

namespace DungeonDash.Entities;

public enum MonsterKind { Slime, Skeleton, Ghost, Demon, Dragon }

public sealed class Monster
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; }
    public int MaxHp { get; }
    public int Hp { get; set; }
    public int Atk { get; }
    public double Speed { get; }
    public int ScoreValue { get; }
    public MonsterKind Kind { get; }
    public bool IsAlive => Hp > 0;
    public bool IsBoss { get; }

    private double _moveTimer;
    private double _attackCooldown;
    private double _dirX, _dirY;
    private double _hitFlashTimer;
    private readonly Random _rng;

    public Canvas Visual { get; }

    public Monster(MonsterKind kind, double x, double y, Random rng, bool isBoss = false)
    {
        _rng = rng;
        Kind = kind;
        X = x; Y = y;
        IsBoss = isBoss;

        double bossScale = isBoss ? 1.8 : 1.0;

        switch (kind)
        {
            case MonsterKind.Slime:
                Size = 14 * bossScale; MaxHp = (int)(15 * bossScale); Atk = 5; Speed = 40; ScoreValue = 50;
                break;
            case MonsterKind.Skeleton:
                Size = 16 * bossScale; MaxHp = (int)(25 * bossScale); Atk = 10; Speed = 60; ScoreValue = 100;
                break;
            case MonsterKind.Ghost:
                Size = 14 * bossScale; MaxHp = (int)(20 * bossScale); Atk = 8; Speed = 80; ScoreValue = 120;
                break;
            case MonsterKind.Demon:
                Size = 18 * bossScale; MaxHp = (int)(40 * bossScale); Atk = 15; Speed = 55; ScoreValue = 200;
                break;
            case MonsterKind.Dragon:
                Size = 24 * bossScale; MaxHp = (int)(120 * bossScale); Atk = 25; Speed = 45; ScoreValue = 500;
                break;
        }

        Hp = MaxHp;
        Visual = new Canvas { Width = Size, Height = Size };

        BuildVisual(kind, isBoss);

        _moveTimer = _rng.NextDouble() * 0.5;
    }

    private static Rectangle R(double w, double h, Color c, double rad = 2) =>
        new() { Width = w, Height = h, Fill = new SolidColorBrush(c), RadiusX = rad, RadiusY = rad };

    private void BuildVisual(MonsterKind kind, bool isBoss)
    {
        double s = Size;
        switch (kind)
        {
            case MonsterKind.Slime:
            {
                var body = new Ellipse
                {
                    Width = s, Height = s * 0.65,
                    Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                    StrokeThickness = 1
                };
                Canvas.SetTop(body, s * 0.35);
                // 눈
                var eye = new Ellipse { Width = s * 0.3, Height = s * 0.22,
                    Fill = new SolidColorBrush(Colors.White) };
                Canvas.SetLeft(eye, s * 0.35); Canvas.SetTop(eye, s * 0.18);
                Visual.Children.Add(body);
                Visual.Children.Add(eye);
                break;
            }
            case MonsterKind.Skeleton:
            {
                var armL = R(s * 0.14, s * 0.35, Color.FromRgb(0xCC, 0xCC, 0xBB), 1);
                var armR = R(s * 0.14, s * 0.35, Color.FromRgb(0xCC, 0xCC, 0xBB), 1);
                Canvas.SetLeft(armL, s * 0.1); Canvas.SetTop(armL, s * 0.35);
                Canvas.SetLeft(armR, s * 0.76); Canvas.SetTop(armR, s * 0.35);
                var body = R(s * 0.55, s * 0.45, Color.FromRgb(0xBD, 0xC3, 0xC7), 2);
                Canvas.SetLeft(body, s * 0.22); Canvas.SetTop(body, s * 0.33);
                var head = R(s * 0.44, s * 0.31, Color.FromRgb(0xD5, 0xDA, 0xDC), 3);
                Canvas.SetLeft(head, s * 0.28); Canvas.SetTop(head, s * 0.0);
                Visual.Children.Add(armL); Visual.Children.Add(armR);
                Visual.Children.Add(body); Visual.Children.Add(head);
                break;
            }
            case MonsterKind.Ghost:
            {
                var body = new Rectangle
                {
                    Width = s, Height = s,
                    Fill = new SolidColorBrush(Color.FromArgb(210, 0x99, 0x77, 0xDD)),
                    RadiusX = s / 2, RadiusY = s / 2
                };
                // 눈 두 개
                var eyeL = new Ellipse { Width = s * 0.18, Height = s * 0.18,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)) };
                var eyeR = new Ellipse { Width = s * 0.18, Height = s * 0.18,
                    Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)) };
                Canvas.SetLeft(eyeL, s * 0.27); Canvas.SetTop(eyeL, s * 0.35);
                Canvas.SetLeft(eyeR, s * 0.54); Canvas.SetTop(eyeR, s * 0.35);
                Visual.Children.Add(body);
                Visual.Children.Add(eyeL); Visual.Children.Add(eyeR);
                break;
            }
            case MonsterKind.Demon:
            {
                var armL = R(s * 0.17, s * 0.38, Color.FromRgb(0xC0, 0x39, 0x2B), 1);
                var armR = R(s * 0.17, s * 0.38, Color.FromRgb(0xC0, 0x39, 0x2B), 1);
                Canvas.SetLeft(armL, 0); Canvas.SetTop(armL, s * 0.30);
                Canvas.SetLeft(armR, s * 0.83); Canvas.SetTop(armR, s * 0.30);
                var body = R(s * 0.66, s * 0.45, Color.FromRgb(0xE7, 0x4C, 0x3C), 2);
                Canvas.SetLeft(body, s * 0.17); Canvas.SetTop(body, s * 0.30);
                var head = R(s * 0.44, s * 0.28, Color.FromRgb(0xC0, 0x39, 0x2B), 2);
                Canvas.SetLeft(head, s * 0.28); Canvas.SetTop(head, s * 0.0);
                // 뿔 (작은 삼각형 모양 사각형)
                var hornL = R(s * 0.11, s * 0.20, Color.FromRgb(0x96, 0x28, 0x1E), 1);
                var hornR = R(s * 0.11, s * 0.20, Color.FromRgb(0x96, 0x28, 0x1E), 1);
                Canvas.SetLeft(hornL, s * 0.22); Canvas.SetTop(hornL, -s * 0.10);
                Canvas.SetLeft(hornR, s * 0.67); Canvas.SetTop(hornR, -s * 0.10);
                Visual.Children.Add(armL); Visual.Children.Add(armR);
                Visual.Children.Add(body); Visual.Children.Add(head);
                Visual.Children.Add(hornL); Visual.Children.Add(hornR);
                break;
            }
            case MonsterKind.Dragon:
            {
                // 날개 (넓은 옆 사각형)
                var wingL = R(s * 0.35, s * 0.4, Color.FromRgb(0xCC, 0x55, 0x00), 1);
                var wingR = R(s * 0.35, s * 0.4, Color.FromRgb(0xCC, 0x55, 0x00), 1);
                Canvas.SetLeft(wingL, -s * 0.12); Canvas.SetTop(wingL, s * 0.3);
                Canvas.SetLeft(wingR, s * 0.77); Canvas.SetTop(wingR, s * 0.3);
                var body = R(s * 0.7, s * 0.5, Color.FromRgb(0xFF, 0x66, 0x00), 3);
                Canvas.SetLeft(body, s * 0.15); Canvas.SetTop(body, s * 0.28);
                var head = R(s * 0.5, s * 0.28, Color.FromRgb(0xFF, 0x88, 0x22), 3);
                Canvas.SetLeft(head, s * 0.25); Canvas.SetTop(head, s * 0.0);
                Visual.Children.Add(wingL); Visual.Children.Add(wingR);
                Visual.Children.Add(body); Visual.Children.Add(head);
                break;
            }
        }

        // 보스 왕관
        if (isBoss)
        {
            var crown = new TextBlock
            {
                Text = "♛", FontSize = s * 0.4,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            };
            Canvas.SetLeft(crown, s / 2 - s * 0.15);
            Canvas.SetTop(crown, -s * 0.35);
            Visual.Children.Add(crown);
        }
    }

    public void Update(double dt, Player player, Tile[,] map)
    {
        if (!IsAlive) return;

        _attackCooldown -= dt;
        _hitFlashTimer -= dt;

        Visual.Opacity = _hitFlashTimer > 0
            ? 0.5 + 0.5 * Math.Sin(_hitFlashTimer * 30)
            : 1.0;

        // 간단 AI: 플레이어 방향으로 이동
        double distX = player.X - X;
        double distY = player.Y - Y;
        double dist = Math.Sqrt(distX * distX + distY * distY);

        if (dist < 200 && dist > 2)
        {
            _dirX = distX / dist;
            _dirY = distY / dist;

            // 고스트는 벽 통과
            if (Kind == MonsterKind.Ghost)
            {
                X += _dirX * Speed * dt;
                Y += _dirY * Speed * dt;
            }
            else
            {
                TryMove(_dirX * Speed * dt, _dirY * Speed * dt, map);
            }
        }
        else
        {
            // 랜덤 배회
            _moveTimer -= dt;
            if (_moveTimer <= 0)
            {
                _moveTimer = 1 + _rng.NextDouble();
                double angle = _rng.NextDouble() * Math.PI * 2;
                _dirX = Math.Cos(angle);
                _dirY = Math.Sin(angle);
            }
            TryMove(_dirX * Speed * 0.5 * dt, _dirY * Speed * 0.5 * dt, map);
        }
    }

    private void TryMove(double dx, double dy, Tile[,] map)
    {
        double newX = X + dx;
        double newY = Y + dy;
        if (!IsBlocked(newX, Y, map)) X = newX;
        if (!IsBlocked(X, newY, map)) Y = newY;
    }

    private bool IsBlocked(double px, double py, Tile[,] map)
    {
        const double tileSize = 20;
        int mapW = map.GetLength(0), mapH = map.GetLength(1);
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

    public bool CanAttack(Player player)
    {
        if (_attackCooldown > 0) return false;
        double dist = Math.Sqrt(Math.Pow(player.X - X, 2) + Math.Pow(player.Y - Y, 2));
        if (dist < Size + Player.Size)
        {
            _attackCooldown = IsBoss ? 0.8 : 1.2;
            return true;
        }
        return false;
    }

    public void TakeDamage(int damage)
    {
        Hp = Math.Max(0, Hp - damage);
        _hitFlashTimer = 0.3;
    }

    public void SyncPosition(double camX, double camY)
    {
        Canvas.SetLeft(Visual, X - camX);
        Canvas.SetTop(Visual, Y - camY);
    }

    public Rect Bounds => new(X, Y, Size, Size);
}
