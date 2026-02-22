namespace BrickBlitz.Engine;

public sealed class Ball
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Radius { get; set; } = 6;
    public double Speed { get; set; } = 360;
    public bool Stuck { get; set; } = true;
    public bool Active { get; set; } = true;

    public Ball(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void Launch()
    {
        if (!Stuck) return;
        Stuck = false;
        VX = Speed * 0.5;
        VY = -Speed * 0.866;
    }

    public void Update(double dt)
    {
        if (Stuck || !Active) return;
        X += VX * dt;
        Y += VY * dt;
    }

    public void SetVelocity(double angle)
    {
        VX = Speed * Math.Sin(angle);
        VY = -Speed * Math.Cos(angle);
    }

    public void NormalizeSpeed()
    {
        double len = Math.Sqrt(VX * VX + VY * VY);
        if (len < 0.001) return;
        VX = VX / len * Speed;
        VY = VY / len * Speed;
    }
}
