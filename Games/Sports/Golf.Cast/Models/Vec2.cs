namespace GolfCast.Models;

/// <summary>2D 벡터 — 물리 연산용</summary>
public readonly struct Vec2(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;

    public double Length    => Math.Sqrt(X * X + Y * Y);
    public double LengthSq  => X * X + Y * Y;
    public Vec2   Normalized => Length < 1e-9 ? Zero : new(X / Length, Y / Length);

    public static Vec2 Zero => new(0, 0);

    public static Vec2   operator +(Vec2 a, Vec2 b)   => new(a.X + b.X, a.Y + b.Y);
    public static Vec2   operator -(Vec2 a, Vec2 b)   => new(a.X - b.X, a.Y - b.Y);
    public static Vec2   operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2   operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2   operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
    public static Vec2   operator -(Vec2 a)           => new(-a.X, -a.Y);
    public static double Dot(Vec2 a, Vec2 b)          => a.X * b.X + a.Y * b.Y;

    /// <summary>벽 법선 n에 대한 반사 벡터</summary>
    public Vec2 Reflect(Vec2 n) => this - 2 * Dot(this, n) * n;

    /// <summary>법선 n 방향 성분을 제거한 접선 성분</summary>
    public Vec2 Tangent(Vec2 n) => this - Dot(this, n) * n;

    public Point ToPoint() => new(X, Y);
    public override string ToString() => $"({X:F1}, {Y:F1})";
}
