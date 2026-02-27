using System.Windows;
using System.Windows.Media;

namespace NeonSlice.Models;

public enum ShapeType
{
    Circle,
    Triangle,
    Square,
    Pentagon,
    Star,      // 보너스 +10점
    Bomb,      // 폭탄 감점
    Lightning, // 화면 클리어
    Ice,       // 슬로모션 3초
}

public enum GameMode
{
    Classic,    // 무한 생존 (목숨 3개)
    TimeAttack, // 60초 제한
    Zen,        // N개 슬라이스 내 최대 콤보
}

public sealed class NeonShape
{
    private static int _idCounter;

    public int Id { get; } = System.Threading.Interlocked.Increment(ref _idCounter);
    public ShapeType Type { get; init; }
    public Color NeonColor { get; init; }

    // 물리 상태
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Radius { get; init; } = 32;
    public double Rotation { get; set; }      // 도
    public double AngularVelocity { get; init; } // 도/초

    public bool IsSliced { get; set; }
    public bool IsMissed { get; set; }  // 화면 아래로 사라짐 (목숨 소모)

    // 중력 상수 (픽셀/초²)
    public const double Gravity = 420;

    public void Update(double dt)
    {
        Vy += Gravity * dt;
        X += Vx * dt;
        Y += Vy * dt;
        Rotation += AngularVelocity * dt;
    }

    public bool IsOffScreen(double height) => Y - Radius > height;
    public bool IsAboveScreen() => Y + Radius < 0;
}
