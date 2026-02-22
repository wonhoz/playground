using System.Windows.Media;
using System.Windows.Shapes;
using DodgeBlitz.Engine;

namespace DodgeBlitz.Entities;

public sealed class Star : GameObject
{
    private readonly double _speed;
    private readonly double _areaH;

    public Star(double areaW, double areaH, Random rng, int layer)
    {
        _areaH = areaH;

        double[] speeds     = [18, 42, 72];
        double[] sizes      = [1.0, 1.5, 2.5];
        byte[]   brightness = [45, 88, 145];

        int l = Math.Clamp(layer, 0, 2);
        _speed = speeds[l];
        double sz = sizes[l];
        byte   b  = brightness[l];

        Width  = sz;
        Height = sz;
        X = rng.NextDouble() * areaW;
        Y = rng.NextDouble() * areaH;

        Visual = new Rectangle
        {
            Width  = sz,
            Height = sz,
            Fill   = new SolidColorBrush(Color.FromRgb(b, b, b))
        };
    }

    public override void Update(double dt)
    {
        Y += _speed * dt;
        if (Y > _areaH) Y = -2;
    }
}
