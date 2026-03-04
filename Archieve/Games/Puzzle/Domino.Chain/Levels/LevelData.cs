using DominoChain.Entities;

namespace DominoChain.Levels;

public record LevelDef(
    int    Number,
    string Title,
    string Hint,
    int    SlotCount,       // 사용 가능 도미노 수 (빈 슬롯 개수)
    IReadOnlyList<DominoEntry> Entries,
    Target Target
);

public record DominoEntry(
    double PivotX,
    double PivotY,
    DominoKind Kind,   // Fixed or Placed(슬롯)
    int SlotIndex = -1 // Placed인 경우 슬롯 번호
);

public static class LevelData
{
    private const double FloorY   = 390.0; // 바닥 Y (도미노 하단)
    private const double DomW     = 14.0;
    private const double DomH     = 64.0;
    private const double Spacing  = 42.0;  // 기본 도미노 간격 (H×0.65 → 쓰러지면 다음 도미노 타격)

    public static LevelDef Get(int level) => level switch
    {
        1 => Level1(),
        2 => Level2(),
        3 => Level3(),
        4 => Level4(),
        5 => Level5(),
        _ => Level1()
    };

    public const int MaxLevel = 5;

    // ── 레벨 1: 첫 걸음 ───────────────────────────────────
    // 직선 5개 배치, 빈 슬롯 1개
    // [D][D][D][ ][D][D]  → 초(candle)
    private static LevelDef Level1()
    {
        double startX = 140;
        var entries = new List<DominoEntry>();

        // 고정 도미노 3개
        for (int i = 0; i < 3; i++)
            entries.Add(new(startX + i * Spacing, FloorY, DominoKind.Fixed));

        // 슬롯 1개 (빈 공간)
        entries.Add(new(startX + 3 * Spacing, FloorY, DominoKind.Placed, 0));

        // 고정 도미노 2개
        for (int i = 4; i < 6; i++)
            entries.Add(new(startX + i * Spacing, FloorY, DominoKind.Fixed));

        return new LevelDef(
            1, "첫 걸음", "빈 공간에 도미노를 배치해 초를 쓰러뜨려라!",
            1, entries,
            new Target { X = startX + 6 * Spacing + 20, Y = FloorY, W = 18, H = 34, Kind = TargetKind.Candle }
        );
    }

    // ── 레벨 2: 두 번의 틈 ────────────────────────────────
    // [D][D][ ][D][D][ ][D]  → 버튼
    private static LevelDef Level2()
    {
        double startX = 100;
        var entries = new List<DominoEntry>();
        int slotIdx = 0;

        double[] xs = [ 0, 1, 2.5, 3.5, 5, 6, 7.5 ];
        bool[]  isSlot = [ false, false, true, false, false, true, false ];

        for (int i = 0; i < xs.Length; i++)
        {
            double px = startX + xs[i] * Spacing;
            if (isSlot[i])
                entries.Add(new(px, FloorY, DominoKind.Placed, slotIdx++));
            else
                entries.Add(new(px, FloorY, DominoKind.Fixed));
        }

        return new LevelDef(
            2, "두 번의 틈", "두 군데 빈 공간을 채워라!",
            2, entries,
            new Target { X = startX + 8.8 * Spacing, Y = FloorY, W = 24, H = 20, Kind = TargetKind.Button }
        );
    }

    // ── 레벨 3: 엇박자 ────────────────────────────────────
    // 간격이 더 넓어 도미노가 적게 쓰러질 위험 — 배치 위치가 중요
    // [D][D][D][ ][ ][D][D]  → 공
    private static LevelDef Level3()
    {
        double startX = 90;
        var entries = new List<DominoEntry>();

        // 고정 3개
        for (int i = 0; i < 3; i++)
            entries.Add(new(startX + i * Spacing, FloorY, DominoKind.Fixed));

        // 넓은 빈 공간 (슬롯 2개, 간격 1.2배)
        double wideSpacing = Spacing * 1.15;
        entries.Add(new(startX + 3 * Spacing,               FloorY, DominoKind.Placed, 0));
        entries.Add(new(startX + 3 * Spacing + wideSpacing, FloorY, DominoKind.Placed, 1));

        // 고정 2개
        entries.Add(new(startX + 3 * Spacing + wideSpacing * 2,     FloorY, DominoKind.Fixed));
        entries.Add(new(startX + 3 * Spacing + wideSpacing * 2 + Spacing, FloorY, DominoKind.Fixed));

        return new LevelDef(
            3, "엇박자", "간격에 주의! 위치를 잘 골라 배치하라.",
            2, entries,
            new Target { X = startX + 3 * Spacing + wideSpacing * 2 + Spacing * 2 + 20, Y = FloorY, W = 22, H = 22, Kind = TargetKind.Ball }
        );
    }

    // ── 레벨 4: 대형 연쇄 ────────────────────────────────
    // [D][ ][D][D][ ][D][ ][D]  → 초
    private static LevelDef Level4()
    {
        double startX = 80;
        var entries = new List<DominoEntry>();
        int slotIdx = 0;

        bool[] isSlot = [ false, true, false, false, true, false, true, false ];

        for (int i = 0; i < isSlot.Length; i++)
        {
            double px = startX + i * Spacing;
            if (isSlot[i])
                entries.Add(new(px, FloorY, DominoKind.Placed, slotIdx++));
            else
                entries.Add(new(px, FloorY, DominoKind.Fixed));
        }

        return new LevelDef(
            4, "대형 연쇄", "세 군데 빈 공간 모두 채워야 완성!",
            3, entries,
            new Target { X = startX + isSlot.Length * Spacing + 18, Y = FloorY, W = 18, H = 34, Kind = TargetKind.Candle }
        );
    }

    // ── 레벨 5: 마스터 도미노 ─────────────────────────────
    // 두 그룹이 나뉘어 있어 중앙 연결을 맞춰야 함
    // [D][D][ ][ ][D][D][D][ ][D]  → 버튼
    private static LevelDef Level5()
    {
        double startX = 70;
        var entries = new List<DominoEntry>();
        int slotIdx = 0;

        double sp2 = Spacing * 1.1;
        (double x, bool slot)[] layout =
        [
            (0,     false), (1,     false),
            (2.2,   true),  (3.4,   true),
            (4.6,   false), (5.6,   false), (6.6,   false),
            (7.8,   true),
            (9.0,   false)
        ];

        foreach (var (xf, slot) in layout)
        {
            double px = startX + xf * sp2;
            if (slot)
                entries.Add(new(px, FloorY, DominoKind.Placed, slotIdx++));
            else
                entries.Add(new(px, FloorY, DominoKind.Fixed));
        }

        return new LevelDef(
            5, "마스터 도미노", "최대 난이도! 모든 빈 공간을 완벽하게 채워라.",
            3, entries,
            new Target { X = startX + 10.2 * sp2, Y = FloorY, W = 24, H = 20, Kind = TargetKind.Button }
        );
    }
}
