using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DodgeBlitz.Engine;
using DodgeBlitz.Entities;

namespace DodgeBlitz;

public partial class MainWindow : Window
{
    // ── 게임 영역 ─────────────────────────────────────────────────────────
    private const double AreaW = 500;
    private const double AreaH = 550;

    // ── 상태 ─────────────────────────────────────────────────────────────
    private enum GameState { Title, Playing, GameOver }
    private GameState _state = GameState.Title;

    // ── 엔진 ──────────────────────────────────────────────────────────────
    private readonly GameEngine   _engine = new();
    private readonly GameInput _input  = new();

    // ── 엔티티 ────────────────────────────────────────────────────────────
    private Player             _player  = null!;
    private readonly List<Bullet>   _bullets   = [];
    private readonly List<Particle> _particles = [];
    private readonly List<Star>     _stars     = [];

    // ── 게임 데이터 ────────────────────────────────────────────────────────
    private double _survivalTime;
    private double _bestTime;
    private double _spawnTimer;
    private string _prevLevel = "";

    private readonly Random _rng = new();

    // ── 생성자 ────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { ApplyDarkTitleBar(); IconGenerator.EnsureIcon(this); };

        InitStars();
        _engine.OnUpdate += OnUpdate;
        _engine.OnRender += OnRender;
        _engine.Start();

        UpdateTitleBest();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private void ApplyDarkTitleBar()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }

    // ── 초기화 ────────────────────────────────────────────────────────────
    private void InitStars()
    {
        _stars.Clear();
        for (int i = 0; i < 65; i++)
            _stars.Add(new Star(AreaW, AreaH, _rng, i % 3));
        foreach (var s in _stars)
            GameCanvas.Children.Add(s.Visual!);
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────
    private void StartGame()
    {
        // 이전 엔티티 정리
        foreach (var b in _bullets)
            if (b.Visual is not null) GameCanvas.Children.Remove(b.Visual);
        foreach (var p in _particles)
            if (p.Visual is not null) GameCanvas.Children.Remove(p.Visual);
        if (_player?.Visual is not null)
            GameCanvas.Children.Remove(_player.Visual);

        _bullets.Clear();
        _particles.Clear();

        _survivalTime = 0;
        _spawnTimer   = 0.5;
        _prevLevel    = "";

        _player = new Player(AreaW, AreaH);
        GameCanvas.Children.Add(_player.Visual!);

        TitlePanel.Visibility   = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        HudPanel.Visibility     = Visibility.Visible;

        _state = GameState.Playing;
        _input.Reset();

        SoundGen.PlayBgm(Sounds.Bgm, 0.28);
    }

    // ── 게임 종료 ─────────────────────────────────────────────────────────
    private void EndGame()
    {
        _state = GameState.GameOver;

        if (_survivalTime > _bestTime)
            _bestTime = _survivalTime;

        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.HitSfx);

        // 딜레이 후 게임오버 SFX
        System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            Dispatcher.InvokeAsync(() => SoundGen.Sfx(Sounds.GameOverSfx)));

        // 파티클 폭발
        SpawnParticles(_player.X + _player.Width / 2, _player.Y + _player.Height / 2,
                       24, Color.FromRgb(0, 255, 204));

        FinalTimeText.Text = $"SURVIVED  {_survivalTime:F1}s";
        FinalBestText.Text = _survivalTime >= _bestTime
            ? $"NEW BEST!  {_bestTime:F1}s"
            : $"BEST  {_bestTime:F1}s";

        GameOverPanel.Visibility = Visibility.Visible;
        HudPanel.Visibility     = Visibility.Collapsed;
        UpdateTitleBest();
    }

    // ── 게임 루프 ─────────────────────────────────────────────────────────
    private void OnUpdate(double dt)
    {
        foreach (var s in _stars) s.Update(dt);

        if (_state == GameState.Playing)
            UpdateGame(dt);
    }

    private void OnRender()
    {
        foreach (var s in _stars) s.SyncPosition();

        if (_state is GameState.Playing or GameState.GameOver)
        {
            _player.SyncPosition();
            foreach (var b in _bullets)   b.SyncPosition();
            foreach (var p in _particles) p.SyncPosition();
        }
    }

    private void UpdateGame(double dt)
    {
        _survivalTime += dt;

        // 플레이어 이동 + 무적 타이머
        _player.Move(_input.Left, _input.Right, _input.Up, _input.Down, dt);
        _player.Update(dt);

        // 총알 스폰
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            SpawnBullets();
            _spawnTimer = GetSpawnInterval();
        }

        // 총알 업데이트 + 정리
        foreach (var b in _bullets) b.Update(dt);
        _bullets.RemoveAll(b =>
        {
            if (!b.IsAlive) { GameCanvas.Children.Remove(b.Visual); return true; }
            return false;
        });

        // 파티클 업데이트 + 정리
        foreach (var p in _particles) p.Update(dt);
        _particles.RemoveAll(p =>
        {
            if (!p.IsAlive) { GameCanvas.Children.Remove(p.Visual); return true; }
            return false;
        });

        // 충돌 처리
        if (!_player.IsInvincible)
        {
            foreach (var b in _bullets)
            {
                if (b.IsAlive && _player.CollidesWith(b))
                {
                    b.IsAlive = false;
                    GameCanvas.Children.Remove(b.Visual);
                    EndGame();
                    return;
                }
            }
        }

        // 레벨 상승 SFX
        var lv = GetLevelName();
        if (lv != _prevLevel && _prevLevel != "")
            SoundGen.Sfx(Sounds.LevelUpSfx);
        _prevLevel = lv;

        UpdateHud();
    }

    // ── 총알 스폰 ─────────────────────────────────────────────────────────
    private void SpawnBullets()
    {
        var patterns = GetActivePatterns();
        int pattern  = patterns[_rng.Next(patterns.Length)];
        double speed = GetBulletSpeed();

        switch (pattern)
        {
            case 0: // 단일 추적
                SpawnTracking(speed, Color.FromRgb(255, 55, 55));
                break;
            case 1: // 추적 + 약간 빠름
                SpawnTracking(speed * 1.15, Color.FromRgb(255, 55, 55));
                SpawnTracking(speed * 0.9,  Color.FromRgb(255, 100, 30));
                break;
            case 2: // 스프레드 3방향
                SpawnSpread(speed, 3, 20, Color.FromRgb(255, 130, 0));
                break;
            case 3: // 스프레드 5방향
                SpawnSpread(speed, 5, 16, Color.FromRgb(180, 50, 255));
                break;
            case 4: // 고속 직선
                SpawnStraight(speed * 1.5, Color.FromRgb(255, 60, 160));
                break;
        }
    }

    private int[] GetActivePatterns() => _survivalTime switch
    {
        < 10  => [0],
        < 20  => [0, 0, 2],
        < 35  => [0, 1, 2, 3],
        _     => [0, 1, 2, 3, 4]
    };

    private double GetSpawnInterval() => Math.Max(0.16, 1.3 - _survivalTime * 0.014);
    private double GetBulletSpeed()   => Math.Min(440, 155 + _survivalTime * 3.2);

    private string GetLevelName() => _survivalTime switch
    {
        < 10  => "NOVICE",
        < 20  => "SKILLED",
        < 35  => "EXPERT",
        < 60  => "VETERAN",
        _     => "LEGEND"
    };

    // 추적형 총알 (플레이어 방향 + 오프셋)
    private void SpawnTracking(double speed, Color color)
    {
        var (sx, sy) = RandomEdgePoint();
        double px = _player.X + _player.Width  / 2;
        double py = _player.Y + _player.Height / 2;

        // 시간이 지날수록 더 정확하게
        double spreadDeg = Math.Max(15, 75 - _survivalTime * 1.2);
        double angle = Math.Atan2(py - sy, px - sx)
                     + (_rng.NextDouble() - 0.5) * spreadDeg * Math.PI / 180;

        AddBullet(sx, sy, Math.Cos(angle) * speed, Math.Sin(angle) * speed, color);
    }

    // 스프레드 총알 (여러 각도)
    private void SpawnSpread(double speed, int count, double spreadDeg, Color color)
    {
        var (sx, sy) = RandomEdgePoint();
        double px = _player.X + _player.Width  / 2;
        double py = _player.Y + _player.Height / 2;

        double baseAngle = Math.Atan2(py - sy, px - sx);
        double step      = spreadDeg * Math.PI / 180;

        for (int i = 0; i < count; i++)
        {
            double angle = baseAngle + (i - count / 2) * step;
            AddBullet(sx, sy, Math.Cos(angle) * speed, Math.Sin(angle) * speed, color);
        }
    }

    // 직선 고속 총알 (8방향 중 랜덤)
    private void SpawnStraight(double speed, Color color)
    {
        int    dir      = _rng.Next(8);
        double angle    = dir * Math.PI / 4;
        double oppAngle = angle + Math.PI;

        double sx = Math.Clamp(AreaW / 2 + Math.Cos(oppAngle) * AreaW * 0.75, -20, AreaW + 20);
        double sy = Math.Clamp(AreaH / 2 + Math.Sin(oppAngle) * AreaH * 0.75, -20, AreaH + 20);

        AddBullet(sx, sy, Math.Cos(angle) * speed, Math.Sin(angle) * speed, color);
    }

    private (double sx, double sy) RandomEdgePoint()
    {
        return _rng.Next(4) switch
        {
            0 => (_rng.NextDouble() * AreaW, -15),
            1 => (_rng.NextDouble() * AreaW, AreaH + 15),
            2 => (-15, _rng.NextDouble() * AreaH),
            _ => (AreaW + 15, _rng.NextDouble() * AreaH)
        };
    }

    private void AddBullet(double cx, double cy, double vx, double vy, Color color)
    {
        var b = new Bullet(cx, cy, vx, vy, AreaW, AreaH, color);
        _bullets.Add(b);
        GameCanvas.Children.Add(b.Visual!);
    }

    // ── 파티클 ───────────────────────────────────────────────────────────
    private void SpawnParticles(double cx, double cy, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double spd   = 70 + _rng.NextDouble() * 240;
            double life  = 0.3 + _rng.NextDouble() * 0.55;
            var p = new Particle(cx, cy, Math.Cos(angle) * spd, Math.Sin(angle) * spd, life, color);
            _particles.Add(p);
            GameCanvas.Children.Add(p.Visual!);
        }
    }

    // ── HUD ──────────────────────────────────────────────────────────────
    private void UpdateHud()
    {
        TimeText.Text = $"{_survivalTime:F1}";
        BestText.Text = $"{Math.Max(_bestTime, _survivalTime):F1}";
        LevelText.Text = GetLevelName();
    }

    private void UpdateTitleBest()
    {
        TitleBestText.Text = $"BEST  {_bestTime:F1}s";
    }

    // ── 입력 ─────────────────────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _input.KeyDown(e.Key);

        if (e.Key == Key.Enter)
        {
            if (_state is GameState.Title or GameState.GameOver)
                StartGame();
        }
        else if (e.Key == Key.Escape)
        {
            if (_state == GameState.Playing)
                BackToTitle();
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        _input.KeyUp(e.Key);
    }

    private void BackToTitle()
    {
        SoundGen.StopBgm();

        foreach (var b in _bullets)   GameCanvas.Children.Remove(b.Visual);
        foreach (var p in _particles) GameCanvas.Children.Remove(p.Visual);
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);

        _bullets.Clear();
        _particles.Clear();

        _state = GameState.Title;
        HudPanel.Visibility     = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility   = Visibility.Visible;
        UpdateTitleBest();
    }
}
