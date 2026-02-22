namespace NeonRun.Engine;

/// <summary>
/// 무한 트랙 장애물/크리스탈 생성.
/// </summary>
public sealed class TrackGenerator
{
    private readonly Random _rng = new();
    private double _nextSpawn;
    private double _spawnInterval = 8.0;

    public void Reset()
    {
        _nextSpawn = 30.0;
        _spawnInterval = 8.0;
    }

    public List<Obstacle> Update(double playerZ, double speed, List<Obstacle> existing)
    {
        var spawned = new List<Obstacle>();

        // 속도에 따라 간격 조절
        _spawnInterval = Math.Max(4.0, 8.0 - speed * 0.05);

        while (_nextSpawn < playerZ + 200)
        {
            // 장애물 1~2개 + 크리스탈
            int count = _rng.Next(1, 3);
            var usedLanes = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                int lane = _rng.Next(3) - 1; // -1, 0, 1
                if (usedLanes.Contains(lane)) continue;
                usedLanes.Add(lane);

                var type = _rng.NextDouble() < 0.3
                    ? ObstacleType.LowBar
                    : ObstacleType.Wall;

                spawned.Add(new Obstacle(type, lane, _nextSpawn));
            }

            // 빈 레인에 크리스탈 배치
            for (int lane = -1; lane <= 1; lane++)
            {
                if (!usedLanes.Contains(lane) && _rng.NextDouble() < 0.4)
                {
                    spawned.Add(new Obstacle(ObstacleType.Crystal, lane, _nextSpawn));
                }
            }

            _nextSpawn += _spawnInterval;
        }

        // 오래된 장애물 제거
        existing.RemoveAll(o => o.Z < playerZ - 20);

        return spawned;
    }
}
