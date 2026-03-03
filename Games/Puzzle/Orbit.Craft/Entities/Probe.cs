namespace OrbitCraft.Entities;

/// <summary>탐사선 — 위치·속도·궤적 트레일.</summary>
public class Probe
{
    public double X, Y;
    public double Vx, Vy;
    public double Speed => Math.Sqrt(Vx*Vx + Vy*Vy);

    public readonly List<(double X, double Y)> Trail = [];

    public void Reset(double x, double y, double vx, double vy)
    {
        X = x; Y = y; Vx = vx; Vy = vy;
        Trail.Clear();
    }
}
