using System.Windows.Media;
using System.Windows.Shapes;
using StarStrike.Engine;

namespace StarStrike.Entities;

/// <summary>
/// 폭발 파티클 효과.
/// </summary>
public sealed class Particle : GameObject
{
    private readonly double _vx;
    private readonly double _vy;
    private double _life;
    private readonly double _maxLife;

    public Particle(double x, double y, double vx, double vy, double life, Color color)
    {
        X = x; Y = y;
        _vx = vx; _vy = vy;
        _life = life; _maxLife = life;
        Width = 3; Height = 3;

        Visual = new Ellipse
        {
            Width = 3, Height = 3,
            Fill = new SolidColorBrush(color)
        };
    }

    public override void Update(double dt)
    {
        X += _vx * dt;
        Y += _vy * dt;
        _life -= dt;

        if (_life <= 0)
        {
            IsAlive = false;
            return;
        }

        if (Visual is not null)
            Visual.Opacity = Math.Max(0, _life / _maxLife);
    }
}
