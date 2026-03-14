namespace OrbitRaid.Models;

public class Level
{
    public int Number { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public List<Body> Bodies { get; set; } = [];
    public Body? Player { get; set; }
    public Body? Target { get; set; }

    // 발사 제한
    public int MaxThrusts { get; set; } = 0;   // 0 = 무제한 (관성만)
    public double TimeLimit { get; set; } = 0;  // 0 = 무제한

    // 성공 조건: Player가 Target.SOI 내에서 Target.TargetCenter 주변 궤도에 진입
    public double SuccessSOI { get; set; }       // Target.SOI와 동일
}

public static class LevelFactory
{
    private const double AU = 1.496e11; // 1 AU in meters (for scaling)
    private const double ME = 5.972e24; // Earth mass
    private const double RE = 6.371e6;  // Earth radius

    // 레벨별 스케일: 실제 물리 단위 (m, kg) 사용하되 시각적 스케일은 메인에서 조정
    public static Level Create(int level) => level switch
    {
        1 => Level1(),
        2 => Level2(),
        3 => Level3(),
        4 => Level4(),
        5 => Level5(),
        _ => Level1()
    };

    private static Level Level1()
    {
        var sun = new Body
        {
            Type = BodyType.Planet,
            Name = "항성",
            Position = Vector2D.Zero,
            Mass = 1.989e30 * 0.3,  // 0.3 태양질량
            Radius = 2e9,
            IsStatic = true,
            SOI = double.MaxValue,
            Color = System.Windows.Media.Color.FromRgb(255, 220, 80)
        };

        var planet = new Body
        {
            Type = BodyType.Target,
            Name = "목표 행성",
            Position = new Vector2D(1.5e11, 0),
            Velocity = new Vector2D(0, Math.Sqrt(PhysicsConstants.G * sun.Mass / 1.5e11)),
            Mass = ME * 2,
            Radius = 8e8,
            IsStatic = false,
            SOI = 3e10,
            TargetCenter = sun,
            TargetOrbitRadius = 1.5e11,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 120)
        };

        var launchSpeed = Math.Sqrt(PhysicsConstants.G * sun.Mass / 0.8e11) * 1.15;
        var player = new Body
        {
            Type = BodyType.Player,
            Name = "탐사선",
            Position = new Vector2D(-0.8e11, 0),
            Velocity = new Vector2D(0, -launchSpeed),
            Mass = 1000,
            Radius = 2e8,
            SOI = 1e9,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 220)
        };

        return new Level
        {
            Number = 1,
            Title = "첫 번째 호만 전이",
            Hint = "관성만으로 항성 중력을 이용해 초록 행성 궤도에 진입하세요.",
            Bodies = [sun, planet],
            Player = player,
            Target = planet,
            MaxThrusts = 0,
            SuccessSOI = planet.SOI
        };
    }

    private static Level Level2()
    {
        var sun = new Body
        {
            Type = BodyType.Planet, Name = "쌍성 A",
            Position = new Vector2D(-3e10, 0),
            Velocity = new Vector2D(0, 8000),
            Mass = 1.989e30 * 0.25, Radius = 1.5e9, IsStatic = false,
            SOI = 5e10,
            Color = System.Windows.Media.Color.FromRgb(255, 160, 60)
        };
        var sun2 = new Body
        {
            Type = BodyType.Planet, Name = "쌍성 B",
            Position = new Vector2D(3e10, 0),
            Velocity = new Vector2D(0, -8000),
            Mass = 1.989e30 * 0.25, Radius = 1.5e9, IsStatic = false,
            SOI = 5e10,
            Color = System.Windows.Media.Color.FromRgb(180, 60, 255)
        };
        var target = new Body
        {
            Type = BodyType.Target, Name = "행성 베타",
            Position = new Vector2D(0, 2e11),
            Velocity = new Vector2D(20000, 0),
            Mass = ME * 3, Radius = 6e8, IsStatic = false,
            SOI = 3e10,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 120)
        };
        var player = new Body
        {
            Type = BodyType.Player, Name = "탐사선",
            Position = new Vector2D(0, -1.5e11),
            Velocity = new Vector2D(-18000, 0),
            Mass = 1000, Radius = 2e8, SOI = 1e9,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 220)
        };
        return new Level
        {
            Number = 2, Title = "쌍성 시스템",
            Hint = "두 항성의 중력 보조를 모두 이용하세요.",
            Bodies = [sun, sun2, target],
            Player = player, Target = target,
            MaxThrusts = 0, SuccessSOI = target.SOI
        };
    }

    private static Level Level3()
    {
        var central = new Body
        {
            Type = BodyType.Planet, Name = "중성자성",
            Position = Vector2D.Zero,
            Mass = 1.989e30 * 1.5, Radius = 1e9, IsStatic = true,
            SOI = double.MaxValue,
            Color = System.Windows.Media.Color.FromRgb(200, 220, 255)
        };
        var moon = new Body
        {
            Type = BodyType.Target, Name = "목표 위성",
            Position = new Vector2D(0, 8e10),
            Velocity = new Vector2D(Math.Sqrt(PhysicsConstants.G * central.Mass / 8e10), 0),
            Mass = ME, Radius = 4e8, IsStatic = false,
            SOI = 2e10,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 120)
        };
        var blocker = new Body
        {
            Type = BodyType.Planet, Name = "방해 행성",
            Position = new Vector2D(0, 4e10),
            Velocity = new Vector2D(Math.Sqrt(PhysicsConstants.G * central.Mass / 4e10), 0),
            Mass = ME * 5, Radius = 1.2e9, IsStatic = false,
            SOI = 1.5e10,
            Color = System.Windows.Media.Color.FromRgb(200, 80, 80)
        };
        var player = new Body
        {
            Type = BodyType.Player, Name = "탐사선",
            Position = new Vector2D(-6e10, 0),
            Velocity = new Vector2D(0, -Math.Sqrt(PhysicsConstants.G * central.Mass / 6e10) * 0.9),
            Mass = 1000, Radius = 2e8, SOI = 1e9,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 220)
        };
        return new Level
        {
            Number = 3, Title = "방해 행성 회피",
            Hint = "붉은 행성을 충돌하지 않고 위성 궤도에 진입하세요.",
            Bodies = [central, moon, blocker],
            Player = player, Target = moon,
            MaxThrusts = 0, SuccessSOI = moon.SOI
        };
    }

    private static Level Level4()
    {
        var star = new Body
        {
            Type = BodyType.Planet, Name = "항성",
            Position = Vector2D.Zero,
            Mass = 1.989e30 * 0.4, Radius = 2e9, IsStatic = true,
            SOI = double.MaxValue,
            Color = System.Windows.Media.Color.FromRgb(255, 220, 80)
        };
        double r1 = 8e10, r2 = 2e11;
        var planet1 = new Body
        {
            Type = BodyType.Planet, Name = "내행성",
            Position = new Vector2D(r1, 0),
            Velocity = new Vector2D(0, Math.Sqrt(PhysicsConstants.G * star.Mass / r1)),
            Mass = ME * 4, Radius = 7e8, IsStatic = false, SOI = 2e10,
            Color = System.Windows.Media.Color.FromRgb(180, 120, 80)
        };
        var target = new Body
        {
            Type = BodyType.Target, Name = "외행성",
            Position = new Vector2D(-r2, 0),
            Velocity = new Vector2D(0, -Math.Sqrt(PhysicsConstants.G * star.Mass / r2)),
            Mass = ME * 8, Radius = 1e9, IsStatic = false, SOI = 4e10,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 120)
        };
        var player = new Body
        {
            Type = BodyType.Player, Name = "탐사선",
            Position = new Vector2D(0, r1 * 0.6),
            Velocity = new Vector2D(-Math.Sqrt(PhysicsConstants.G * star.Mass / (r1 * 0.6)) * 0.8, 0),
            Mass = 1000, Radius = 2e8, SOI = 1e9,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 220)
        };
        return new Level
        {
            Number = 4, Title = "내행성 스윙바이",
            Hint = "내행성 중력 보조로 속도를 얻어 외행성에 도달하세요.",
            Bodies = [star, planet1, target],
            Player = player, Target = target,
            MaxThrusts = 0, SuccessSOI = target.SOI
        };
    }

    private static Level Level5()
    {
        var star = new Body
        {
            Type = BodyType.Planet, Name = "적색왜성",
            Position = Vector2D.Zero,
            Mass = 1.989e30 * 0.2, Radius = 1.5e9, IsStatic = true,
            SOI = double.MaxValue,
            Color = System.Windows.Media.Color.FromRgb(255, 80, 40)
        };
        var p1 = new Body
        {
            Type = BodyType.Planet, Name = "행성 I",
            Position = new Vector2D(5e10, 0),
            Velocity = new Vector2D(0, Math.Sqrt(PhysicsConstants.G * star.Mass / 5e10)),
            Mass = ME, Radius = 5e8, IsStatic = false, SOI = 1.5e10,
            Color = System.Windows.Media.Color.FromRgb(150, 150, 200)
        };
        var p2 = new Body
        {
            Type = BodyType.Planet, Name = "행성 II",
            Position = new Vector2D(0, -1e11),
            Velocity = new Vector2D(Math.Sqrt(PhysicsConstants.G * star.Mass / 1e11), 0),
            Mass = ME * 2, Radius = 6e8, IsStatic = false, SOI = 2e10,
            Color = System.Windows.Media.Color.FromRgb(150, 200, 150)
        };
        var target = new Body
        {
            Type = BodyType.Target, Name = "행성 III",
            Position = new Vector2D(-1.8e11, 0),
            Velocity = new Vector2D(0, -Math.Sqrt(PhysicsConstants.G * star.Mass / 1.8e11)),
            Mass = ME * 6, Radius = 9e8, IsStatic = false, SOI = 4e10,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 120)
        };
        var player = new Body
        {
            Type = BodyType.Player, Name = "탐사선",
            Position = new Vector2D(5e10, 1e10),
            Velocity = new Vector2D(-Math.Sqrt(PhysicsConstants.G * star.Mass / 5e10) * 0.5,
                                    -Math.Sqrt(PhysicsConstants.G * star.Mass / 5e10) * 0.5),
            Mass = 1000, Radius = 2e8, SOI = 1e9,
            Color = System.Windows.Media.Color.FromRgb(80, 200, 220)
        };
        return new Level
        {
            Number = 5, Title = "다중 스윙바이 마스터",
            Hint = "세 행성의 중력을 모두 활용해야만 목표에 도달할 수 있습니다.",
            Bodies = [star, p1, p2, target],
            Player = player, Target = target,
            MaxThrusts = 0, SuccessSOI = target.SOI
        };
    }
}
