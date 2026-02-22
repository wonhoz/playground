namespace GravityFlip.Entities;

public sealed class Coin
{
    public double X { get; }
    public double Y { get; }
    public bool Collected { get; set; }

    public const double Size = 12;

    public Coin(double x, double y)
    {
        X = x;
        Y = y;
    }
}
