using DominoChain.Entities;

namespace DominoChain.Engine;

/// <summary>
/// 도미노 연쇄 물리 시뮬레이터.
/// 강체 충격량 전달, 회전 관성, 정적→동적 마찰 전환 근사 구현.
/// </summary>
public static class Physics
{
    // 중력 가속도 (픽셀/s² 스케일)
    private const double G = 1200.0;

    // 임계 기울기 각도(라디안) — 무게중심이 지지점 바깥으로 이동하는 시점
    // arctan(W / H) ≈ arctan(12/60) ≈ 0.197 rad
    public static double TiltThreshold(double w, double h) => Math.Atan2(w * 0.5, h * 0.5);

    /// <summary>
    /// 한 프레임의 물리 업데이트: 도미노 각 상태 갱신 + 연쇄 충돌 전달.
    /// </summary>
    public static void Step(IReadOnlyList<Domino> dominoes, double dt)
    {
        // 1단계: 각 도미노 회전 업데이트
        foreach (var d in dominoes)
        {
            if (d.State == DominoState.Standing) continue;
            if (d.State == DominoState.Fallen)   continue;

            UpdateRotation(d, dt);
        }

        // 2단계: 충돌 감지 & 충격량 전달 (오른쪽 방향)
        for (int i = 0; i < dominoes.Count - 1; i++)
        {
            var a = dominoes[i];
            var b = dominoes[i + 1];
            if (a.State == DominoState.Fallen || b.State != DominoState.Standing) continue;
            if (a.FallDir > 0) TryTransferImpulse(a, b);  // A가 오른쪽으로 쓰러짐
        }

        // 왼쪽 방향 충돌 전달
        for (int i = dominoes.Count - 1; i > 0; i--)
        {
            var a = dominoes[i];
            var b = dominoes[i - 1];
            if (a.State == DominoState.Fallen || b.State != DominoState.Standing) continue;
            if (a.FallDir < 0) TryTransferImpulse(a, b);
        }
    }

    private static void UpdateRotation(Domino d, double dt)
    {
        // α = (3*G / (2*H)) * sin(θ)  — 하단 피벗 기준 중력 토크
        double alpha = (3.0 * G) / (2.0 * d.H) * Math.Sin(Math.Abs(d.Angle));
        d.AngularVelocity += alpha * dt * Math.Sign(d.FallDir != 0 ? d.FallDir : 1);

        // 최대 각속도 클램프 (너무 빠르게 쓰러지지 않도록)
        d.AngularVelocity = Math.Clamp(d.AngularVelocity, -25.0, 25.0);
        d.Angle += d.AngularVelocity * dt;

        // 90도 이상 → 완전히 쓰러짐
        if (Math.Abs(d.Angle) >= Math.PI * 0.5)
        {
            d.Angle = Math.PI * 0.5 * Math.Sign(d.Angle);
            d.AngularVelocity = 0;
            d.State = DominoState.Fallen;
        }
    }

    private static void TryTransferImpulse(Domino a, Domino b)
    {
        // A의 상단 모서리 (FallDir=+1 이면 오른쪽 상단)
        double topX = a.PivotX + a.H * Math.Sin(a.Angle);
        double topY = a.PivotY - a.H * Math.Cos(a.Angle);

        // B의 AABB (수직 기준)
        double bLeft  = b.PivotX - b.W * 0.5;
        double bRight = b.PivotX + b.W * 0.5;
        double bTop   = b.PivotY - b.H;
        double bBot   = b.PivotY;

        if (topX < bLeft || topX > bRight) return;
        if (topY < bTop  || topY > bBot)  return;

        // 접촉 높이 비율 (0=하단, 1=상단) — 높을수록 강한 충격
        double contactRatio = Math.Clamp((bBot - topY) / b.H, 0.1, 1.0);

        // 충격량 전달: a의 각속도 × 접촉 높이 비율 × 전달 계수(에너지 손실)
        double transferFactor = 0.70;
        double impulse = a.AngularVelocity * contactRatio * transferFactor;

        if (impulse > 0.3) // 유효 충격량 이상일 때만 전달
        {
            b.FallDir = +1;
            b.AngularVelocity = impulse;
            b.State = DominoState.Falling;
        }
    }

    /// <summary>
    /// 첫 번째 도미노를 오른쪽 방향으로 밀어 넘어뜨린다.
    /// </summary>
    public static void Topple(Domino d, int dir = 1)
    {
        if (d.State != DominoState.Standing) return;
        d.FallDir = dir;
        d.State = DominoState.Falling;
        d.AngularVelocity = 1.5; // 초기 각속도
        d.Angle = TiltThreshold(d.W, d.H) * dir + 0.01 * dir; // 임계 기울기 살짝 초과
    }
}
