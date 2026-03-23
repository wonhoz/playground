using ClothCut.Entities;

namespace ClothCut.Engine;

/// <summary>
/// 스프링-질량 천 시뮬레이터.
/// Verlet Integration + Position Based Dynamics 제약 해결.
/// </summary>
public static class ClothSim
{
    private const int    SubSteps  = 3;    // 물리 안정성을 위한 서브스텝
    private const int    Iterations = 8;   // 제약 해결 반복 수
    private const double Gravity   = 900;  // px/s²
    private const double Damping   = 0.988; // 속도 감쇠 (에어 저항)
    private const double BoundsY   = 520;  // 화면 하단 경계

    // ── 메인 스텝 ─────────────────────────────────────────

    public static void Step(ClothMesh mesh, double dt)
    {
        double subDt = dt / SubSteps;
        for (int s = 0; s < SubSteps; s++)
        {
            ApplyVerlet(mesh, subDt);
            for (int i = 0; i < Iterations; i++)
                SolveConstraints(mesh);
            ClampBounds(mesh);
        }
    }

    // ── Verlet 통합 ───────────────────────────────────────

    private static void ApplyVerlet(ClothMesh mesh, double dt)
    {
        double dt2 = dt * dt;

        foreach (var n in mesh.Nodes)
        {
            if (n.IsPinned) continue;

            double vx = (n.X - n.OldX) * Damping;
            double vy = (n.Y - n.OldY) * Damping;

            n.OldX = n.X;
            n.OldY = n.Y;

            n.X += vx;
            n.Y += vy + Gravity * dt2;
        }
    }

    // ── Position Based Dynamics 제약 해결 ─────────────────

    private static void SolveConstraints(ClothMesh mesh)
    {
        double stiffness = mesh.Stiffness;

        foreach (var link in mesh.Links)
        {
            if (link.IsCut) continue;

            var a = link.A;
            var b = link.B;

            double dx   = b.X - a.X;
            double dy   = b.Y - a.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 1e-6) continue;

            double diff       = (dist - link.RestLength) / dist * 0.5 * stiffness;
            double corrX      = dx * diff;
            double corrY      = dy * diff;

            if (!a.IsPinned) { a.X += corrX; a.Y += corrY; }
            if (!b.IsPinned) { b.X -= corrX; b.Y -= corrY; }
        }
    }

    // ── 경계 클램프 ───────────────────────────────────────

    private static void ClampBounds(ClothMesh mesh)
    {
        foreach (var n in mesh.Nodes)
        {
            if (n.IsPinned) continue;
            if (n.Y > BoundsY) { n.Y = BoundsY; n.OldY = n.Y + (n.Y - n.OldY) * -0.2; }
        }
    }

    // ── 절단 ──────────────────────────────────────────────

    /// <summary>
    /// 마우스 드래그 경로(점 목록)와 교차하는 모든 링크를 절단.
    /// 절단된 링크 수를 반환.
    /// </summary>
    public static int Cut(ClothMesh mesh, IReadOnlyList<(double X, double Y)> path)
    {
        int cutCount = 0;

        for (int seg = 0; seg < path.Count - 1; seg++)
        {
            double ax = path[seg].X,     ay = path[seg].Y;
            double bx = path[seg + 1].X, by = path[seg + 1].Y;

            foreach (var link in mesh.Links)
            {
                if (link.IsCut) continue;
                // Shear 링크는 절단 대상에서 제외 (구조 변형 최소화)
                if (link.Type == LinkType.Shear) continue;

                if (SegmentsIntersect(ax, ay, bx, by,
                                      link.A.X, link.A.Y, link.B.X, link.B.Y))
                {
                    link.IsCut = true;
                    cutCount++;
                }
            }
        }

        return cutCount;
    }

    // ── 선분 교차 판정 ────────────────────────────────────

    private static bool SegmentsIntersect(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        double d1 = Cross(dx - cx, dy - cy, ax - cx, ay - cy);
        double d2 = Cross(dx - cx, dy - cy, bx - cx, by - cy);
        double d3 = Cross(bx - ax, by - ay, cx - ax, cy - ay);
        double d4 = Cross(bx - ax, by - ay, dx - ax, dy - ay);

        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    private static double Cross(double ux, double uy, double vx, double vy)
        => ux * vy - uy * vx;
}
