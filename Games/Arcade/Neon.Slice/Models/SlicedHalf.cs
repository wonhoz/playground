using System.Windows.Media;

namespace NeonSlice.Models;

/// <summary>슬라이스된 도형의 반쪽 — 물리 기반으로 날아감</summary>
public sealed class SlicedHalf
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Radius { get; init; }
    public double Rotation { get; set; }
    public double AngularVelocity { get; init; }
    public Color NeonColor { get; init; }
    public ShapeType Type { get; init; }

    // 0=우상단 반쪽, 1=좌하단 반쪽
    public int Half { get; init; }

    public double Life { get; set; } = 0.9;  // 서서히 페이드
    public bool IsDead => Life <= 0;

    public void Update(double dt)
    {
        Vy += NeonShape.Gravity * dt;
        X += Vx * dt;
        Y += Vy * dt;
        Rotation += AngularVelocity * dt;
        Life -= dt * 1.4;
        if (Life < 0) Life = 0;
    }

    public double Alpha => Math.Max(0, Life);
}
