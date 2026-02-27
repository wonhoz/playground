using System.Windows.Media;

namespace NeonSlice.Models;

public sealed class Particle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Life { get; set; }   // 0~1, 1=신생, 0=소멸
    public double MaxLife { get; init; } = 0.7;
    public double Radius { get; init; } = 3;
    public Color Color { get; init; }

    public bool IsDead => Life <= 0;

    public void Update(double dt)
    {
        Vy += 160 * dt; // 약한 중력
        X += Vx * dt;
        Y += Vy * dt;
        Life -= dt / MaxLife;
        if (Life < 0) Life = 0;
    }

    public double Alpha => Life * Life; // 비선형 페이드
}
