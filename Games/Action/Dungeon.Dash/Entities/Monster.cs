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
    private readonly Shape _bodyShape;

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

        var color = kind switch
        {
            MonsterKind.Slime => Color.FromRgb(0x2E, 0xCC, 0x71),
            MonsterKind.Skeleton => Color.FromRgb(0xBD, 0xC3, 0xC7),
            MonsterKind.Ghost => Color.FromRgb(0x99, 0x77, 0xDD),
            MonsterKind.Demon => Color.FromRgb(0xE7, 0x4C, 0x3C),
            MonsterKind.Dragon => Color.FromRgb(0xFF, 0x66, 0x00),
            _ => Colors.White
        };

        if (kind == MonsterKind.Slime)
        {
            _bodyShape = new Ellipse
            {
                Width = Size, Height = Size * 0.7,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromRgb((byte)(color.R + 40), color.G, color.B)),
                StrokeThickness = 1
            };
            Canvas.SetTop(_bodyShape, Size * 0.3);
        }
        else
        {
            _bodyShape = new Rectangle
            {
                Width = Size, Height = Size,
                Fill = new SolidColorBrush(color),
                RadiusX = kind == MonsterKind.Ghost ? Size / 2 : 3,
                RadiusY = kind == MonsterKind.Ghost ? Size / 2 : 3,
                Stroke = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, color.R + 50),
                    (byte)Math.Min(255, color.G + 50),
                    (byte)Math.Min(255, color.B + 50))),
                StrokeThickness = isBoss ? 2 : 1
            };
        }
        Visual.Children.Add(_bodyShape);

        // 보스 표시
        if (isBoss)
        {
            var crown = new TextBlock
            {
                Text = "♛",
                FontSize = Size * 0.4,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            };
            Canvas.SetLeft(crown, Size / 2 - Size * 0.15);
            Canvas.SetTop(crown, -Size * 0.3);
            Visual.Children.Add(crown);
        }

        _moveTimer = _rng.NextDouble() * 0.5;
    }

    public void Update(double dt, Player player, Tile[,] map)
    {
        if (!IsAlive) return;

        _attackCooldown -= dt;
        _hitFlashTimer -= dt;

        if (_hitFlashTimer > 0)
            _bodyShape.Opacity = 0.5 + 0.5 * Math.Sin(_hitFlashTimer * 30);
        else
            _bodyShape.Opacity = 1.0;

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
