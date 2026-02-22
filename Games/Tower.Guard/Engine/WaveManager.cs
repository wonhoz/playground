using TowerGuard.Entities;

namespace TowerGuard.Engine;

public sealed class WaveManager
{
    public int CurrentWave { get; private set; }
    public int TotalWaves => 10;
    public bool AllWavesComplete => CurrentWave > TotalWaves;
    public bool WaveInProgress { get; private set; }
    public double TimerUntilNextWave { get; set; }
    public bool WaitingForNextWave { get; private set; }

    private readonly List<(EnemyType Type, double Delay)> _spawnQueue = [];
    private double _spawnTimer;
    private int _spawnIndex;
    private int _aliveCount;

    public event Action<EnemyType>? OnSpawnEnemy;
    public event Action? OnWaveComplete;
    public event Action? OnAllWavesComplete;

    public void StartFirstWave()
    {
        CurrentWave = 0;
        WaveInProgress = false;
        WaitingForNextWave = true;
        TimerUntilNextWave = 5.0; // shorter first wait
    }

    public void StartNextWave()
    {
        CurrentWave++;
        if (CurrentWave > TotalWaves)
        {
            OnAllWavesComplete?.Invoke();
            return;
        }

        WaitingForNextWave = false;
        WaveInProgress = true;
        _spawnQueue.Clear();
        _spawnIndex = 0;
        _spawnTimer = 0;

        BuildWave(CurrentWave);
    }

    public void ForceStartNextWave()
    {
        if (WaitingForNextWave)
        {
            TimerUntilNextWave = 0;
        }
    }

    public void Update(double dt, int aliveEnemyCount)
    {
        _aliveCount = aliveEnemyCount;

        if (WaitingForNextWave)
        {
            TimerUntilNextWave -= dt;
            if (TimerUntilNextWave <= 0)
            {
                StartNextWave();
            }
            return;
        }

        if (!WaveInProgress) return;

        _spawnTimer -= dt;
        if (_spawnTimer <= 0 && _spawnIndex < _spawnQueue.Count)
        {
            var (type, delay) = _spawnQueue[_spawnIndex];
            OnSpawnEnemy?.Invoke(type);
            _spawnIndex++;
            _spawnTimer = _spawnIndex < _spawnQueue.Count ? _spawnQueue[_spawnIndex].Delay : 0;
        }

        // Wave complete when all spawned and all dead
        if (_spawnIndex >= _spawnQueue.Count && _aliveCount == 0)
        {
            WaveInProgress = false;

            if (CurrentWave >= TotalWaves)
            {
                OnAllWavesComplete?.Invoke();
            }
            else
            {
                WaitingForNextWave = true;
                TimerUntilNextWave = 10.0;
                OnWaveComplete?.Invoke();
            }
        }
    }

    private void BuildWave(int wave)
    {
        // Each wave defines: list of (EnemyType, delayBeforeThisEnemy)
        switch (wave)
        {
            case 1: // 6 Grunts
                AddGroup(EnemyType.Grunt, 6, 1.0);
                break;
            case 2: // 8 Grunts + 3 Runners
                AddGroup(EnemyType.Grunt, 8, 0.9);
                AddGroup(EnemyType.Runner, 3, 0.6);
                break;
            case 3: // 6 Grunts + 4 Runners + 2 Tanks
                AddGroup(EnemyType.Grunt, 6, 0.8);
                AddGroup(EnemyType.Runner, 4, 0.5);
                AddGroup(EnemyType.Tank, 2, 1.5);
                break;
            case 4: // 10 Grunts + 4 Runners + 2 Healers
                AddGroup(EnemyType.Grunt, 10, 0.7);
                AddGroup(EnemyType.Runner, 4, 0.5);
                AddGroup(EnemyType.Healer, 2, 1.2);
                break;
            case 5: // Boss wave: mixed + Boss
                AddGroup(EnemyType.Grunt, 6, 0.6);
                AddGroup(EnemyType.Tank, 3, 1.2);
                AddGroup(EnemyType.Boss, 1, 2.0);
                break;
            case 6: // 12 Runners + 4 Tanks
                AddGroup(EnemyType.Runner, 12, 0.4);
                AddGroup(EnemyType.Tank, 4, 1.0);
                break;
            case 7: // 8 Grunts + 6 Runners + 3 Healers + 3 Tanks
                AddGroup(EnemyType.Grunt, 8, 0.6);
                AddGroup(EnemyType.Runner, 6, 0.4);
                AddGroup(EnemyType.Healer, 3, 1.0);
                AddGroup(EnemyType.Tank, 3, 1.0);
                break;
            case 8: // Heavy: 6 Tanks + 4 Healers + 8 Runners
                AddGroup(EnemyType.Tank, 6, 1.0);
                AddGroup(EnemyType.Healer, 4, 0.8);
                AddGroup(EnemyType.Runner, 8, 0.3);
                break;
            case 9: // 15 Grunts + 8 Runners + 4 Tanks + 3 Healers
                AddGroup(EnemyType.Grunt, 15, 0.5);
                AddGroup(EnemyType.Runner, 8, 0.3);
                AddGroup(EnemyType.Tank, 4, 0.8);
                AddGroup(EnemyType.Healer, 3, 0.8);
                break;
            case 10: // Final boss: everything + 2 bosses
                AddGroup(EnemyType.Grunt, 10, 0.4);
                AddGroup(EnemyType.Runner, 10, 0.3);
                AddGroup(EnemyType.Tank, 5, 0.8);
                AddGroup(EnemyType.Healer, 4, 0.7);
                AddGroup(EnemyType.Boss, 2, 2.5);
                break;
        }

        // Set initial delay for first spawn
        if (_spawnQueue.Count > 0)
        {
            _spawnTimer = _spawnQueue[0].Delay;
        }
    }

    private void AddGroup(EnemyType type, int count, double interval)
    {
        for (int i = 0; i < count; i++)
            _spawnQueue.Add((type, interval));
    }

    /// <summary>HP multiplier for current wave</summary>
    public double HpMultiplier => 1.0 + (CurrentWave - 1) * 0.2;
}
