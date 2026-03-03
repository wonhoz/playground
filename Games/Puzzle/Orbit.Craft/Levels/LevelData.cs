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
    double       DefaultVx, double DefaultVy,   // 기본 발사 속도 (마우스 조준 시작값)
    PlanetDef[]  Planets,
    int          TargetPlanetIdx,               // 공전 목표 행성 인덱스
    int          RequiredRevolutions            // 필요 공전 횟수
);

/// <summary>
/// 물리 상수 기준 (G = 3800)
///   원형궤도 속도: v = √(G·M/r)
///   탈출 속도:     v_esc = √(2·G·M/r) = √2 · v_circ
/// </summary>
public static class LevelData
{
    public const int MaxLevel = 5;

    public static LevelDef Get(int n) => n switch
    {
        1 => Level1(), 2 => Level2(), 3 => Level3(),
        4 => Level4(), 5 => Level5(),
        _ => Level1()
    };

    // ── 레벨 1: 단순 원형 궤도 ──────────────────────────────
    // 별(M=120) 중앙, 탐사선 상단 r=143 → v_circ≈56.3 px/s
    // 목표: 2회 공전
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
    // 두 쌍성(M=100씩) — 왼쪽 별 주위 2회 공전
    // 쌍성 분리 = 320px → Hill sphere ≈ 121px → r=95 ✓
    // v_circ(좌별만) ≈ 63, 우별 섭동 보정 → 65
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
    // 행성 Hill sphere ≈ 152px, 탐사선 r=64 → 안정 ✓
    // v_circ(행성만) ≈ 59.7, 모성 섭동 보정 → 61
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
    // 탐사선이 좌측 멀리서 시작 → 빠른 근일점 통과 필요
    // v_circ(r=160) ≈ 72.1, 타원용으로 약간 더 빠르게
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
    // 삼각 배치 3개 항성, 아래쪽 항성(목표) 주위 1회 공전
    // StarC(420,380) 기준 r=75 → v_circ ≈ 67-68, 하향 조정: -68
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
}
