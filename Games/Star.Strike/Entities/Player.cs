using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StarStrike.Engine;

namespace StarStrike.Entities;

public sealed class Player : GameObject
{
    private const double Speed = 350;
    private const double ShipW = 36;
    private const double ShipH = 40;

    private readonly InputManager _input;
    private readonly double _areaWidth;
    private readonly double _areaHeight;

    public int Lives { get; set; } = 3;
    public bool IsInvincible { get; private set; }
    private double _invincibleTimer;
    private double _blinkTimer;

    public Player(InputManager input, double areaWidth, double areaHeight)
    {
        _input = input;
        _areaWidth = areaWidth;
        _areaHeight = areaHeight;
        Width = ShipW;
        Height = ShipH;
        X = (areaWidth - ShipW) / 2;
        Y = areaHeight - ShipH - 20;
        Visual = CreateShipVisual();
    }

    private static UIElement CreateShipVisual()
    {
        // 삼각형 우주선 + 불꽃 추진기
        var canvas = new Canvas { Width = ShipW, Height = ShipH };

        // 선체 (삼각형)
        var body = new Polygon
        {
            Points = [new Point(ShipW / 2, 0), new Point(0, ShipH - 5), new Point(ShipW, ShipH - 5)],
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x3A, 0x86, 0xFF),
                Color.FromRgb(0x1A, 0x4E, 0xA8),
                90),
            Stroke = new SolidColorBrush(Color.FromRgb(0x5A, 0xA0, 0xFF)),
            StrokeThickness = 1.2
        };

        // 추진 불꽃
        var flame = new Polygon
        {
            Points = [new Point(ShipW / 2 - 6, ShipH - 5), new Point(ShipW / 2, ShipH), new Point(ShipW / 2 + 6, ShipH - 5)],
            Fill = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xA5, 0x00),
                Color.FromRgb(0xFF, 0x45, 0x00),
                90)
        };

        // 코어 (원형 빛)
        var core = new Ellipse
        {
            Width = 6, Height = 6,
            Fill = new SolidColorBrush(Color.FromRgb(0xAA, 0xDD, 0xFF))
        };
        Canvas.SetLeft(core, ShipW / 2 - 3);
        Canvas.SetTop(core, ShipH / 2 - 2);

        canvas.Children.Add(flame);
        canvas.Children.Add(body);
        canvas.Children.Add(core);
        return canvas;
    }

    public override void Update(double dt)
    {
        // 이동
        double dx = 0, dy = 0;
        if (_input.Left) dx -= Speed * dt;
        if (_input.Right) dx += Speed * dt;
        if (_input.Up) dy -= Speed * dt;
        if (_input.Down) dy += Speed * dt;

        X = Math.Clamp(X + dx, 0, _areaWidth - Width);
        Y = Math.Clamp(Y + dy, 0, _areaHeight - Height);

        // 무적 타이머
        if (IsInvincible)
        {
            _invincibleTimer -= dt;
            _blinkTimer -= dt;
            if (_blinkTimer <= 0)
            {
                _blinkTimer = 0.1;
                if (Visual is not null)
                    Visual.Opacity = Visual.Opacity > 0.5 ? 0.3 : 1.0;
            }
            if (_invincibleTimer <= 0)
            {
                IsInvincible = false;
                if (Visual is not null) Visual.Opacity = 1.0;
            }
        }
    }

    public void Hit()
    {
        if (IsInvincible) return;
        Lives--;
        if (Lives <= 0)
        {
            IsAlive = false;
            return;
        }
        IsInvincible = true;
        _invincibleTimer = 2.0;
        _blinkTimer = 0.1;
    }

    public void Reset(double areaWidth, double areaHeight)
    {
        Lives = 3;
        IsAlive = true;
        IsInvincible = false;
        X = (areaWidth - Width) / 2;
        Y = areaHeight - Height - 20;
        if (Visual is not null) Visual.Opacity = 1.0;
    }
}
