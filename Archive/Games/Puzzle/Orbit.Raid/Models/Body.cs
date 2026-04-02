using System.Windows;

namespace OrbitRaid.Models;

public enum BodyType { Planet, Moon, Player, Target }

public class Body
{
    public BodyType Type { get; init; }
    public string Name { get; set; } = string.Empty;

    // 물리 상태 (미터 단위)
    public Vector2D Position { get; set; }
    public Vector2D Velocity { get; set; }
    public double Mass { get; set; }        // kg
    public double Radius { get; set; }      // m (표시용)

    // 목표 궤도 (Target 전용)
    public double TargetOrbitRadius { get; set; }  // 중심 천체 기준 거리
    public Body? TargetCenter { get; set; }

    public bool IsStatic { get; set; } = false;  // 행성은 고정
    public bool IsAlive { get; set; } = true;

    // SOI (Sphere of Influence) — 이 범위 내로 진입하면 캡처
    public double SOI { get; set; }

    // UI 색상
    public System.Windows.Media.Color Color { get; set; } = System.Windows.Media.Colors.White;

    public Vector2D AccumulatedForce { get; set; }

    public void ResetForce() => AccumulatedForce = Vector2D.Zero;

    public void AddGravityFrom(Body other)
    {
        if (IsStatic || !IsAlive || !other.IsAlive) return;
        var G = PhysicsConstants.G;
        var delta = other.Position - Position;
        var dist = delta.Length;
        if (dist < 1.0) return;
        var force = G * Mass * other.Mass / (dist * dist);
        AccumulatedForce += delta.Normalized() * force;
    }

    public void Integrate(double dt)
    {
        if (IsStatic || !IsAlive) return;
        var accel = AccumulatedForce / Mass;
        Velocity += accel * dt;
        Position += Velocity * dt;
    }
}

public static class PhysicsConstants
{
    public const double G = 6.674e-11;
}

public record struct Vector2D(double X, double Y)
{
    public static readonly Vector2D Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vector2D Normalized()
    {
        var l = Length;
        return l < 1e-15 ? Zero : new(X / l, Y / l);
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);
    public static Vector2D operator *(double s, Vector2D a) => new(a.X * s, a.Y * s);
    public static Vector2D operator /(Vector2D a, double s) => new(a.X / s, a.Y / s);
    public static Vector2D operator -(Vector2D a) => new(-a.X, -a.Y);

    public Point ToPoint(double scale, double cx, double cy)
        => new(cx + X * scale, cy - Y * scale);
}
