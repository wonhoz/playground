using GearWorks.Entities;

namespace GearWorks.Engine;

/// <summary>
/// 기어 체인 BFS 솔버.
/// 모터에서 출발해 맞물린 기어들에 각속도 전파.
/// 외접 전달: ω₂ = -ω₁ × (r₁/r₂)  (방향 반전, 속도 비례)
/// </summary>
public static class GearSolver
{
    // 맞물림 허용 오차 (px): |dist - (r₁+r₂)| ≤ Tolerance
    private const double Tolerance = 6.0;

    /// <summary>
    /// 모든 기어(고정 + 배치된 슬롯)에 대해 BFS 전파.
    /// 전파 완료 후 각 기어의 AngularVelocity, IsSolved 업데이트.
    /// </summary>
    public static void Solve(IReadOnlyList<Gear> gears)
    {
        // 초기화
        foreach (var g in gears)
        {
            g.IsSolved    = false;
            g.IsConnected = false;
            if (g.Role != GearRole.Motor) g.AngularVelocity = 0;
        }

        var motor = gears.FirstOrDefault(g => g.Role == GearRole.Motor);
        if (motor is null) return;

        motor.IsSolved    = true;
        motor.IsConnected = true;

        var queue = new Queue<Gear>();
        queue.Enqueue(motor);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var other in gears)
            {
                if (other.IsSolved) continue;
                if (!CanMesh(cur, other)) continue;

                // 외접 전달: 방향 반전, 기어비 적용
                other.AngularVelocity = -cur.AngularVelocity * (cur.Radius / other.Radius);
                other.IsSolved        = true;
                other.IsConnected     = true;
                queue.Enqueue(other);
            }
        }
    }

    /// <summary>두 기어가 물리적으로 맞물릴 수 있는지 (외접 거리 기준).</summary>
    public static bool CanMesh(Gear a, Gear b)
    {
        double dx   = b.X - a.X;
        double dy   = b.Y - a.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        return Math.Abs(dist - (a.Radius + b.Radius)) <= Tolerance;
    }

    /// <summary>
    /// 레벨 클리어 판정.
    /// output.IsSolved + 방향 목표 일치.
    /// TargetSign: +1=CW, -1=CCW, 0=방향 무관
    /// </summary>
    public static bool CheckClear(Gear output, int targetSign)
    {
        if (!output.IsSolved) return false;
        if (targetSign == 0)  return true;
        return Math.Sign(output.AngularVelocity) == targetSign;
    }
}
