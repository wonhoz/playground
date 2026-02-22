using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DodgeBlitz.Engine;

namespace DodgeBlitz.Entities;

public sealed class Bullet : GameObject
{
    private readonly double _vx;
    private readonly double _vy;
    private readonly double _areaW;
    private readonly double _areaH;

    public Bullet(double cx, double cy, double vx, double vy, double areaW, double areaH, Color color)
    {
        _vx    = vx;
        _vy    = vy;
        _areaW = areaW;
        _areaH = areaH;
        Width  = 10;
        Height = 10;
        X = cx - Width  / 2;
        Y = cy - Height / 2;

        Visual = new Ellipse
        {
            Width  = Width,
            Height = Height,
            Fill   = new SolidColorBrush(color),
            Effect = new DropShadowEffect
            {
                Color       = color,
                BlurRadius  = 10,
                ShadowDepth = 0,
                Opacity     = 0.85
            }
        };
    }

    public override void Update(double dt)
    {
        X += _vx * dt;
        Y += _vy * dt;

        if (X < -60 || X > _areaW + 60 || Y < -60 || Y > _areaH + 60)
            IsAlive = false;
    }
}
