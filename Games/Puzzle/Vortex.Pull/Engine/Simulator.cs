using VortexPull.Entities;

namespace VortexPull.Engine;

/// <summary>
/// RK4(4차 룽게-쿠타) 기반 N체 궤도 시뮬레이터.
///
/// 발생기 별 힘:
///   인력(Attract): F = Strength / r²  (방향: 발생기 쪽)
///   척력(Repel):   F = Strength / r²  (방향: 반대)
///   소용돌이(Vortex): F_t = Strength (접선 방향, CCW 기준)
///
/// 최소 거리 클램프(MinDist=18px) 로 특이점 방지.
/// </summary>
public static class Simulator
{
    private const double MinDist = 18.0;

    // ── 단일 RK4 스텝 ───────────────────────────────────
    public static (double x, double y, double vx, double vy) Step(
        double x, double y, double vx, double vy,
        IReadOnlyList<Generator> gens, double dt)
    {
        var (ax1, ay1) = Accel(x,              y,              gens);
        var (ax2, ay2) = Accel(x + vx*dt*0.5, y + vy*dt*0.5, gens);
        var (ax3, ay3) = Accel(x + vx*dt*0.5 + ax2*dt*dt*0.25,
                               y + vy*dt*0.5 + ay2*dt*dt*0.25, gens);
        var (ax4, ay4) = Accel(x + (vx + ax3*dt)*dt,
                               y + (vy + ay3*dt)*dt, gens);

        double nvx = vx + dt / 6.0 * (ax1 + 2*ax2 + 2*ax3 + ax4);
        double nvy = vy + dt / 6.0 * (ay1 + 2*ay2 + 2*ay3 + ay4);
        double nx  = x  + dt / 6.0 * ((vx)     + 2*(vx+ax2*dt*0.5)
                                      + 2*(vx+ax3*dt*0.5) + (vx+ax4*dt));
        double ny  = y  + dt / 6.0 * ((vy)     + 2*(vy+ay2*dt*0.5)
                                      + 2*(vy+ay3*dt*0.5) + (vy+ay4*dt));
        return (nx, ny, nvx, nvy);
    }

    // ── 궤도 미리보기 ────────────────────────────────────
    /// <summary>
    /// steps 수만큼 RK4 통합 → 예측 궤도점 반환.
    /// dt_preview 는 보통 실제 dt보다 크게 설정해 긴 궤도를 빠르게 계산.
    /// </summary>
    public static List<(double X, double Y)> PreviewOrbit(
        double x0, double y0, double vx0, double vy0,
        IReadOnlyList<Generator> gens,
        int steps = 400, double dt = 0.025)
    {
        var pts = new List<(double, double)>(steps + 1) { (x0, y0) };
        double x = x0, y = y0, vx = vx0, vy = vy0;
        for (int i = 0; i < steps; i++)
        {
            (x, y, vx, vy) = Step(x, y, vx, vy, gens, dt);
            pts.Add((x, y));
        }
        return pts;
    }

    // ── 가속도 계산 ──────────────────────────────────────
    public static (double ax, double ay) Accel(double x, double y, IReadOnlyList<Generator> gens)
    {
        double ax = 0, ay = 0;
        foreach (var g in gens)
        {
            double dx   = g.X - x;
            double dy   = g.Y - y;
            double dist = Math.Sqrt(dx*dx + dy*dy);
            double r    = Math.Max(dist, MinDist);

            switch (g.Kind)
            {
                case GeneratorKind.Attract:
                {
                    double fMag = g.Strength / (r * r);
                    ax += fMag * dx / r;
                    ay += fMag * dy / r;
                    break;
                }
                case GeneratorKind.Repel:
                {
                    double fMag = g.Strength / (r * r);
                    ax -= fMag * dx / r;
                    ay -= fMag * dy / r;
                    break;
                }
                case GeneratorKind.Vortex:
                {
                    // 접선 방향 (CCW): (-dy/r, dx/r)
                    ax += g.Strength * (-dy / r);
                    ay += g.Strength * ( dx / r);
                    break;
                }
            }
        }
        return (ax, ay);
    }
}
