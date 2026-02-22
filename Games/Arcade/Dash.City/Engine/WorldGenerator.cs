namespace DashCity.Engine;

/// <summary>
/// 무한 월드 생성: 장애물, 코인, 파워업.
/// </summary>
public sealed class WorldGenerator
{
    private readonly Random _rng = new();
    private double _nextObstacle;
    private double _nextCoinGroup;
    private double _nextPowerUp;

    public void Reset()
    {
        _nextObstacle = 40.0;
        _nextCoinGroup = 25.0;
        _nextPowerUp = 80.0;
    }

    public List<WorldObject> Generate(double playerZ, double speed)
    {
        var spawned = new List<WorldObject>();
        double lookAhead = 180.0;
        double minGap = Math.Max(6.0, 12.0 - speed * 0.08);

        // ── 장애물 ──
        while (_nextObstacle < playerZ + lookAhead)
        {
            int pattern = _rng.Next(6);
            switch (pattern)
            {
                case 0: // 단일 트레인
                {
                    int lane = _rng.Next(3) - 1;
                    spawned.Add(new WorldObject(ObjectKind.Train, lane, _nextObstacle));
                    break;
                }
                case 1: // 2레인 트레인 (1개 빈 레인)
                {
                    int freeLane = _rng.Next(3) - 1;
                    for (int l = -1; l <= 1; l++)
                        if (l != freeLane)
                            spawned.Add(new WorldObject(ObjectKind.Train, l, _nextObstacle));
                    break;
                }
                case 2: // 바리케이드 (점프)
                {
                    int count = _rng.Next(1, 3);
                    var used = new HashSet<int>();
                    for (int i = 0; i < count; i++)
                    {
                        int lane = _rng.Next(3) - 1;
                        if (used.Add(lane))
                            spawned.Add(new WorldObject(ObjectKind.Barrier, lane, _nextObstacle));
                    }
                    break;
                }
                case 3: // 높은 빔 (슬라이드)
                {
                    for (int l = -1; l <= 1; l++)
                        spawned.Add(new WorldObject(ObjectKind.Beam, l, _nextObstacle));
                    break;
                }
                case 4: // 트레인 + 바리케이드 콤보
                {
                    int trainLane = _rng.Next(3) - 1;
                    spawned.Add(new WorldObject(ObjectKind.Train, trainLane, _nextObstacle));
                    int barrierLane;
                    do { barrierLane = _rng.Next(3) - 1; } while (barrierLane == trainLane);
                    spawned.Add(new WorldObject(ObjectKind.Barrier, barrierLane, _nextObstacle + 2));
                    break;
                }
                default: // 지그재그 트레인
                {
                    int lane1 = _rng.Next(3) - 1;
                    spawned.Add(new WorldObject(ObjectKind.Train, lane1, _nextObstacle));
                    int lane2;
                    do { lane2 = _rng.Next(3) - 1; } while (lane2 == lane1);
                    spawned.Add(new WorldObject(ObjectKind.Train, lane2, _nextObstacle + minGap * 0.6));
                    break;
                }
            }
            _nextObstacle += minGap + _rng.NextDouble() * 4;
        }

        // ── 코인 그룹 ──
        while (_nextCoinGroup < playerZ + lookAhead)
        {
            int lane = _rng.Next(3) - 1;
            int coinCount = _rng.Next(3, 8);
            int coinPattern = _rng.Next(3);

            for (int i = 0; i < coinCount; i++)
            {
                double z = _nextCoinGroup + i * 2.5;
                double y = coinPattern switch
                {
                    0 => 1.0,                                       // 직선
                    1 => 1.0 + Math.Sin(i * 0.8) * 1.2,            // 웨이브
                    _ => 1.0 + i * 0.4,                              // 상승
                };
                spawned.Add(new WorldObject(ObjectKind.Coin, lane, z, y));
            }
            _nextCoinGroup += 15 + _rng.NextDouble() * 10;
        }

        // ── 파워업 ──
        while (_nextPowerUp < playerZ + lookAhead)
        {
            int lane = _rng.Next(3) - 1;
            var kind = _rng.Next(4) switch
            {
                0 => ObjectKind.Magnet,
                1 => ObjectKind.Shield,
                2 => ObjectKind.Multiplier,
                _ => ObjectKind.Jetpack
            };
            spawned.Add(new WorldObject(kind, lane, _nextPowerUp, 1.5));
            _nextPowerUp += 60 + _rng.NextDouble() * 40;
        }

        return spawned;
    }
}
