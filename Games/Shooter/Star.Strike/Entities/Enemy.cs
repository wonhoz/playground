using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StarStrike.Engine;

namespace StarStrike.Entities;

public enum EnemyType { Basic, Fast, Tank }

public sealed class Enemy : GameObject
{
    private readonly double _speed;
    private readonly double _areaWidth;
    private double _shootTimer;
    private readonly double _shootInterval;
    private readonly Random _rng;

    public EnemyType Type { get; }
    public int Hp { get; set; }
    public int ScoreValue { get; }
    public bool CanShoot { get; }

    // 좌우 이동
    private double _moveDir = 1;
    private readonly double _horizontalSpeed;

    public Enemy(EnemyType type, double x, double y, double areaWidth, Random rng)
    {
        _areaWidth = areaWidth;
        _rng = rng;
        Type = type;
        X = x;
        Y = y;

        switch (type)
        {
            case EnemyType.Basic:
                Width = 30; Height = 28;
                _speed = 80; Hp = 1; ScoreValue = 100;
                CanShoot = true; _shootInterval = 2.5;
                _horizontalSpeed = 40;
                Visual = CreateBasicVisual();
                break;
            case EnemyType.Fast:
                Width = 24; Height = 24;
                _speed = 160; Hp = 1; ScoreValue = 150;
                CanShoot = false; _shootInterval = 999;
                _horizontalSpeed = 100;
                Visual = CreateFastVisual();
                break;
            case EnemyType.Tank:
                Width = 40; Height = 36;
                _speed = 50; Hp = 3; ScoreValue = 300;
                CanShoot = true; _shootInterval = 1.8;
                _horizontalSpeed = 20;
                Visual = CreateTankVisual();
                break;
        }

        _shootTimer = _rng.NextDouble() * _shootInterval;
    }

    public override void Update(double dt)
    {
        Y += _speed * dt;

        // 좌우 지그재그
        X += _horizontalSpeed * _moveDir * dt;
        if (X <= 0 || X + Width >= _areaWidth) _moveDir = -_moveDir;

        if (Y > 750) IsAlive = false;

        _shootTimer -= dt;
    }

    public bool TryShoot()
    {
        if (!CanShoot || _shootTimer > 0) return false;
        _shootTimer = _shootInterval + _rng.NextDouble() * 0.5;
        return true;
    }

    public void TakeDamage()
    {
        Hp--;
        if (Hp <= 0) IsAlive = false;
    }

    private UIElement CreateBasicVisual()
    {
        var canvas = new Canvas { Width = Width, Height = Height };
        var body = new Polygon
        {
            Points = [new Point(Width / 2, Height), new Point(0, 0), new Point(Width, 0)],
            Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x77, 0x66)),
            StrokeThickness = 1
        };
        canvas.Children.Add(body);
        return canvas;
    }

    private UIElement CreateFastVisual()
    {
        var canvas = new Canvas { Width = Width, Height = Height };
        var body = new Polygon
        {
            Points = [new Point(Width / 2, Height), new Point(0, 6), new Point(Width / 2, 0), new Point(Width, 6)],
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x55)),
            StrokeThickness = 1
        };
        canvas.Children.Add(body);
        return canvas;
    }

    private UIElement CreateTankVisual()
    {
        var canvas = new Canvas { Width = Width, Height = Height };
        var body = new Rectangle
        {
            Width = Width, Height = Height - 6,
            RadiusX = 4, RadiusY = 4,
            Fill = new SolidColorBrush(Color.FromRgb(0x8E, 0x44, 0xAD)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xBB, 0x77, 0xDD)),
            StrokeThickness = 1.5
        };
        var cockpit = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = new SolidColorBrush(Color.FromRgb(0xDD, 0xBB, 0xFF))
        };
        Canvas.SetLeft(cockpit, Width / 2 - 5);
        Canvas.SetTop(cockpit, (Height - 6) / 2 - 5);
        canvas.Children.Add(body);
        canvas.Children.Add(cockpit);
        return canvas;
    }
}
