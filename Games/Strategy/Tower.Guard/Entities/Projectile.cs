namespace TowerGuard.Entities;

public sealed class Projectile
{
    public double X { get; set; }
    public double Y { get; set; }
    public Enemy Target { get; }
    public Tower Source { get; }
    public double Damage { get; }
    public bool IsAlive { get; set; } = true;
    public string ColorHex { get; }

    private const double Speed = 320.0;

    public Projectile(Tower source, Enemy target)
    {
        Source = source;
        Target = target;
        X = source.CenterX;
        Y = source.CenterY;
        Damage = source.Damage;
        ColorHex = source.ColorHex;
    }

    public void Update(double dt)
    {
        if (!IsAlive) return;

        if (!Target.IsAlive)
        {
            IsAlive = false;
            return;
        }

        double dx = Target.X - X;
        double dy = Target.Y - Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist < 6)
        {
            IsAlive = false;
            return;
        }

        double move = Speed * dt;
        if (move >= dist)
        {
            X = Target.X;
            Y = Target.Y;
            IsAlive = false;
        }
        else
        {
            X += dx / dist * move;
            Y += dy / dist * move;
        }
    }
}
