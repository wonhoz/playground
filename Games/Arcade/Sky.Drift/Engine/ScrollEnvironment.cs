namespace SkyDrift.Engine;

/// <summary>스크롤 환경 생성기 — 열기류, 장애물, 배경 레이어</summary>
public class ScrollEnvironment
{
    private readonly Random _rng = new();

    public List<Thermal>   Thermals  { get; } = [];
    public List<Obstacle>  Obstacles { get; } = [];

    private double _spawnAltitude;   // 다음 스폰 위치

    public void Reset()
    {
        Thermals.Clear();
        Obstacles.Clear();
        _spawnAltitude = 800;  // 시작 고도 위에서부터 스폰

        // 초기 열기류 몇 개 미리 배치
        for (int i = 0; i < 4; i++)
            SpawnThermal(_spawnAltitude + i * 300);
    }

    /// <summary>현재 고도 기준으로 새 요소 스폰</summary>
    public void Update(double altitude, double screenWidth, double screenHeight)
    {
        // 새 열기류 스폰 (고도가 올라가면)
        while (_spawnAltitude < altitude + screenHeight * 2)
        {
            SpawnThermal(_spawnAltitude);
            SpawnObstacle(_spawnAltitude, screenWidth);
            _spawnAltitude += 200 + _rng.NextDouble() * 400;
        }

        // 멀리 지나간 요소 제거
        double cullY = altitude - screenHeight;
        Thermals.RemoveAll(t => t.Y < cullY);
        Obstacles.RemoveAll(o => o.Y < cullY);
    }

    private void SpawnThermal(double worldY)
    {
        var type = _rng.NextDouble() < 0.7 ? ThermalType.Rising : ThermalType.Downdraft;
        Thermals.Add(new Thermal
        {
            X        = 80 + _rng.NextDouble() * 440,   // 화면 X (0~520 범위)
            Y        = worldY,
            Radius   = 40 + _rng.NextDouble() * 80,
            Strength = type == ThermalType.Rising
                ? 60 + _rng.NextDouble() * 120
                : 40 + _rng.NextDouble() * 80,
            Type     = type,
        });
    }

    private void SpawnObstacle(double worldY, double screenWidth)
    {
        if (_rng.NextDouble() < 0.35) return; // 35% 확률로 스킵

        var kind = _rng.NextDouble() switch
        {
            < 0.4 => ObstacleKind.BirdFlock,
            < 0.7 => ObstacleKind.StormCloud,
            _     => ObstacleKind.WindZone,
        };

        double w = kind switch
        {
            ObstacleKind.BirdFlock  => 60 + _rng.NextDouble() * 60,
            ObstacleKind.StormCloud => 80 + _rng.NextDouble() * 120,
            _                       => 120 + _rng.NextDouble() * 160,
        };

        Obstacles.Add(new Obstacle
        {
            X      = 60 + _rng.NextDouble() * (screenWidth - 120),
            Y      = worldY,
            Width  = w,
            Height = kind == ObstacleKind.WindZone ? 40 : w * 0.6,
            Kind   = kind,
            VX     = kind == ObstacleKind.BirdFlock ? (_rng.NextDouble() < 0.5 ? 50 : -50) : 0,
        });
    }

    /// <summary>글라이더 위치에서 합산 기류 속도 계산</summary>
    public double GetTotalLift(double gliderX, double altitude)
    {
        double total = 0;
        foreach (var t in Thermals)
            total += t.GetLift(gliderX, altitude);
        return Math.Clamp(total, -200, 300);
    }
}
