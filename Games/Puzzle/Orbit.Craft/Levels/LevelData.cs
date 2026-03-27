namespace OrbitCraft.Levels;

/// <summary>행성 정의.</summary>
public record PlanetDef(
    double X, double Y, double Mass, double Radius,
    bool IsTarget, string Name);

/// <summary>레벨 정의.</summary>
public record LevelDef(
    int          Number,
    string       Name,
    string       Hint,
    double       ProbeX,   double ProbeY,
    double       DefaultVx, double DefaultVy,
    PlanetDef[]  Planets,
    int          TargetPlanetIdx,
    int          RequiredRevolutions
);

/// <summary>
/// 물리 상수 기준 (G = 3800)
///   원형궤도 속도: v = √(G·M/r)
///   탈출 속도:     v_esc = √(2·G·M/r) = √2 · v_circ
/// </summary>
public static class LevelData
{
    public const int MaxLevel = 10;

    public static LevelDef Get(int n) => n switch
    {
        1  => Level1(),  2 => Level2(),  3 => Level3(),
        4  => Level4(),  5 => Level5(),  6 => Level6(),
        7  => Level7(),  8 => Level8(),  9 => Level9(),
        10 => Level10(),
        _  => Level1()
    };

    // ── 레벨 1: 단순 원형 궤도 ──────────────────────────────
    // 별(M=120) 중앙, 탐사선 상단 r=143 → v_circ≈56.3 px/s
    private static LevelDef Level1() => new(
        1, "첫 궤도",
        "마우스를 움직여 발사 방향·속도를 설정하고 SPACE로 발사!",
        ProbeX: 422, ProbeY: 117,
        DefaultVx: 56, DefaultVy: 0,
        Planets: [
            new(422, 260, Mass: 120, Radius: 22, IsTarget: true, "별")
        ],
        TargetPlanetIdx: 0,
        RequiredRevolutions: 2
    );

    // ── 레벨 2: 이중성계 ────────────────────────────────────
    // 쌍성 A(M=100) 주위 2회 공전 — 쌍성 B의 중력 조심
    private static LevelDef Level2() => new(
        2, "이중성계",
        "쌍성 중 하나를 골라 궤도 진입! 반대 별의 중력을 조심하세요.",
        ProbeX: 250, ProbeY: 165,
        DefaultVx: 65, DefaultVy: 0,
        Planets: [
            new(250, 260, Mass: 100, Radius: 20, IsTarget: true,  "쌍성 A"),
            new(570, 260, Mass: 100, Radius: 20, IsTarget: false, "쌍성 B")
        ],
        TargetPlanetIdx: 0,
        RequiredRevolutions: 2
    );

    // ── 레벨 3: 항성계 행성 포획 ────────────────────────────
    // 대형성(M=180) 좌측 + 작은 행성(M=60, 목표) 우측
    private static LevelDef Level3() => new(
        3, "행성 포획",
        "거대 항성의 중력장 속에서 작은 행성에 궤도 진입!",
        ProbeX: 610, ProbeY: 196,
        DefaultVx: 61, DefaultVy: 0,
        Planets: [
            new(250, 260, Mass: 180, Radius: 26, IsTarget: false, "모성"),
            new(610, 260, Mass: 60,  Radius: 16, IsTarget: true,  "행성")
        ],
        TargetPlanetIdx: 1,
        RequiredRevolutions: 2
    );

    // ── 레벨 4: 거대 타원 궤도 ──────────────────────────────
    // 단일 대형성(M=220) — 높은 이심률 타원 궤도 3회 공전
    private static LevelDef Level4() => new(
        4, "타원 궤도",
        "이심률이 큰 타원 궤도! 케플러 제2법칙: 근일점에서 더 빠르게.",
        ProbeX: 422, ProbeY: 100,
        DefaultVx: 78, DefaultVy: 0,
        Planets: [
            new(422, 260, Mass: 220, Radius: 32, IsTarget: true, "거성")
        ],
        TargetPlanetIdx: 0,
        RequiredRevolutions: 3
    );

    // ── 레벨 5: 3체 카오스 ──────────────────────────────────
    // 삼각 배치 3개 항성, 성 C(목표) 주위 1회 공전
    private static LevelDef Level5() => new(
        5, "3체 카오스",
        "세 항성의 복합 중력장! 안정 궤도를 정밀하게 설계하라.",
        ProbeX: 420, ProbeY: 455,
        DefaultVx: -68, DefaultVy: 0,
        Planets: [
            new(240, 190, Mass: 85,  Radius: 20, IsTarget: false, "성 A"),
            new(600, 190, Mass: 85,  Radius: 20, IsTarget: false, "성 B"),
            new(420, 380, Mass: 110, Radius: 24, IsTarget: true,  "성 C")
        ],
        TargetPlanetIdx: 2,
        RequiredRevolutions: 1
    );

    // ── 레벨 6: 달 포획 ─────────────────────────────────────
    // 모행성(M=180) + 달(M=40, 목표) — 달 주위 2회 공전
    // 달까지 r=65: v_circ ≈ √(3800·40/65) ≈ 48.4 → 50
    private static LevelDef Level6() => new(
        6, "달 포획",
        "모행성 중력 너머 작은 달에 진입! 달의 Hill sphere 안에서 안정 궤도를.",
        ProbeX: 480, ProbeY: 195,
        DefaultVx: 50, DefaultVy: 0,
        Planets: [
            new(280, 260, Mass: 180, Radius: 26, IsTarget: false, "모행성"),
            new(480, 260, Mass: 40,  Radius: 13, IsTarget: true,  "달")
        ],
        TargetPlanetIdx: 1,
        RequiredRevolutions: 2
    );

    // ── 레벨 7: 역행 궤도 ───────────────────────────────────
    // 단일 별(M=140) — 반시계(역행) 방향 3회 공전
    // r=145: v_circ ≈ √(3800·140/145) ≈ 60.5 → -62 (왼쪽)
    private static LevelDef Level7() => new(
        7, "역행 궤도",
        "이번엔 반대 방향! 왼쪽(반시계)으로 발사해 역행 궤도를 구축하라.",
        ProbeX: 422, ProbeY: 405,
        DefaultVx: -62, DefaultVy: 0,
        Planets: [
            new(422, 260, Mass: 140, Radius: 24, IsTarget: true, "별")
        ],
        TargetPlanetIdx: 0,
        RequiredRevolutions: 3
    );

    // ── 레벨 8: 중력 방패 ───────────────────────────────────
    // 거대성(M=200) 왼쪽, 목표 행성(M=60) 오른쪽 — 거대성 중력 뚫고 진입
    // 목표 r=67: v_circ ≈ √(3800·60/67) ≈ 58.3 → 60 (위쪽서 오른쪽으로)
    private static LevelDef Level8() => new(
        8, "중력 방패",
        "거대성이 진로를 방해한다! 섭동을 계산해 목표 행성에 정확히 진입하라.",
        ProbeX: 600, ProbeY: 193,
        DefaultVx: 60, DefaultVy: 0,
        Planets: [
            new(180, 260, Mass: 200, Radius: 28, IsTarget: false, "거대성"),
            new(600, 260, Mass: 60,  Radius: 16, IsTarget: true,  "표적성")
        ],
        TargetPlanetIdx: 1,
        RequiredRevolutions: 2
    );

    // ── 레벨 9: 4중 섭동 ────────────────────────────────────
    // 중앙 항성(M=180, 목표) + 주변 3행성(M=60씩) — 항성 3회 공전
    // r=152: v_circ(항성만) ≈ √(3800·180/152) ≈ 67.1 → 68
    private static LevelDef Level9() => new(
        9, "4중 섭동",
        "세 행성의 중력이 복잡하게 얽힌다! 중앙 항성 주위를 정밀하게 돌아라.",
        ProbeX: 422, ProbeY: 108,
        DefaultVx: 68, DefaultVy: 0,
        Planets: [
            new(422, 260, Mass: 180, Radius: 26, IsTarget: true,  "항성"),
            new(200, 140, Mass: 60,  Radius: 14, IsTarget: false, "행성 α"),
            new(644, 380, Mass: 60,  Radius: 14, IsTarget: false, "행성 β"),
            new(200, 400, Mass: 60,  Radius: 14, IsTarget: false, "행성 γ")
        ],
        TargetPlanetIdx: 0,
        RequiredRevolutions: 3
    );

    // ── 레벨 10: 그랜드 피날레 ──────────────────────────────
    // 이중 항성(M=120×2) + 위성 목표(M=80) — 이중성 중력 사이에서 안정 진입
    // 목표 r=62: v_circ(목표만) ≈ √(3800·80/62) ≈ 70 → 74 (이중성 섭동 보정)
    private static LevelDef Level10() => new(
        10, "그랜드 피날레",
        "이중 항성 사이에 걸린 목표! 복합 중력장을 뚫고 최종 궤도를 달성하라.",
        ProbeX: 422, ProbeY: 78,
        DefaultVx: 74, DefaultVy: 0,
        Planets: [
            new(300, 260, Mass: 120, Radius: 20, IsTarget: false, "항성 Ⅰ"),
            new(544, 260, Mass: 120, Radius: 20, IsTarget: false, "항성 Ⅱ"),
            new(422, 140, Mass: 80,  Radius: 18, IsTarget: true,  "목표성"),
            new(160, 100, Mass: 45,  Radius: 12, IsTarget: false, "소행성 A"),
            new(684, 420, Mass: 45,  Radius: 12, IsTarget: false, "소행성 B")
        ],
        TargetPlanetIdx: 2,
        RequiredRevolutions: 2
    );
}
