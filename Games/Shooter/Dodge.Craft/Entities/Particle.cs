namespace DodgeCraft.Entities;

/// <summary>파티클 이펙트</summary>
public class Particle
{
    public double X  { get; set; }
    public double Y  { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Life    { get; set; } = 0.5;
    public double MaxLife { get; set; } = 0.5;
    public double Size    { get; set; } = 4;
    public Color  Color   { get; set; } = Colors.White;
    public bool   IsAlive => Life > 0;
    public double Alpha   => Math.Max(0, Life / MaxLife);

    public void Update(double dt)
    {
        X    += VX * dt;
        Y    += VY * dt;
        VX   *= (1 - 3 * dt);
        VY   *= (1 - 3 * dt);
        Life -= dt;
    }

    private static readonly Random _rng = new();

    public static List<Particle> Burst(double x, double y, Color color, int count = 8)
    {
        var list = new List<Particle>();
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 40 + _rng.NextDouble() * 80;
            list.Add(new Particle
            {
                X = x, Y = y,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed,
                Life = 0.3 + _rng.NextDouble() * 0.3,
                MaxLife = 0.6,
                Size = 2 + _rng.NextDouble() * 4,
                Color = color,
            });
        }
        return list;
    }
}
