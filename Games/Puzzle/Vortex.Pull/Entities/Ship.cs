namespace VortexPull.Entities;

/// <summary>우주선 상태 — 위치·속도·궤적 히스토리.</summary>
public class Ship
{
    public double X, Y;
    public double Vx, Vy;

    // 런타임 궤적 (실행 중 업데이트)
    public readonly List<(double X, double Y)> Trail = [];

    public void Reset(double startX, double startY, double startVx = 0, double startVy = 0)
    {
        X = startX; Y = startY;
        Vx = startVx; Vy = startVy;
        Trail.Clear();
    }
}
