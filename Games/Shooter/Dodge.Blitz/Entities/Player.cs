using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DodgeBlitz.Engine;

namespace DodgeBlitz.Entities;

public sealed class Player : GameObject
{
    private const double MoveSpeed = 230.0;

    public bool IsInvincible => _invincibleTimer > 0;
    private double _invincibleTimer;
    private double _blinkTimer;

    private readonly double _areaW;
    private readonly double _areaH;

    public Player(double areaW, double areaH)
    {
        _areaW = areaW;
        _areaH = areaH;
        Width  = 14;
        Height = 14;
        X = areaW / 2 - Width / 2;
        Y = areaH / 2 - Height / 2;

        var poly = new Polygon
        {
            Points = new PointCollection(
            [
                new Point(7, 0), new Point(14, 7),
                new Point(7, 14), new Point(0, 7)
            ]),
            Fill            = new SolidColorBrush(Color.FromRgb(0, 255, 204)),
            Stroke          = new SolidColorBrush(Color.FromRgb(0, 190, 155)),
            StrokeThickness = 1.5,
            Effect          = new DropShadowEffect
            {
                Color       = Color.FromRgb(0, 255, 204),
                BlurRadius  = 14,
                ShadowDepth = 0,
                Opacity     = 0.9
            }
        };
        Visual = poly;
    }

    public override void Update(double dt)
    {
        if (_invincibleTimer > 0)
        {
            _invincibleTimer -= dt;
            _blinkTimer      -= dt;
            if (_blinkTimer <= 0)
            {
                _blinkTimer = 0.08;
                if (Visual is not null)
                    Visual.Opacity = Visual.Opacity > 0.5 ? 0.2 : 1.0;
            }
            if (_invincibleTimer <= 0 && Visual is not null)
                Visual.Opacity = 1.0;
        }
    }

    public void Move(bool left, bool right, bool up, bool down, double dt)
    {
        double dx = 0, dy = 0;
        if (left)  dx -= MoveSpeed * dt;
        if (right) dx += MoveSpeed * dt;
        if (up)    dy -= MoveSpeed * dt;
        if (down)  dy += MoveSpeed * dt;

        // 대각선 이동 정규화
        if (dx != 0 && dy != 0) { dx *= 0.7071; dy *= 0.7071; }

        X = Math.Clamp(X + dx, 0, _areaW - Width);
        Y = Math.Clamp(Y + dy, 0, _areaH - Height);
    }

    public void ResetPosition()
    {
        X = _areaW / 2 - Width / 2;
        Y = _areaH / 2 - Height / 2;
        if (Visual is not null) Visual.Opacity = 1.0;
        _invincibleTimer = 0;
    }

    /// <summary>피격 후 잠시 무적 + 깜빡임.</summary>
    public void Flash(double duration = 0.4)
    {
        _invincibleTimer = duration;
        _blinkTimer      = 0.05;
    }
}
