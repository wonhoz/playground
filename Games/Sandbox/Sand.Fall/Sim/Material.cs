namespace SandFall.Sim;

public enum Material : byte
{
    Empty   = 0,
    Sand    = 1,
    Water   = 2,
    Fire    = 3,
    Oil     = 4,
    Steam   = 5,
    Stone   = 6,
    Wood    = 7,
    Ice     = 8,
    Seed    = 9,
    Plant   = 10,
    Acid       = 11,
    Ash        = 12,
    Gunpowder  = 13,
}

/// <summary>물질 분류</summary>
public enum MatCategory { Empty, Powder, Liquid, Gas, Solid, Special }

public static class MaterialDef
{
    // ── 색상 (BGRA32 팩) ─────────────────────────────────────────────
    // 물은 프레임마다 약간 변화, 불은 별도 랜덤 색
    public static readonly uint[] BaseColor = new uint[(int)Material.Gunpowder + 1]
    {
        0xFF0D1117,   // Empty      - 다크 배경
        0xFFE8C878,   // Sand       - 황토
        0xFF3468C8,   // Water      - 파란색
        0xFFFF5500,   // Fire       - 오렌지 (기본; 실제 렌더 시 랜덤)
        0xFF6A4820,   // Oil        - 어두운 갈색
        0xFF9AAAC0,   // Steam      - 회청색
        0xFF788090,   // Stone      - 회색
        0xFF8B5E3C,   // Wood       - 갈색
        0xFFA8D8F0,   // Ice        - 밝은 하늘색
        0xFFAA7040,   // Seed       - 시에나
        0xFF22A030,   // Plant      - 초록
        0xFF88FF10,   // Acid       - 형광 연두
        0xFF504848,   // Ash        - 어두운 회색
        0xFF30302A,   // Gunpowder  - 어두운 회갈색
    };

    public static readonly MatCategory[] Category = new MatCategory[(int)Material.Gunpowder + 1]
    {
        MatCategory.Empty,   // Empty
        MatCategory.Powder,  // Sand
        MatCategory.Liquid,  // Water
        MatCategory.Special, // Fire
        MatCategory.Liquid,  // Oil (moves like liquid but less fluid)
        MatCategory.Gas,     // Steam
        MatCategory.Solid,   // Stone
        MatCategory.Solid,   // Wood
        MatCategory.Solid,   // Ice
        MatCategory.Powder,  // Seed
        MatCategory.Solid,   // Plant
        MatCategory.Liquid,  // Acid
        MatCategory.Powder,  // Ash
        MatCategory.Powder,  // Gunpowder
    };

    public static readonly string[] Names =
    [
        "빈 공간", "모래", "물", "불", "기름",
        "증기", "돌", "나무", "얼음", "씨앗",
        "식물", "산", "재", "화약",
    ];

    /// <summary>물질이 불에 타는가?</summary>
    public static bool IsFlammable(Material m) =>
        m is Material.Wood or Material.Oil or Material.Plant or Material.Seed or Material.Gunpowder;

    /// <summary>불이 통과할 수 있는가? (고체가 아닌가)</summary>
    public static bool CanDisplace(Material mover, Material target)
    {
        if (target == Material.Empty) return true;
        // 산 → 비-산 고체·액체 파괴
        if (mover == Material.Acid && target != Material.Acid) return true;
        return false;
    }
}
