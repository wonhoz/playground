using System.Windows.Media;
using System.Windows.Shapes;
using StarStrike.Engine;

namespace StarStrike.Entities;

/// <summary>
/// 배경 별 (패럴랙스 스크롤 효과).
/// </summary>
public sealed class Star : GameObject
{
    private readonly double _speed;

    public Star(double x, double y, double size, double speed, byte brightness)
    {
        X = x;
        Y = y;
        Width = size;
        Height = size;
        _speed = speed;

        Visual = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Color.FromRgb(brightness, brightness, brightness))
        };
    }

    public override void Update(double dt)
    {
        Y += _speed * dt;
        if (Y > 700)
        {
            Y = -2;
        }
    }
}
