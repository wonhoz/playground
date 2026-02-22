using GravityFlip.Entities;

namespace GravityFlip.Engine;

public sealed class Level
{
    public int Number { get; }
    public double ScrollSpeed { get; }
    public List<Platform> Platforms { get; }
    public List<Hazard> Hazards { get; }
    public List<Coin> Coins { get; }
    public double PortalX { get; }
    public double PortalY { get; }
    public double LevelWidth { get; }

    public const double FloorY = 460;
    public const double CeilingY = 0;
    public const double ViewHeight = 480;
    public const double PlatThick = 16;

    private Level(int number, double scrollSpeed, List<Platform> platforms,
        List<Hazard> hazards, List<Coin> coins, double portalX, double portalY, double levelWidth)
    {
        Number = number;
        ScrollSpeed = scrollSpeed;
        Platforms = platforms;
        Hazards = hazards;
        Coins = coins;
        PortalX = portalX;
        PortalY = portalY;
        LevelWidth = levelWidth;
    }

    public void ResetAll()
    {
        foreach (var p in Platforms) p.Reset();
        foreach (var c in Coins) c.Collected = false;
    }

    public static Level Create(int levelNumber)
    {
        return levelNumber switch
        {
            1 => BuildLevel1(),
            2 => BuildLevel2(),
            3 => BuildLevel3(),
            4 => BuildLevel4(),
            5 => BuildLevel5(),
            6 => BuildLevel6(),
            7 => BuildLevel7(),
            8 => BuildLevel8(),
            _ => BuildLevel1()
        };
    }

    // ── Helper methods ─────────────────────────────────

    private static Platform Floor(double x, double w) =>
        new(x, FloorY, w, PlatThick, PlatformType.Normal);

    private static Platform Ceiling(double x, double w) =>
        new(x, CeilingY, w, PlatThick, PlatformType.Normal);

    private static Platform Mid(double x, double y, double w, PlatformType type = PlatformType.Normal) =>
        new(x, y, w, PlatThick, type);

    private static Hazard SpikeUp(double x, double y, double w = 20) =>
        new(x, y, w, 12, HazardType.Spike, true);

    private static Hazard SpikeDown(double x, double y, double w = 20) =>
        new(x, y, w, 12, HazardType.Spike, false);

    private static Coin C(double x, double y) => new(x, y);

    // ── Level 1: Introduction ──────────────────────────

    private static Level BuildLevel1()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 3000;

        // Continuous floor and ceiling
        plats.Add(Floor(0, 800));
        plats.Add(Ceiling(0, 800));
        plats.Add(Floor(850, 600));
        plats.Add(Ceiling(850, 600));
        plats.Add(Floor(1500, 500));
        plats.Add(Ceiling(1500, 500));
        plats.Add(Floor(2050, 500));
        plats.Add(Ceiling(2050, 500));
        plats.Add(Floor(2600, 400));
        plats.Add(Ceiling(2600, 400));

        // Mid platforms to learn flipping
        plats.Add(Mid(600, 300, 120));
        plats.Add(Mid(1000, 160, 120));
        plats.Add(Mid(1400, 300, 100));
        plats.Add(Mid(1800, 160, 100));
        plats.Add(Mid(2200, 250, 120));

        // Coins to guide the player
        coins.Add(C(300, 420)); coins.Add(C(500, 420));
        coins.Add(C(650, 260)); coins.Add(C(1050, 120));
        coins.Add(C(1450, 260)); coins.Add(C(1850, 120));
        coins.Add(C(2250, 210)); coins.Add(C(2700, 420));

        return new Level(1, 120, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 2: First Spikes ──────────────────────────

    private static Level BuildLevel2()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 3500;

        plats.Add(Floor(0, 700));
        plats.Add(Ceiling(0, 700));
        plats.Add(Floor(750, 500));
        plats.Add(Ceiling(750, 500));
        plats.Add(Floor(1300, 600));
        plats.Add(Ceiling(1300, 600));
        plats.Add(Floor(1950, 500));
        plats.Add(Ceiling(1950, 500));
        plats.Add(Floor(2500, 500));
        plats.Add(Ceiling(2500, 500));
        plats.Add(Floor(3050, 450));
        plats.Add(Ceiling(3050, 450));

        // Spikes on floor
        hazards.Add(SpikeUp(400, FloorY - 12));
        hazards.Add(SpikeUp(420, FloorY - 12));
        hazards.Add(SpikeUp(1500, FloorY - 12));
        hazards.Add(SpikeUp(1520, FloorY - 12));
        hazards.Add(SpikeUp(1540, FloorY - 12));

        // Spikes on ceiling
        hazards.Add(SpikeDown(900, CeilingY + PlatThick));
        hazards.Add(SpikeDown(920, CeilingY + PlatThick));
        hazards.Add(SpikeDown(2600, CeilingY + PlatThick));

        // Mid platforms
        plats.Add(Mid(500, 280, 100));
        plats.Add(Mid(1100, 180, 100));
        plats.Add(Mid(1700, 300, 120));
        plats.Add(Mid(2300, 160, 100));

        // Coins
        coins.Add(C(250, 420)); coins.Add(C(550, 240));
        coins.Add(C(850, 60)); coins.Add(C(1150, 140));
        coins.Add(C(1750, 260)); coins.Add(C(2350, 120));
        coins.Add(C(2800, 420)); coins.Add(C(3100, 420));

        return new Level(2, 130, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 3: Moving Platforms ──────────────────────

    private static Level BuildLevel3()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 4000;

        plats.Add(Floor(0, 600));
        plats.Add(Ceiling(0, 600));
        plats.Add(Floor(700, 400));
        plats.Add(Ceiling(700, 400));
        plats.Add(Floor(1200, 500));
        plats.Add(Ceiling(1200, 500));
        plats.Add(Floor(1800, 400));
        plats.Add(Ceiling(1800, 400));
        plats.Add(Floor(2300, 500));
        plats.Add(Ceiling(2300, 500));
        plats.Add(Floor(2900, 400));
        plats.Add(Ceiling(2900, 400));
        plats.Add(Floor(3400, 600));
        plats.Add(Ceiling(3400, 600));

        // Moving platforms
        var mp1 = Mid(650, 250, 100, PlatformType.Moving);
        mp1.MoveStartY = 150; mp1.MoveEndY = 350; mp1.MoveSpeed = 60;
        plats.Add(mp1);

        var mp2 = Mid(1600, 200, 100, PlatformType.Moving);
        mp2.MoveStartY = 100; mp2.MoveEndY = 360; mp2.MoveSpeed = 80;
        plats.Add(mp2);

        var mp3 = Mid(2700, 300, 100, PlatformType.Moving);
        mp3.MoveStartY = 120; mp3.MoveEndY = 380; mp3.MoveSpeed = 70;
        plats.Add(mp3);

        // Spikes
        hazards.Add(SpikeUp(800, FloorY - 12));
        hazards.Add(SpikeUp(820, FloorY - 12));
        hazards.Add(SpikeDown(1400, CeilingY + PlatThick));
        hazards.Add(SpikeDown(1420, CeilingY + PlatThick));
        hazards.Add(SpikeUp(2000, FloorY - 12));
        hazards.Add(SpikeUp(2020, FloorY - 12));
        hazards.Add(SpikeUp(2040, FloorY - 12));
        hazards.Add(SpikeDown(3100, CeilingY + PlatThick));
        hazards.Add(SpikeDown(3120, CeilingY + PlatThick));

        // Coins
        coins.Add(C(300, 420)); coins.Add(C(700, 200));
        coins.Add(C(1000, 60)); coins.Add(C(1400, 300));
        coins.Add(C(1650, 160)); coins.Add(C(2100, 60));
        coins.Add(C(2500, 420)); coins.Add(C(2750, 260));
        coins.Add(C(3200, 420)); coins.Add(C(3600, 420));

        return new Level(3, 135, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 4: Crumbling Platforms ───────────────────

    private static Level BuildLevel4()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 4000;

        plats.Add(Floor(0, 500));
        plats.Add(Ceiling(0, 500));
        plats.Add(Floor(600, 300));
        plats.Add(Ceiling(600, 300));
        plats.Add(Floor(1000, 400));
        plats.Add(Ceiling(1000, 400));
        plats.Add(Floor(1500, 300));
        plats.Add(Ceiling(1500, 300));
        plats.Add(Floor(1900, 400));
        plats.Add(Ceiling(1900, 400));
        plats.Add(Floor(2400, 300));
        plats.Add(Ceiling(2400, 300));
        plats.Add(Floor(2800, 400));
        plats.Add(Ceiling(2800, 400));
        plats.Add(Floor(3300, 700));
        plats.Add(Ceiling(3300, 700));

        // Crumbling platforms
        plats.Add(Mid(500, 300, 80, PlatformType.Crumbling));
        plats.Add(Mid(900, 180, 80, PlatformType.Crumbling));
        plats.Add(Mid(1400, 350, 80, PlatformType.Crumbling));
        plats.Add(Mid(1850, 150, 80, PlatformType.Crumbling));
        plats.Add(Mid(2700, 280, 80, PlatformType.Crumbling));

        // Normal mid platforms
        plats.Add(Mid(1200, 250, 100));
        plats.Add(Mid(2100, 300, 100));
        plats.Add(Mid(3000, 200, 100));

        // Spikes
        hazards.Add(SpikeUp(700, FloorY - 12)); hazards.Add(SpikeUp(720, FloorY - 12));
        hazards.Add(SpikeDown(1100, CeilingY + PlatThick)); hazards.Add(SpikeDown(1120, CeilingY + PlatThick));
        hazards.Add(SpikeUp(1600, FloorY - 12)); hazards.Add(SpikeUp(1620, FloorY - 12));
        hazards.Add(SpikeDown(2000, CeilingY + PlatThick)); hazards.Add(SpikeDown(2020, CeilingY + PlatThick));
        hazards.Add(SpikeUp(2500, FloorY - 12)); hazards.Add(SpikeUp(2520, FloorY - 12));
        hazards.Add(SpikeUp(2540, FloorY - 12));
        hazards.Add(SpikeDown(3400, CeilingY + PlatThick));

        // Coins
        coins.Add(C(250, 420)); coins.Add(C(550, 260));
        coins.Add(C(950, 140)); coins.Add(C(1250, 210));
        coins.Add(C(1450, 310)); coins.Add(C(1900, 110));
        coins.Add(C(2150, 260)); coins.Add(C(2500, 60));
        coins.Add(C(2750, 240)); coins.Add(C(3050, 160));
        coins.Add(C(3500, 420));

        return new Level(4, 140, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 5: Bouncy Platforms ──────────────────────

    private static Level BuildLevel5()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 4500;

        plats.Add(Floor(0, 500));
        plats.Add(Ceiling(0, 500));
        plats.Add(Floor(600, 300));
        plats.Add(Ceiling(600, 300));
        plats.Add(Floor(1000, 500));
        plats.Add(Ceiling(1000, 500));
        plats.Add(Floor(1600, 300));
        plats.Add(Ceiling(1600, 300));
        plats.Add(Floor(2000, 500));
        plats.Add(Ceiling(2000, 500));
        plats.Add(Floor(2600, 400));
        plats.Add(Ceiling(2600, 400));
        plats.Add(Floor(3100, 400));
        plats.Add(Ceiling(3100, 400));
        plats.Add(Floor(3600, 400));
        plats.Add(Ceiling(3600, 400));
        plats.Add(Floor(4100, 400));
        plats.Add(Ceiling(4100, 400));

        // Bouncy platforms
        plats.Add(Mid(500, 350, 80, PlatformType.Bouncy));
        plats.Add(Mid(900, 120, 80, PlatformType.Bouncy));
        plats.Add(Mid(1500, 350, 80, PlatformType.Bouncy));
        plats.Add(Mid(2500, 130, 80, PlatformType.Bouncy));
        plats.Add(Mid(3500, 350, 80, PlatformType.Bouncy));

        // Moving
        var mp1 = Mid(1900, 230, 80, PlatformType.Moving);
        mp1.MoveStartY = 120; mp1.MoveEndY = 360; mp1.MoveSpeed = 90;
        plats.Add(mp1);

        // Spikes galore
        hazards.Add(SpikeUp(650, FloorY - 12)); hazards.Add(SpikeUp(670, FloorY - 12));
        hazards.Add(SpikeDown(800, CeilingY + PlatThick)); hazards.Add(SpikeDown(820, CeilingY + PlatThick));
        hazards.Add(SpikeUp(1200, FloorY - 12)); hazards.Add(SpikeUp(1220, FloorY - 12));
        hazards.Add(SpikeUp(1240, FloorY - 12));
        hazards.Add(SpikeDown(1700, CeilingY + PlatThick)); hazards.Add(SpikeDown(1720, CeilingY + PlatThick));
        hazards.Add(SpikeUp(2200, FloorY - 12)); hazards.Add(SpikeUp(2220, FloorY - 12));
        hazards.Add(SpikeDown(2800, CeilingY + PlatThick)); hazards.Add(SpikeDown(2820, CeilingY + PlatThick));
        hazards.Add(SpikeUp(3300, FloorY - 12)); hazards.Add(SpikeUp(3320, FloorY - 12));
        hazards.Add(SpikeDown(3900, CeilingY + PlatThick));

        // Coins
        coins.Add(C(250, 420)); coins.Add(C(550, 310));
        coins.Add(C(750, 60)); coins.Add(C(950, 80));
        coins.Add(C(1300, 300)); coins.Add(C(1550, 310));
        coins.Add(C(1950, 190)); coins.Add(C(2300, 420));
        coins.Add(C(2550, 90)); coins.Add(C(2900, 300));
        coins.Add(C(3200, 420)); coins.Add(C(3550, 310));
        coins.Add(C(3900, 420)); coins.Add(C(4200, 420));

        return new Level(5, 145, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 6: Mixed Mayhem ──────────────────────────

    private static Level BuildLevel6()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 5000;

        plats.Add(Floor(0, 400));
        plats.Add(Ceiling(0, 400));
        plats.Add(Floor(500, 300));
        plats.Add(Ceiling(500, 300));
        plats.Add(Floor(900, 400));
        plats.Add(Ceiling(900, 400));
        plats.Add(Floor(1400, 300));
        plats.Add(Ceiling(1400, 300));
        plats.Add(Floor(1800, 400));
        plats.Add(Ceiling(1800, 400));
        plats.Add(Floor(2300, 300));
        plats.Add(Ceiling(2300, 300));
        plats.Add(Floor(2700, 400));
        plats.Add(Ceiling(2700, 400));
        plats.Add(Floor(3200, 300));
        plats.Add(Ceiling(3200, 300));
        plats.Add(Floor(3600, 400));
        plats.Add(Ceiling(3600, 400));
        plats.Add(Floor(4100, 400));
        plats.Add(Ceiling(4100, 400));
        plats.Add(Floor(4600, 400));
        plats.Add(Ceiling(4600, 400));

        // All types of platforms
        plats.Add(Mid(400, 280, 80, PlatformType.Crumbling));
        plats.Add(Mid(800, 180, 80, PlatformType.Bouncy));

        var mp1 = Mid(1300, 250, 80, PlatformType.Moving);
        mp1.MoveStartY = 100; mp1.MoveEndY = 380; mp1.MoveSpeed = 100;
        plats.Add(mp1);

        plats.Add(Mid(1700, 320, 80, PlatformType.Crumbling));
        plats.Add(Mid(2200, 140, 80, PlatformType.Bouncy));
        plats.Add(Mid(2600, 300, 80, PlatformType.Crumbling));

        var mp2 = Mid(3100, 200, 80, PlatformType.Moving);
        mp2.MoveStartY = 80; mp2.MoveEndY = 400; mp2.MoveSpeed = 110;
        plats.Add(mp2);

        plats.Add(Mid(3500, 350, 80, PlatformType.Bouncy));
        plats.Add(Mid(4000, 160, 80, PlatformType.Crumbling));
        plats.Add(Mid(4500, 300, 100));

        // Heavy spikes
        for (int i = 0; i < 8; i++)
        {
            hazards.Add(SpikeUp(450 + i * 550, FloorY - 12));
            hazards.Add(SpikeUp(470 + i * 550, FloorY - 12));
        }
        for (int i = 0; i < 6; i++)
        {
            hazards.Add(SpikeDown(600 + i * 700, CeilingY + PlatThick));
            hazards.Add(SpikeDown(620 + i * 700, CeilingY + PlatThick));
        }

        // Coins scattered
        for (int i = 0; i < 16; i++)
            coins.Add(C(200 + i * 290, i % 2 == 0 ? 420 : 60));

        return new Level(6, 155, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 7: Gauntlet ──────────────────────────────

    private static Level BuildLevel7()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 5500;

        // Shorter floor/ceiling segments = more gaps
        for (int i = 0; i < 14; i++)
        {
            double x = i * 380;
            double w = 280;
            plats.Add(Floor(x, w));
            plats.Add(Ceiling(x, w));
        }

        // Lots of mid platforms of all types
        for (int i = 0; i < 10; i++)
        {
            double x = 300 + i * 500;
            PlatformType t = (PlatformType)(i % 4);
            var p = Mid(x, i % 2 == 0 ? 300 : 160, 70, t);
            if (t == PlatformType.Moving)
            {
                p.MoveStartY = 80;
                p.MoveEndY = 400;
                p.MoveSpeed = 90 + i * 5;
            }
            plats.Add(p);
        }

        // Spikes everywhere
        for (int i = 0; i < 12; i++)
        {
            hazards.Add(SpikeUp(200 + i * 420, FloorY - 12));
            hazards.Add(SpikeUp(220 + i * 420, FloorY - 12));
            if (i % 2 == 0)
            {
                hazards.Add(SpikeDown(350 + i * 420, CeilingY + PlatThick));
                hazards.Add(SpikeDown(370 + i * 420, CeilingY + PlatThick));
            }
        }

        // Coins
        for (int i = 0; i < 18; i++)
            coins.Add(C(150 + i * 290, i % 3 == 0 ? 420 : i % 3 == 1 ? 230 : 60));

        return new Level(7, 165, plats, hazards, coins, width - 60, 200, width);
    }

    // ── Level 8: Final Challenge ───────────────────────

    private static Level BuildLevel8()
    {
        var plats = new List<Platform>();
        var hazards = new List<Hazard>();
        var coins = new List<Coin>();
        double width = 6000;

        // Very short floor/ceiling segments
        for (int i = 0; i < 18; i++)
        {
            double x = i * 330;
            double w = 220;
            plats.Add(Floor(x, w));
            plats.Add(Ceiling(x, w));
        }

        // Dense mid platforms of all types
        for (int i = 0; i < 14; i++)
        {
            double x = 250 + i * 400;
            PlatformType t = (PlatformType)(i % 4);
            var p = Mid(x, (i % 3) switch { 0 => 320, 1 => 160, _ => 240 }, 60, t);
            if (t == PlatformType.Moving)
            {
                p.MoveStartY = 60;
                p.MoveEndY = 420;
                p.MoveSpeed = 100 + i * 8;
            }
            plats.Add(p);
        }

        // Maximum spikes
        for (int i = 0; i < 16; i++)
        {
            hazards.Add(SpikeUp(180 + i * 350, FloorY - 12));
            hazards.Add(SpikeUp(200 + i * 350, FloorY - 12));
            hazards.Add(SpikeDown(280 + i * 350, CeilingY + PlatThick));
            hazards.Add(SpikeDown(300 + i * 350, CeilingY + PlatThick));
        }

        // Coins
        for (int i = 0; i < 20; i++)
            coins.Add(C(100 + i * 280, i % 3 == 0 ? 420 : i % 3 == 1 ? 230 : 60));

        return new Level(8, 180, plats, hazards, coins, width - 60, 200, width);
    }
}
