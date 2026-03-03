using GearWorks.Entities;

namespace GearWorks.Levels;

/// <summary>레벨에서 고정 기어(Motor / Fixed / Output) 정의.</summary>
public record FixedGearDef(double X, double Y, double Radius, GearRole Role, double MotorRpm = 30);

/// <summary>플레이어가 기어를 배치할 슬롯 정의.</summary>
public record SlotDef(double X, double Y);

public record LevelDef(
    int          Number,
    string       Name,
    string       Hint,
    FixedGearDef[] FixedGears,
    SlotDef[]    Slots,
    int          TargetSign   // +1=CW  -1=CCW  0=방향무관
);

public static class LevelData
{
    public const int MaxLevel = 5;

    // ─────────────────────────────────────────────────────
    // 기어 크기 참조: Small=24, Medium=36, Large=52
    // 외접 거리: r₁+r₂  (허용 오차 ±6)
    // 방향 규칙: 외접 1회마다 반전
    //   모터 CCW → 1단 CW → 2단 CCW → 3단 CW → 4단 CCW ...
    // ─────────────────────────────────────────────────────

    public static LevelDef Get(int n) => n switch
    {
        1 => Level1(), 2 => Level2(), 3 => Level3(),
        4 => Level4(), 5 => Level5(),
        _ => Level1()
    };

    // ── 레벨 1: 첫 연결 ──────────────────────────────────
    // Motor(M36) → [Slot] → Output(M36)   직선, 거리 72
    // 정답: Medium(36)   목표: 방향 무관 (연결만 되면 됨)
    private static LevelDef Level1() => new(
        1, "첫 연결",
        "슬롯을 클릭해 알맞은 크기의 기어를 배치하세요!",
        [
            new(150, 280, 36, GearRole.Motor,  MotorRpm: 30),
            new(294, 280, 36, GearRole.Output)
        ],
        [ new(222, 280) ],
        TargetSign: 0   // 방향 무관
    );

    // ── 레벨 2: 방향 퍼즐 ────────────────────────────────
    // Motor(M36) → [S1] → [S2] → Output(M36)
    // 3번 외접 → CW (모터 CCW 기준)
    // 정답: S1=Medium, S2=Medium   목표: CW (+1)
    private static LevelDef Level2() => new(
        2, "방향 퍼즐",
        "슬롯 두 개를 채워 출력 기어를 CW(시계 방향)로 돌려라!",
        [
            new(120, 280, 36, GearRole.Motor,  MotorRpm: 30),
            new(336, 280, 36, GearRole.Output)
        ],
        [ new(192, 280), new(264, 280) ],
        TargetSign: +1  // CW
    );

    // ── 레벨 3: L자 경로 ──────────────────────────────────
    // Motor → Slot(오른쪽) → Slot(아래) → Output(왼쪽아래)
    // 3번 외접 → CW   목표: CW
    private static LevelDef Level3() => new(
        3, "L자 경로",
        "방향을 꺾어라! 기어를 두 슬롯에 모두 배치하세요.",
        [
            new(150, 222, 36, GearRole.Motor,  MotorRpm: 30),
            new(150, 294, 36, GearRole.Output)
        ],
        [ new(222, 222), new(222, 294) ],
        TargetSign: +1  // CW
    );

    // ── 레벨 4: 크기 퍼즐 ────────────────────────────────
    // Motor(M36) → [Slot] → Output(M36)
    // 거리=88 → Large(52) 만 연결 가능  (Medium 72≠88, Small 60≠88)
    // 2번 외접 → CCW (모터 CCW → CW → CCW)   목표: CCW
    private static LevelDef Level4() => new(
        4, "크기 선택",
        "어떤 크기의 기어가 이 간격에 맞을까? 잘 생각해라!",
        [
            new(160, 280, 36, GearRole.Motor,  MotorRpm: 30),
            new(336, 280, 36, GearRole.Output)
        ],
        [ new(248, 280) ],   // 거리 88 = 36+52 → Large만 답
        TargetSign: -1   // CCW
    );

    // ── 레벨 5: 복합 ㄷ자 ────────────────────────────────
    // Motor → S1 → S2 → S3 → Output
    // 4번 외접 → CCW   목표: CCW
    private static LevelDef Level5() => new(
        5, "복합 기어열",
        "세 슬롯을 모두 채워 기계를 완성하라! 최종 방향은 CCW.",
        [
            new(100, 222, 36, GearRole.Motor,  MotorRpm: 30),
            new(172, 294, 36, GearRole.Output)
        ],
        [ new(172, 222), new(244, 222), new(244, 294) ],
        TargetSign: -1   // CCW
    );
}
