namespace HookCast.Services;

/// <summary>Verlet 통합 로프 물리 (낚싯줄 시뮬레이션)</summary>
public class PhysicsEngine
{
    public const int    NodeCount    = 14;
    public const double SegmentLen   = 22.0;  // px
    private const double Gravity     = 0.38;
    private const double Damping     = 0.985;
    private const int   Iterations   = 8;     // 제약 해소 반복 횟수

    public RopeNode[] Nodes { get; } = new RopeNode[NodeCount];

    public void Init(Vec2 rodTip)
    {
        for (int i = 0; i < NodeCount; i++)
            Nodes[i] = new RopeNode(new Vec2(rodTip.X + i * 2, rodTip.Y + i * 4));
        Nodes[0].Pinned = true; // 낚싯대 끝에 고정
    }

    public void Step(Vec2 rodTip, bool inWater)
    {
        // 핀 업데이트
        Nodes[0].Pos = rodTip;

        foreach (var node in Nodes)
        {
            if (node.Pinned) continue;

            var vel   = (node.Pos - node.PrevPos) * Damping;
            var gravY = inWater ? Gravity * 0.3 : Gravity;  // 물 속 부력
            node.PrevPos = node.Pos;
            node.Pos     = node.Pos + vel + new Vec2(0, gravY);
        }

        // 제약 해소 (길이 유지)
        for (int iter = 0; iter < Iterations; iter++)
        {
            // 핀 재설정
            Nodes[0].Pos = rodTip;

            for (int i = 0; i < NodeCount - 1; i++)
            {
                var a    = Nodes[i];
                var b    = Nodes[i + 1];
                var diff = b.Pos - a.Pos;
                var len  = diff.Length;
                if (len < 1e-6) continue;
                var correction = diff.Normalized * ((len - SegmentLen) * 0.5);
                if (!a.Pinned) a.Pos = a.Pos + correction;
                if (!b.Pinned) b.Pos = b.Pos - correction;
            }
        }
    }

    /// <summary>후크(마지막 노드) 위치</summary>
    public Vec2 HookPos => Nodes[^1].Pos;

    /// <summary>캐스팅: 초기 속도로 노드 날리기</summary>
    public void Cast(Vec2 rodTip, Vec2 velocity)
    {
        Init(rodTip);
        // 마지막 노드에 초기 속도 적용
        var hook = Nodes[^1];
        hook.PrevPos = hook.Pos - velocity;
    }

    /// <summary>물 위 착수 후 후크 고정</summary>
    public void LandInWater(Vec2 waterPos)
    {
        Nodes[^1].PrevPos = waterPos;
        Nodes[^1].Pos     = waterPos;
    }
}
