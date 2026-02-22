using TowerGuard.Engine;

namespace TowerGuard.Entities;

public enum EnemyType
{
    Grunt,
    Runner,
    Tank,
    Healer,
    Boss
}

public sealed class Enemy
{
    public EnemyType Type { get; }
    public double MaxHp { get; }
    public double Hp { get; set; }
    public double Speed { get; }
    public int Reward { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsAlive => Hp > 0;
    public double SlowTimer { get; set; }
    public double SlowFactor { get; set; } = 1.0;
    public string ColorHex { get; }

    private int _pathIndex;
    private readonly List<(int X, int Y)> _path;

    public bool ReachedEnd { get; private set; }

    public Enemy(EnemyType type, List<(int X, int Y)> path, double hpMultiplier)
    {
        Type = type;
        _path = path;
        _pathIndex = 0;

        (MaxHp, Speed, Reward, ColorHex) = type switch
        {
            EnemyType.Grunt   => (60 * hpMultiplier,  48.0, 10, "#E67E22"),
            EnemyType.Runner  => (30 * hpMultiplier,  80.0, 15, "#F1C40F"),
            EnemyType.Tank    => (200 * hpMultiplier,  28.0, 25, "#95A5A6"),
            EnemyType.Healer  => (50 * hpMultiplier,  44.0, 20, "#2ECC71"),
            EnemyType.Boss    => (500 * hpMultiplier,  24.0, 100, "#E74C3C"),
            _ => (60 * hpMultiplier, 48.0, 10, "#E67E22")
        };

        Hp = MaxHp;

        if (_path.Count > 0)
        {
            X = _path[0].X * GridMap.TileSize + GridMap.TileSize / 2;
            Y = _path[0].Y * GridMap.TileSize + GridMap.TileSize / 2;
        }
    }

    public void Update(double dt)
    {
        if (!IsAlive || ReachedEnd) return;

        // Slow effect
        if (SlowTimer > 0)
        {
            SlowTimer -= dt;
            if (SlowTimer <= 0) SlowFactor = 1.0;
        }

        double speed = Speed * SlowFactor;
        double moveAmount = speed * dt;

        while (moveAmount > 0 && _pathIndex < _path.Count)
        {
            double targetX = _path[_pathIndex].X * GridMap.TileSize + GridMap.TileSize / 2;
            double targetY = _path[_pathIndex].Y * GridMap.TileSize + GridMap.TileSize / 2;

            double dx = targetX - X;
            double dy = targetY - Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist <= moveAmount)
            {
                X = targetX;
                Y = targetY;
                moveAmount -= dist;
                _pathIndex++;
            }
            else
            {
                X += dx / dist * moveAmount;
                Y += dy / dist * moveAmount;
                moveAmount = 0;
            }
        }

        if (_pathIndex >= _path.Count)
        {
            ReachedEnd = true;
        }
    }

    public void TakeDamage(double damage)
    {
        Hp -= damage;
        if (Hp < 0) Hp = 0;
    }

    public void Heal(double amount)
    {
        Hp = Math.Min(MaxHp, Hp + amount);
    }

    public void ApplySlow(double factor, double duration)
    {
        SlowFactor = factor;
        SlowTimer = duration;
    }

    /// <summary>Progress along the path as a ratio 0..1</summary>
    public double PathProgress => _path.Count > 0 ? (double)_pathIndex / _path.Count : 0;
}
