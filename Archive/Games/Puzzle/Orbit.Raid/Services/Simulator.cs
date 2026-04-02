using OrbitRaid.Models;

namespace OrbitRaid.Services;

public class SimulatorState
{
    public List<Body> Bodies { get; init; } = [];
    public Body? Player { get; set; }
    public Body? Target { get; set; }
    public double ElapsedTime { get; set; }
    public bool IsRunning { get; set; }
    public GameResult Result { get; set; } = GameResult.Running;
}

public enum GameResult { Running, Success, Collision, Escaped }

public static class Simulator
{
    private const double G = PhysicsConstants.G;
    // RK4 step size: 시뮬 스피드에 따라 메인에서 조정
    public const double BaseTimeStep = 3600.0; // 1 시간 per tick

    /// <summary>RK4 적분 — 모든 천체에 적용</summary>
    public static void Step(SimulatorState state, double dt)
    {
        if (!state.IsRunning) return;

        var allBodies = GetMovingBodies(state);

        // RK4: k1
        var acc1 = ComputeAccelerations(allBodies, state);
        var (pos1, vel1) = ApplyKinematics(allBodies, dt / 2, acc1);

        // RK4: k2 (절반 스텝)
        var tmpBodies = CloneBodies(allBodies, pos1, vel1);
        var acc2 = ComputeAccelerations(tmpBodies, state);
        var (pos2, vel2) = ApplyKinematics(allBodies, dt / 2, acc2);

        // RK4: k3
        tmpBodies = CloneBodies(allBodies, pos2, vel2);
        var acc3 = ComputeAccelerations(tmpBodies, state);
        var (pos3, vel3) = ApplyKinematics(allBodies, dt, acc3);

        // RK4: k4 (전체 스텝)
        tmpBodies = CloneBodies(allBodies, pos3, vel3);
        var acc4 = ComputeAccelerations(tmpBodies, state);

        // 최종 적용
        for (int i = 0; i < allBodies.Count; i++)
        {
            var b = allBodies[i];
            b.Velocity += (acc1[i] + 2 * acc2[i] + 2 * acc3[i] + acc4[i]) * (dt / 6);
            b.Position += b.Velocity * dt;
        }

        state.ElapsedTime += dt;
        CheckCollisions(state);
    }

    private static List<Body> GetMovingBodies(SimulatorState state)
    {
        var list = state.Bodies.Where(b => !b.IsStatic && b.IsAlive).ToList();
        if (state.Player != null && state.Player.IsAlive) list.Add(state.Player);
        return list;
    }

    private static List<Vector2D> ComputeAccelerations(List<Body> bodies, SimulatorState state)
    {
        var staticBodies = state.Bodies.Where(b => b.IsStatic && b.IsAlive).ToList();
        var allGravityBodies = staticBodies.Concat(bodies).ToList();
        var acc = new List<Vector2D>(bodies.Count);
        for (int i = 0; i < bodies.Count; i++)
        {
            var a = Vector2D.Zero;
            var b = bodies[i];
            foreach (var other in allGravityBodies)
            {
                if (ReferenceEquals(b, other)) continue;
                var delta = other.Position - b.Position;
                var dist = delta.Length;
                if (dist < 1.0) continue;
                var force = G * other.Mass / (dist * dist);
                a += delta.Normalized() * force;
            }
            acc.Add(a);
        }
        return acc;
    }

    private static (List<Vector2D> pos, List<Vector2D> vel) ApplyKinematics(
        List<Body> bodies, double dt, List<Vector2D> acc)
    {
        var pos = new List<Vector2D>(bodies.Count);
        var vel = new List<Vector2D>(bodies.Count);
        for (int i = 0; i < bodies.Count; i++)
        {
            vel.Add(bodies[i].Velocity + acc[i] * dt);
            pos.Add(bodies[i].Position + bodies[i].Velocity * dt);
        }
        return (pos, vel);
    }

    private static List<Body> CloneBodies(List<Body> bodies, List<Vector2D> pos, List<Vector2D> vel)
    {
        var result = new List<Body>(bodies.Count);
        for (int i = 0; i < bodies.Count; i++)
        {
            result.Add(new Body
            {
                Type = bodies[i].Type,
                Mass = bodies[i].Mass,
                Radius = bodies[i].Radius,
                Position = pos[i],
                Velocity = vel[i],
                IsStatic = false,
                IsAlive = bodies[i].IsAlive
            });
        }
        return result;
    }

    private static void CheckCollisions(SimulatorState state)
    {
        if (state.Player == null || !state.Player.IsAlive) return;

        var allBodies = state.Bodies.Where(b => b.IsAlive).ToList();

        // 천체 충돌 체크
        foreach (var b in allBodies)
        {
            var dist = (state.Player.Position - b.Position).Length;
            var minDist = b.Radius * 1.1;
            if (dist < minDist)
            {
                state.Result = GameResult.Collision;
                state.IsRunning = false;
                return;
            }
        }

        // 탈출 체크 (시스템 중심에서 너무 멀어짐)
        var maxDist = allBodies.Max(b => b.Position.Length) * 3 + 1e12;
        if (state.Player.Position.Length > maxDist)
        {
            state.Result = GameResult.Escaped;
            state.IsRunning = false;
            return;
        }

        // 성공 체크: Target SOI 진입
        if (state.Target != null)
        {
            var distToTarget = (state.Player.Position - state.Target.Position).Length;
            if (distToTarget < state.Target.SOI)
            {
                state.Result = GameResult.Success;
                state.IsRunning = false;
            }
        }
    }

    /// <summary>궤도 예측 경로 계산 (미래 N 스텝)</summary>
    public static List<Vector2D> PredictOrbit(SimulatorState state, int steps, double dt)
    {
        // 상태 복사
        var playerCopy = new Body
        {
            Type = BodyType.Player,
            Position = state.Player!.Position,
            Velocity = state.Player.Velocity,
            Mass = state.Player.Mass,
            IsAlive = true
        };
        var bodiesCopy = state.Bodies.Select(b => new Body
        {
            Type = b.Type,
            Position = b.Position,
            Velocity = b.Velocity,
            Mass = b.Mass,
            Radius = b.Radius,
            IsStatic = b.IsStatic,
            IsAlive = b.IsAlive
        }).ToList();

        var tempState = new SimulatorState
        {
            Bodies = bodiesCopy,
            Player = playerCopy,
            Target = null,
            IsRunning = true,
            Result = GameResult.Running
        };

        var path = new List<Vector2D>(steps) { playerCopy.Position };
        for (int i = 0; i < steps; i++)
        {
            Step(tempState, dt);
            if (!tempState.IsRunning) break;
            path.Add(playerCopy.Position);
        }
        return path;
    }
}
