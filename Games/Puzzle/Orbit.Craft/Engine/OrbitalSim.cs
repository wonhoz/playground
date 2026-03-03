namespace OrbitCraft.Engine;

/// <summary>
/// Velocity Verlet(Leapfrog) 기반 N체 궤도 시뮬레이터.
///
/// 뉴턴 만유인력: F = G·M / r²  (방향: 행성 방향)
/// Leapfrog는 에너지 보존이 뛰어나 장기 궤도 시뮬에 적합 (RK4보다 안정).
///
/// 탈출 속도: v_esc = √(2GM/r)
/// </summary>
public static class OrbitalSim
{
    public const double G = 3800.0;
    private const double MinDist = 12.0;

    // ── Velocity Verlet (KDK Leapfrog) ───────────────────
    public static (double x, double y, double vx, double vy) Step(
        double x, double y, double vx, double vy,
        IReadOnlyList<Planet> planets, double dt)
    {
        var (ax0, ay0) = Accel(x, y, planets);
        double vhx = vx + 0.5 * dt * ax0;
        double vhy = vy + 0.5 * dt * ay0;
        double nx  = x  + dt * vhx;
        double ny  = y  + dt * vhy;
        var (ax1, ay1) = Accel(nx, ny, planets);
        return (nx, ny, vhx + 0.5 * dt * ax1, vhy + 0.5 * dt * ay1);
    }

    // ── 중력 가속도 (N체 합산) ────────────────────────────
    public static (double ax, double ay) Accel(
        double x, double y, IReadOnlyList<Planet> planets)
    {
        double ax = 0, ay = 0;
        foreach (var p in planets)
        {
            double dx = p.X - x;
            double dy = p.Y - y;
            double r  = Math.Max(Math.Sqrt(dx*dx + dy*dy), MinDist);
            double f  = G * p.Mass / (r * r);
            ax += f * dx / r;
            ay += f * dy / r;
        }
        return (ax, ay);
    }

    // ── 탈출 속도 ─────────────────────────────────────────
    public static double EscapeVelocity(double x, double y, Planet p)
    {
        double dx = p.X - x, dy = p.Y - y;
        double r  = Math.Max(Math.Sqrt(dx*dx + dy*dy), MinDist);
        return Math.Sqrt(2.0 * G * p.Mass / r);
    }

    // ── 궤도 미리보기 (Aim 모드) ───────────────────────────
    /// <summary>
    /// steps×dt 시간만큼 Leapfrog 적분 → 예측 궤적점 반환.
    /// 화면 이탈 또는 충돌 시 조기 종료.
    /// </summary>
    public static List<(double X, double Y)> PreviewOrbit(
        double x0, double y0, double vx0, double vy0,
        IReadOnlyList<Planet> planets,
        int steps = 480, double dt = 0.022)
    {
        var pts = new List<(double, double)>(steps + 1) { (x0, y0) };
        double x = x0, y = y0, vx = vx0, vy = vy0;

        for (int i = 0; i < steps; i++)
        {
            (x, y, vx, vy) = Step(x, y, vx, vy, planets, dt);
            pts.Add((x, y));
            if (x < -300 || x > 1150 || y < -300 || y > 820) break;
            if (planets.Any(p =>
            {
                double dx = x - p.X, dy = y - p.Y;
                return dx*dx + dy*dy < (p.Radius + 3) * (p.Radius + 3);
            })) break;
        }
        return pts;
    }
}
