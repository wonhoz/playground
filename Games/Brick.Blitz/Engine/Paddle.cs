namespace BrickBlitz.Engine;

public sealed class Paddle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 80;
    public double Height { get; set; } = 14;
    public double Speed { get; set; } = 500;
    public double DefaultWidth { get; } = 80;

    public Paddle(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}
