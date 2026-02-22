using System.Windows.Media;
using System.Windows.Shapes;
using DodgeBlitz.Engine;

namespace DodgeBlitz.Entities;

public sealed class Particle : GameObject
{
    private readonly double _vx;
    private readonly double _vy;
    private double _life;
    private readonly double _maxLife;

    public Particle(double cx, double cy, double vx, double vy, double life, Color color)
    {
        _vx = vx;
        _vy = vy;
        _life = _maxLife = life;

        double sz = 2 + Random.Shared.NextDouble() * 4;
        Width = sz;
        Height = sz;
        X = cx - sz / 2;
        Y = cy - sz / 2;

        Visual = new Ellipse
        {
            Width = sz,
            Height = sz,
            Fill = new SolidColorBrush(color)
        };
    }

    public override void Update(double dt)
    {
        X += _vx * dt;
        Y += _vy * dt;
        _life -= dt;
        if (_life <= 0) { IsAlive = false; return; }
        if (Visual is not null) Visual.Opacity = _life / _maxLife;
    }
}
