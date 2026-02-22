using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using StarStrike.Engine;
using StarStrike.Entities;

namespace StarStrike;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // 게임 상수
    private const double CanvasW = 484; // 500 - border
    private const double CanvasH = 662; // 700 - titlebar

    // 엔진
    private readonly GameEngine _engine = new();
    private readonly Engine.InputManager _input = new();
    private readonly Random _rng = new();

    // 엔티티
    private Player _player = null!;
    private readonly List<Bullet> _bullets = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Particle> _particles = [];
    private readonly List<Star> _stars = [];

    // 게임 상태
    private enum GameState { Title, Playing, Paused, GameOver }
    private GameState _state = GameState.Title;
    private int _score;
    private int _highScore;
    private int _wave;
    private int _enemiesInWave;
    private int _enemiesSpawned;
    private double _spawnTimer;
    private double _spawnInterval;
    private double _fireTimer;
    private const double FireInterval = 0.15;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int value = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
            }

            InitStars();
            _engine.OnUpdate += OnUpdate;
            _engine.OnRender += OnRender;
            _engine.Start();
            Focus();
        };
    }

    // ── 초기화 ──────────────────────────────────────────

    private void InitStars()
    {
        for (int i = 0; i < 60; i++)
        {
            var layer = _rng.Next(3);
            double size = layer switch { 0 => 1, 1 => 1.5, _ => 2.5 };
            double speed = layer switch { 0 => 20, 1 => 40, _ => 70 };
            byte bright = (byte)(layer switch { 0 => 60, 1 => 100, _ => 160 });
            var star = new Star(_rng.NextDouble() * CanvasW, _rng.NextDouble() * CanvasH, size, speed, bright);
            _stars.Add(star);
            GameCanvas.Children.Add(star.Visual!);
        }
    }

    private void StartGame()
    {
        _score = 0;
        _wave = 0;

        // 기존 엔티티 클리어
        foreach (var b in _bullets) if (b.Visual is not null) GameCanvas.Children.Remove(b.Visual);
        foreach (var e in _enemies) if (e.Visual is not null) GameCanvas.Children.Remove(e.Visual);
        foreach (var p in _particles) if (p.Visual is not null) GameCanvas.Children.Remove(p.Visual);
        _bullets.Clear();
        _enemies.Clear();
        _particles.Clear();

        // 플레이어 생성 (또는 리셋)
        if (_player?.Visual is not null)
            GameCanvas.Children.Remove(_player.Visual);

        _player = new Player(_input, CanvasW, CanvasH);
        GameCanvas.Children.Add(_player.Visual!);

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;

        SoundGen.PlayBgm(Sounds.Bgm);
        NextWave();
    }

    private void NextWave()
    {
        _wave++;
        _enemiesSpawned = 0;
        _enemiesInWave = 4 + _wave * 2;
        _spawnInterval = Math.Max(0.4, 1.5 - _wave * 0.1);
        _spawnTimer = 0.5;
        WaveText.Text = $"WAVE {_wave}";
    }

    // ── 게임 루프 ──────────────────────────────────────

    private void OnUpdate(double dt)
    {
        // 별은 항상 움직임
        foreach (var star in _stars) star.Update(dt);

        if (_state != GameState.Playing) return;

        // 플레이어
        _player.Update(dt);

        // 발사
        _fireTimer -= dt;
        if (_input.Fire && _fireTimer <= 0)
        {
            _fireTimer = FireInterval;
            var bullet = new Bullet(_player.X + _player.Width / 2, _player.Y, true);
            _bullets.Add(bullet);
            GameCanvas.Children.Add(bullet.Visual!);
            SoundGen.Sfx(Sounds.ShootSfx);
        }

        // 적 스폰
        _spawnTimer -= dt;
        if (_spawnTimer <= 0 && _enemiesSpawned < _enemiesInWave)
        {
            SpawnEnemy();
            _spawnTimer = _spawnInterval;
        }

        // 적 업데이트 + 적 발사
        foreach (var enemy in _enemies)
        {
            enemy.Update(dt);
            if (enemy.IsAlive && enemy.TryShoot())
            {
                var eb = new Bullet(enemy.X + enemy.Width / 2, enemy.Y + enemy.Height, false);
                _bullets.Add(eb);
                GameCanvas.Children.Add(eb.Visual!);
            }
        }

        // 총알 업데이트
        foreach (var b in _bullets) b.Update(dt);

        // 파티클 업데이트
        foreach (var p in _particles) p.Update(dt);

        // 충돌 감지
        CheckCollisions();

        // 죽은 오브젝트 제거
        CleanDead(_bullets);
        CleanDead(_enemies);
        CleanDead(_particles);

        // 웨이브 완료 체크
        if (_enemiesSpawned >= _enemiesInWave && _enemies.Count == 0)
            NextWave();

        // 게임오버 체크
        if (!_player.IsAlive)
        {
            SpawnExplosion(_player.X + _player.Width / 2, _player.Y + _player.Height / 2, 30,
                Color.FromRgb(0x3A, 0x86, 0xFF));
            _state = GameState.GameOver;
            HudPanel.Visibility = Visibility.Collapsed;
            FinalScoreText.Text = $"SCORE: {_score}";
            if (_score > _highScore)
            {
                _highScore = _score;
                NewHighScoreText.Text = "★ NEW HIGH SCORE! ★";
            }
            else
            {
                NewHighScoreText.Text = "";
            }
            GameOverOverlay.Visibility = Visibility.Visible;
        }

        // HUD 업데이트
        ScoreText.Text = $"SCORE: {_score}";
        LivesText.Text = new string('♥', Math.Max(0, _player.Lives));
    }

    private void OnRender()
    {
        foreach (var star in _stars) star.SyncPosition();

        if (_state != GameState.Playing && _state != GameState.Paused) return;

        _player.SyncPosition();
        foreach (var b in _bullets) b.SyncPosition();
        foreach (var e in _enemies) e.SyncPosition();
        foreach (var p in _particles) p.SyncPosition();
    }

    // ── 스폰 ──────────────────────────────────────────

    private void SpawnEnemy()
    {
        _enemiesSpawned++;

        var type = _wave switch
        {
            <= 2 => EnemyType.Basic,
            <= 4 => _rng.NextDouble() < 0.3 ? EnemyType.Fast : EnemyType.Basic,
            _ => _rng.NextDouble() switch
            {
                < 0.5 => EnemyType.Basic,
                < 0.8 => EnemyType.Fast,
                _ => EnemyType.Tank
            }
        };

        double ew = type switch { EnemyType.Tank => 40, EnemyType.Fast => 24, _ => 30 };
        double x = _rng.NextDouble() * (CanvasW - ew);
        var enemy = new Enemy(type, x, -40, CanvasW, _rng);
        _enemies.Add(enemy);
        GameCanvas.Children.Add(enemy.Visual!);
    }

    private void SpawnExplosion(double cx, double cy, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 50 + _rng.NextDouble() * 200;
            double vx = Math.Cos(angle) * speed;
            double vy = Math.Sin(angle) * speed;
            double life = 0.3 + _rng.NextDouble() * 0.5;

            var p = new Particle(cx, cy, vx, vy, life, color);
            _particles.Add(p);
            GameCanvas.Children.Add(p.Visual!);
        }
    }

    // ── 충돌 ──────────────────────────────────────────

    private void CheckCollisions()
    {
        // 플레이어 탄 vs 적
        foreach (var bullet in _bullets)
        {
            if (!bullet.IsPlayerBullet || !bullet.IsAlive) continue;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive) continue;
                if (bullet.CollidesWith(enemy))
                {
                    bullet.IsAlive = false;
                    enemy.TakeDamage();
                    if (!enemy.IsAlive)
                    {
                        _score += enemy.ScoreValue;
                        var expColor = enemy.Type switch
                        {
                            EnemyType.Basic => Color.FromRgb(0xE7, 0x4C, 0x3C),
                            EnemyType.Fast => Color.FromRgb(0xFF, 0xA5, 0x00),
                            _ => Color.FromRgb(0x8E, 0x44, 0xAD)
                        };
                        SpawnExplosion(enemy.X + enemy.Width / 2, enemy.Y + enemy.Height / 2, 15, expColor);
                    }
                    break;
                }
            }
        }

        // 적 탄 vs 플레이어
        foreach (var bullet in _bullets)
        {
            if (bullet.IsPlayerBullet || !bullet.IsAlive) continue;
            if (bullet.CollidesWith(_player))
            {
                bullet.IsAlive = false;
                _player.Hit();
            }
        }

        // 적 몸체 vs 플레이어
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;
            if (enemy.CollidesWith(_player))
            {
                enemy.IsAlive = false;
                _player.Hit();
                SpawnExplosion(enemy.X + enemy.Width / 2, enemy.Y + enemy.Height / 2, 12,
                    Color.FromRgb(0xFF, 0xAA, 0x00));
            }
        }
    }

    // ── 클린업 ──────────────────────────────────────────

    private void CleanDead<T>(List<T> list) where T : GameObject
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].IsAlive)
            {
                if (list[i].Visual is not null)
                    GameCanvas.Children.Remove(list[i].Visual);
                list.RemoveAt(i);
            }
        }
    }

    // ── 입력 ──────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _input.KeyDown(e.Key);

        switch (e.Key)
        {
            case Key.Enter when _state is GameState.Title or GameState.GameOver:
                StartGame();
                break;
            case Key.P when _state == GameState.Playing:
                _state = GameState.Paused;
                _engine.Pause();
                PauseOverlay.Visibility = Visibility.Visible;
                break;
            case Key.P when _state == GameState.Paused:
                _state = GameState.Playing;
                _engine.Resume();
                PauseOverlay.Visibility = Visibility.Collapsed;
                break;
            case Key.Escape:
                if (_state == GameState.Playing)
                {
                    _engine.Stop();
                    _state = GameState.Title;
                    ShowTitle();
                }
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) => _input.KeyUp(e.Key);

    private void ShowTitle()
    {
        // 엔티티 클리어
        foreach (var b in _bullets) if (b.Visual is not null) GameCanvas.Children.Remove(b.Visual);
        foreach (var en in _enemies) if (en.Visual is not null) GameCanvas.Children.Remove(en.Visual);
        foreach (var p in _particles) if (p.Visual is not null) GameCanvas.Children.Remove(p.Visual);
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);
        _bullets.Clear(); _enemies.Clear(); _particles.Clear();

        HudPanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        HighScoreTitle.Text = _highScore > 0 ? $"HIGH SCORE: {_highScore}" : "";

        _input.Reset();
        _engine.Start();
    }
}
