using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StarStrike.Engine;

/// <summary>
/// 게임 오브젝트 베이스 클래스. Canvas 위의 Shape 또는 UIElement를 래핑.
/// </summary>
public abstract class GameObject
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsAlive { get; set; } = true;

    public UIElement? Visual { get; protected set; }

    public Rect Bounds => new(X, Y, Width, Height);

    public abstract void Update(double dt);

    public void SyncPosition()
    {
        if (Visual is null) return;
        Canvas.SetLeft(Visual, X);
        Canvas.SetTop(Visual, Y);
    }

    public bool CollidesWith(GameObject other)
    {
        if (!IsAlive || !other.IsAlive) return false;
        return Bounds.IntersectsWith(other.Bounds);
    }
}
