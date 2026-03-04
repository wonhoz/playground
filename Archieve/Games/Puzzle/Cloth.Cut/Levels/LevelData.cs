using ClothCut.Entities;

namespace ClothCut.Levels;

public record LevelDef(
    int           Number,
    string        Name,
    string        Hint,
    int           Cols,
    int           Rows,
    double        Spacing,
    int[]         PinnedCols,   // 상단 행에서 고정할 열 인덱스
    double        StartX,
    double        StartY,
    double        Stiffness,    // 1.0=뻣뻣, 0.5=탄성
    string        MaterialName,
    Scale[]       Scales
);

public static class LevelData
{
    public const int MaxLevel = 5;

    private const double ViewW   = 844;
    private const double ScaleY  = 490;  // 저울 바닥 Y

    public static LevelDef Get(int level) => level switch
    {
        1 => Level1(),
        2 => Level2(),
        3 => Level3(),
        4 => Level4(),
        5 => Level5(),
        _ => Level1()
    };

    // ── 레벨 1: 첫 절단 ───────────────────────────────────
    // 12×8 격자, 핀 4개, 저울 2개 — 세로로 한 번 절단
    private static LevelDef Level1()
    {
        int cols = 12, rows = 8;
        double spacing = 36;
        double startX  = (ViewW - (cols - 1) * spacing) * 0.5;
        double startY  = 60;

        return new LevelDef(
            1, "첫 절단", "천의 가운데를 세로로 잘라 두 저울에 균등하게!",
            cols, rows, spacing,
            PinnedCols: [1, 4, 7, 10],
            startX, startY,
            Stiffness: 1.0,
            MaterialName: "면 천",
            Scales:
            [
                new Scale { CenterX = ViewW * 0.27, BaseY = ScaleY, Width = 260, MinRatio = 0.35, MaxRatio = 0.65, Label = "좌" },
                new Scale { CenterX = ViewW * 0.73, BaseY = ScaleY, Width = 260, MinRatio = 0.35, MaxRatio = 0.65, Label = "우" }
            ]
        );
    }

    // ── 레벨 2: 두 번 절단 ────────────────────────────────
    // 더 큰 격자, 저울 2개, 비율이 더 좁음 (40~60%)
    private static LevelDef Level2()
    {
        int cols = 16, rows = 9;
        double spacing = 30;
        double startX  = (ViewW - (cols - 1) * spacing) * 0.5;
        double startY  = 55;

        return new LevelDef(
            2, "두 번 절단", "두 번 잘라 왼쪽에 40%, 오른쪽에 60% 맞춰라!",
            cols, rows, spacing,
            PinnedCols: [2, 6, 9, 13],
            startX, startY,
            Stiffness: 1.0,
            MaterialName: "면 천",
            Scales:
            [
                new Scale { CenterX = ViewW * 0.27, BaseY = ScaleY, Width = 240, MinRatio = 0.30, MaxRatio = 0.50, Label = "좌 40%" },
                new Scale { CenterX = ViewW * 0.73, BaseY = ScaleY, Width = 240, MinRatio = 0.50, MaxRatio = 0.70, Label = "우 60%" }
            ]
        );
    }

    // ── 레벨 3: 탄성 천 ───────────────────────────────────
    // Stiffness 낮음 → 더 많이 처짐, 절단 위치가 달라 보임
    private static LevelDef Level3()
    {
        int cols = 12, rows = 8;
        double spacing = 36;
        double startX  = (ViewW - (cols - 1) * spacing) * 0.5;
        double startY  = 50;

        return new LevelDef(
            3, "탄성 천", "탄성 천은 더 많이 늘어납니다! 신중하게 절단하세요.",
            cols, rows, spacing,
            PinnedCols: [1, 5, 6, 10],
            startX, startY,
            Stiffness: 0.55,
            MaterialName: "탄성 직물",
            Scales:
            [
                new Scale { CenterX = ViewW * 0.27, BaseY = ScaleY, Width = 260, MinRatio = 0.38, MaxRatio = 0.62, Label = "좌" },
                new Scale { CenterX = ViewW * 0.73, BaseY = ScaleY, Width = 260, MinRatio = 0.38, MaxRatio = 0.62, Label = "우" }
            ]
        );
    }

    // ── 레벨 4: 세 조각 ───────────────────────────────────
    // 큰 격자, 저울 3개, 각 ~33%
    private static LevelDef Level4()
    {
        int cols = 18, rows = 9;
        double spacing = 28;
        double startX  = (ViewW - (cols - 1) * spacing) * 0.5;
        double startY  = 50;

        return new LevelDef(
            4, "세 조각", "두 번 절단해 세 저울에 각각 33% 가까이 맞춰라!",
            cols, rows, spacing,
            PinnedCols: [2, 6, 11, 15],
            startX, startY,
            Stiffness: 0.95,
            MaterialName: "캔버스",
            Scales:
            [
                new Scale { CenterX = ViewW * 0.18, BaseY = ScaleY, Width = 200, MinRatio = 0.25, MaxRatio = 0.42, Label = "좌 33%" },
                new Scale { CenterX = ViewW * 0.50, BaseY = ScaleY, Width = 200, MinRatio = 0.25, MaxRatio = 0.42, Label = "중 33%" },
                new Scale { CenterX = ViewW * 0.82, BaseY = ScaleY, Width = 200, MinRatio = 0.25, MaxRatio = 0.42, Label = "우 33%" }
            ]
        );
    }

    // ── 레벨 5: 마스터 재단사 ─────────────────────────────
    // 큰 격자, 저울 3개, 정밀 비율 (25% / 50% / 25%)
    private static LevelDef Level5()
    {
        int cols = 16, rows = 10;
        double spacing = 32;
        double startX  = (ViewW - (cols - 1) * spacing) * 0.5;
        double startY  = 48;

        return new LevelDef(
            5, "마스터 재단사", "정밀 절단! 좌 25% — 중 50% — 우 25%를 맞춰라.",
            cols, rows, spacing,
            PinnedCols: [2, 5, 10, 13],
            startX, startY,
            Stiffness: 0.85,
            MaterialName: "혼방 직물",
            Scales:
            [
                new Scale { CenterX = ViewW * 0.18, BaseY = ScaleY, Width = 190, MinRatio = 0.18, MaxRatio = 0.33, Label = "좌 25%" },
                new Scale { CenterX = ViewW * 0.50, BaseY = ScaleY, Width = 240, MinRatio = 0.42, MaxRatio = 0.60, Label = "중 50%" },
                new Scale { CenterX = ViewW * 0.82, BaseY = ScaleY, Width = 190, MinRatio = 0.18, MaxRatio = 0.33, Label = "우 25%" }
            ]
        );
    }
}
