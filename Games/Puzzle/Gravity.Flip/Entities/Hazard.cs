namespace GravityFlip.Entities;

public enum HazardType { Spike }

public sealed class Hazard
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public HazardType Type { get; }
    public bool PointsUp { get; }

    public Hazard(double x, double y, double width, double height, HazardType type, bool pointsUp)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Type = type;
        PointsUp = pointsUp;
    }
}
