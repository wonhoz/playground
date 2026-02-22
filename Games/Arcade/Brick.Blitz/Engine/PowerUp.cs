using System.Windows.Media;

namespace BrickBlitz.Engine;

public enum PowerUpType
{
    WidePaddle,
    MultiBall,
    LaserPaddle,
    SlowBall,
    ExtraLife
}

public sealed class PowerUp
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 30;
    public double Height { get; set; } = 14;
    public double FallSpeed { get; set; } = 120;
    public PowerUpType Type { get; set; }
    public bool Active { get; set; } = true;

    public string Label { get; }
    public Color Color { get; }

    public PowerUp(double x, double y, PowerUpType type)
    {
        X = x;
        Y = y;
        Type = type;

        (Label, Color) = type switch
        {
            PowerUpType.WidePaddle => ("W+", Color.FromRgb(0x00, 0xFF, 0xCC)),
            PowerUpType.MultiBall => ("x3", Color.FromRgb(0xFF, 0xD7, 0x00)),
            PowerUpType.LaserPaddle => ("L!", Color.FromRgb(0xFF, 0x44, 0x44)),
            PowerUpType.SlowBall => ("SL", Color.FromRgb(0x3A, 0x86, 0xFF)),
            PowerUpType.ExtraLife => ("+1", Color.FromRgb(0xFF, 0x66, 0xAA)),
            _ => ("??", Colors.White)
        };
    }

    public void Update(double dt)
    {
        if (!Active) return;
        Y += FallSpeed * dt;
    }
}
