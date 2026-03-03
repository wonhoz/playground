namespace VortexPull.Levels;

/// <summary>슬롯 초기 정보 — 위치.</summary>
public record SlotDef(double X, double Y);

/// <summary>장애물 — 원형 위험 구역.</summary>
public record ObstacleDef(double X, double Y, double Radius);

/// <summary>레벨 정의.</summary>
public record LevelDef(
    int          Number,
    string       Name,
    string       Hint,
    double       ShipX,   double ShipY,
    double       ShipVx,  double ShipVy,
    double       PortalX, double PortalY, double PortalRadius,
    SlotDef[]    Slots,
    ObstacleDef[] Obstacles
);

public static class LevelData
{
    public const int MaxLevel = 5;

    public static LevelDef Get(int n) => n switch
    {
        1 => Level1(), 2 => Level2(), 3 => Level3(),
        4 => Level4(), 5 => Level5(),
        _ => Level1()
    };

    // ── 레벨 1: 단순 인력 ───────────────────────────────
    // 우주선 좌측, 포털 우측 — 인력 1개로 끌어당김
    // 정답: Attract 1개 (중앙)
    private static LevelDef Level1() => new(
        1, "첫 인력", "인력 발생기로 우주선을 포털까지 유도하세요!",
        ShipX: 90,  ShipY: 260,
        ShipVx: 60, ShipVy: 0,
        PortalX: 750, PortalY: 260, PortalRadius: 28,
        Slots: [ new(380, 310) ],
        Obstacles: []
    );

    // ── 레벨 2: 장애물 우회 ──────────────────────────────
    // 중앙 장애물 → 위 또는 아래로 우회해야 함
    // 정답: 위 Attract + 아래 Repel (또는 반대)
    private static LevelDef Level2() => new(
        2, "장애물 우회", "장애물을 피해 포털로! 인력과 척력을 조합하세요.",
        ShipX: 90,  ShipY: 260,
        ShipVx: 70, ShipVy: 0,
        PortalX: 750, PortalY: 180, PortalRadius: 28,
        Slots: [ new(350, 155), new(350, 340) ],
        Obstacles: [ new(430, 255, 55) ]
    );

    // ── 레벨 3: 소용돌이 ─────────────────────────────────
    // 포털이 시작 위치와 같은 쪽 → 소용돌이로 방향 전환
    // 정답: Vortex + Attract 조합
    private static LevelDef Level3() => new(
        3, "소용돌이", "소용돌이 발생기로 궤도를 휘어라!",
        ShipX: 100, ShipY: 400,
        ShipVx: 80, ShipVy: -30,
        PortalX: 700, PortalY: 130, PortalRadius: 30,
        Slots: [ new(380, 260), new(560, 320) ],
        Obstacles: []
    );

    // ── 레벨 4: 복합 ─────────────────────────────────────
    // 3개 슬롯, 좁은 통로 통과
    // 정답: Attract + Repel + Vortex 조합
    private static LevelDef Level4() => new(
        4, "복합 기동", "세 슬롯을 모두 활용해 복잡한 경로를 설계하라!",
        ShipX: 90,  ShipY: 130,
        ShipVx: 50, ShipVy: 40,
        PortalX: 740, PortalY: 380, PortalRadius: 26,
        Slots: [ new(280, 220), new(460, 180), new(580, 350) ],
        Obstacles: [ new(370, 310, 45), new(520, 240, 40) ]
    );

    // ── 레벨 5: 정밀 궤도 ───────────────────────────────
    // 4개 슬롯, 여러 장애물 사이 통과
    private static LevelDef Level5() => new(
        5, "정밀 궤도", "모든 발생기를 정밀하게 조합해 최종 궤도를 완성하라!",
        ShipX: 90,  ShipY: 260,
        ShipVx: 55, ShipVy: 30,
        PortalX: 740, PortalY: 260, PortalRadius: 24,
        Slots: [ new(250, 160), new(420, 340), new(580, 150), new(650, 310) ],
        Obstacles: [ new(340, 255, 40), new(510, 260, 38), new(470, 160, 35) ]
    );
}
