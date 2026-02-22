namespace TowerGuard.Entities;

public enum TowerType
{
    Arrow,
    Cannon,
    Ice,
    Lightning
}

public sealed class Tower
{
    public TowerType Type { get; }
    public int GridX { get; }
    public int GridY { get; }
    public int Level { get; private set; } = 1;
    public const int MaxLevel = 3;

    public double Range { get; private set; }
    public double Damage { get; private set; }
    public double FireRate { get; private set; }
    public int Cost { get; }

    public double FireCooldown { get; set; }
    public string ColorHex { get; }

    // For Lightning chain count
    public int ChainCount { get; private set; }

    // For Ice slow
    public double SlowFactor { get; private set; }
    public double SlowDuration { get; private set; }

    // For Cannon splash radius (in tiles)
    public double SplashRadius { get; private set; }

    public Tower(TowerType type, int gridX, int gridY)
    {
        Type = type;
        GridX = gridX;
        GridY = gridY;

        (Cost, ColorHex) = type switch
        {
            TowerType.Arrow     => (50, "#2ECC71"),
            TowerType.Cannon    => (100, "#E74C3C"),
            TowerType.Ice       => (75, "#3A86FF"),
            TowerType.Lightning => (150, "#FFD700"),
            _ => (50, "#2ECC71")
        };

        ApplyLevelStats();
    }

    private void ApplyLevelStats()
    {
        (Range, Damage, FireRate, ChainCount, SlowFactor, SlowDuration, SplashRadius) = Type switch
        {
            TowerType.Arrow => Level switch
            {
                1 => (3.0 * 32, 15.0, 0.5, 0, 1.0, 0.0, 0.0),
                2 => (3.5 * 32, 22.0, 0.4, 0, 1.0, 0.0, 0.0),
                _ => (4.0 * 32, 30.0, 0.3, 0, 1.0, 0.0, 0.0)
            },
            TowerType.Cannon => Level switch
            {
                1 => (2.5 * 32, 40.0, 1.2, 0, 1.0, 0.0, 1.0),
                2 => (3.0 * 32, 60.0, 1.0, 0, 1.0, 0.0, 1.3),
                _ => (3.5 * 32, 85.0, 0.8, 0, 1.0, 0.0, 1.6)
            },
            TowerType.Ice => Level switch
            {
                1 => (3.0 * 32, 8.0,  0.8, 0, 0.5, 1.5, 0.0),
                2 => (3.5 * 32, 12.0, 0.7, 0, 0.4, 2.0, 0.0),
                _ => (4.0 * 32, 18.0, 0.6, 0, 0.3, 2.5, 0.0)
            },
            TowerType.Lightning => Level switch
            {
                1 => (4.0 * 32, 25.0, 1.0, 3, 1.0, 0.0, 0.0),
                2 => (4.5 * 32, 38.0, 0.85, 4, 1.0, 0.0, 0.0),
                _ => (5.0 * 32, 55.0, 0.7, 5, 1.0, 0.0, 0.0)
            },
            _ => (3.0 * 32, 15.0, 0.5, 0, 1.0, 0.0, 0.0)
        };
    }

    public int UpgradeCost => Level switch
    {
        1 => (int)(Cost * 0.75),
        2 => (int)(Cost * 1.25),
        _ => 0
    };

    public int SellValue => (int)(Cost * 0.6) + (Level > 1 ? (int)(UpgradeCostForLevel(1) * 0.4) : 0)
                            + (Level > 2 ? (int)(UpgradeCostForLevel(2) * 0.4) : 0);

    private int UpgradeCostForLevel(int lvl) => lvl switch
    {
        1 => (int)(Cost * 0.75),
        2 => (int)(Cost * 1.25),
        _ => 0
    };

    public bool CanUpgrade => Level < MaxLevel;

    public bool Upgrade()
    {
        if (!CanUpgrade) return false;
        Level++;
        ApplyLevelStats();
        return true;
    }

    public double CenterX => GridX * 32.0 + 16.0;
    public double CenterY => GridY * 32.0 + 16.0;

    public static string GetDisplayName(TowerType type) => type switch
    {
        TowerType.Arrow => "Arrow",
        TowerType.Cannon => "Cannon",
        TowerType.Ice => "Ice",
        TowerType.Lightning => "Lightning",
        _ => "Unknown"
    };

    public static int GetCost(TowerType type) => type switch
    {
        TowerType.Arrow => 50,
        TowerType.Cannon => 100,
        TowerType.Ice => 75,
        TowerType.Lightning => 150,
        _ => 50
    };
}
