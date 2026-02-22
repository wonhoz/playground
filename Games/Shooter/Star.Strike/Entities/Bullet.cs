using System.Windows.Media;
using System.Windows.Shapes;
using StarStrike.Engine;

namespace StarStrike.Entities;

public sealed class Bullet : GameObject
{
    private const double BulletSpeed = 600;
    private const double BulletW = 4;
    private const double BulletH = 14;

    public bool IsPlayerBullet { get; }

    public Bullet(double x, double y, bool isPlayerBullet)
    {
        IsPlayerBullet = isPlayerBullet;
        Width = BulletW;
        Height = BulletH;
        X = x - BulletW / 2;
        Y = y;

        Visual = new Rectangle
        {
            Width = BulletW,
            Height = BulletH,
            RadiusX = 2, RadiusY = 2,
            Fill = isPlayerBullet
                ? new LinearGradientBrush(
                    Color.FromRgb(0x00, 0xFF, 0xCC),
                    Color.FromRgb(0x00, 0xAA, 0x88), 90)
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44))
        };
    }

    public override void Update(double dt)
    {
        Y += (IsPlayerBullet ? -BulletSpeed : BulletSpeed * 0.6) * dt;
        if (Y < -BulletH || Y > 800)
            IsAlive = false;
    }
}
