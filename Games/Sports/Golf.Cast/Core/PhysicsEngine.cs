using GolfCast.Models;

namespace GolfCast.Core;

/// <summary>미니골프 물리 엔진 — 순수 수학 기반 (no Box2D)</summary>
public class PhysicsEngine
{
    // 마찰 계수
    public const double FrictionFairway = 0.985;
    public const double FrictionSand    = 0.88;
    public const double FrictionSlope   = 0.992;

    // 경사 가속
    public const double SlopeAccel = 120.0;  // px/s²

    // 홀 흡입 반경 배수
    public const double HoleSuckRadius = 1.8;

    private readonly Random _rng = new();

    /// <summary>물리 스텝 — dt는 초 단위</summary>
    public void Step(Ball ball, HoleData hole, double dt)
    {
        if (ball.InHole || ball.InWater) return;

        // 1. 특수 지형 효과 적용
        var region = GetRegion(ball.Pos, hole.Regions);
        ball.Vel  = ApplyRegionEffects(ball.Vel, region, dt);

        // 2. 위치 업데이트
        ball.Pos += ball.Vel * dt;

        // 3. 장애물 업데이트 & 충돌
        foreach (var obs in hole.Obstacles)
        {
            obs.Update(dt);
            CollideWithObstacle(ball, obs);
        }

        // 4. 벽 충돌
        foreach (var wall in hole.Walls)
            CollideWithWall(ball, wall);

        // 5. 홀 흡입
        var toHole = hole.HolePos - ball.Pos;
        if (toHole.Length < hole.HoleRadius * HoleSuckRadius)
        {
            // 홀 안으로 빠져들기
            ball.Vel += toHole.Normalized * 200 * dt;
            if (toHole.Length < hole.HoleRadius * 0.7)
            {
                ball.InHole = true;
                ball.Vel    = Vec2.Zero;
                ball.Pos    = hole.HolePos;
                return;
            }
        }

        // 6. 물 빠짐
        if (region?.Kind == TileKind.Water)
        {
            ball.InWater = true;
            ball.Vel     = Vec2.Zero;
        }

        // 7. 감속 (공기 마찰 + 잔디)
        double friction = region?.Kind switch
        {
            TileKind.Sand   => FrictionSand,
            TileKind.Slope  => FrictionSlope,
            _               => FrictionFairway,
        };
        ball.Vel *= Math.Pow(friction, dt * 60);

        // 8. 최저 속도 컷
        if (ball.Vel.LengthSq < 0.5) ball.Vel = Vec2.Zero;
    }

    // ── 벽 충돌 (원 vs 선분) ────────────────────────────────────────────────

    private static void CollideWithWall(Ball ball, WallSegment wall)
    {
        var closest = ClosestPointOnSegment(ball.Pos, wall.A, wall.B);
        var diff    = ball.Pos - closest;
        double dist = diff.Length;

        if (dist < ball.Radius && dist > 1e-6)
        {
            var n    = diff.Normalized;
            ball.Pos = closest + n * ball.Radius;
            ball.Vel = ball.Vel.Reflect(n) * 0.68;   // 반발 계수
        }
    }

    private static Vec2 ClosestPointOnSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double t = Vec2.Dot(p - a, ab) / ab.LengthSq;
        t = Math.Clamp(t, 0, 1);
        return a + ab * t;
    }

    // ── 장애물 충돌 ─────────────────────────────────────────────────────────

    private static void CollideWithObstacle(Ball ball, Obstacle obs)
    {
        switch (obs.Kind)
        {
            case ObstacleKind.Windmill:
                foreach (var blade in obs.GetWindmillBlades())
                    CollideWithWall(ball, blade);
                break;
            case ObstacleKind.MovingWall:
                // 이동 벽을 폭 있는 박스로 근사
                var half   = new Vec2(obs.Width / 2, obs.Height / 2);
                var wallA  = new Vec2(obs.Center.X - half.X, obs.Center.Y);
                var wallB  = new Vec2(obs.Center.X + half.X, obs.Center.Y);
                CollideWithWall(ball, new WallSegment(wallA, wallB));
                break;
        }
    }

    // ── 지형 효과 ────────────────────────────────────────────────────────────

    private static Vec2 ApplyRegionEffects(Vec2 vel, TileRegion? region, double dt)
    {
        if (region is null) return vel;
        return region.Kind switch
        {
            TileKind.Slope => ApplySlope(vel, region.Slope, dt),
            _              => vel,
        };
    }

    private static Vec2 ApplySlope(Vec2 vel, SlopeDir dir, double dt)
    {
        var gravity = dir switch
        {
            SlopeDir.Up    => new Vec2(0,  -SlopeAccel),
            SlopeDir.Down  => new Vec2(0,   SlopeAccel),
            SlopeDir.Left  => new Vec2(-SlopeAccel, 0),
            SlopeDir.Right => new Vec2( SlopeAccel, 0),
            _              => Vec2.Zero,
        };
        return vel + gravity * dt;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private static TileRegion? GetRegion(Vec2 pos, List<TileRegion> regions)
    {
        var p = new Point(pos.X, pos.Y);
        return regions.FirstOrDefault(r => r.Bounds.Contains(p));
    }
}
