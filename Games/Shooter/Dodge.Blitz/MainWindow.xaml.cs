using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
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
    private enum GameState { Title, Playing, Paused, GameOver }
    private GameState _state = GameState.Title;

    // ── 엔진 ──────────────────────────────────────────────────────────────
    private readonly GameEngine       _engine   = new();
    private readonly GameInput        _input    = new();
    private readonly JoystickManager  _joystick = new();

    // ── 엔티티 ────────────────────────────────────────────────────────────
    private Player?            _player;
    private readonly List<Bullet>   _bullets   = [];
    private readonly List<Particle> _particles = [];
    private readonly List<Star>     _stars     = [];

    // ── 게임 데이터 ────────────────────────────────────────────────────────
    private double _survivalTime;
    private double _bestTime;
    private double _spawnTimer;
    private string _prevLevel = "";

    // ── 마일스톤 팝업 ─────────────────────────────────────────────────────
    private static readonly double[] Milestones = [5, 10, 20, 35, 60, 90, 120];
    private int    _milestoneIndex;
    private double _milestoneTimer;

    // ── 세션 기록 히스토리 ─────────────────────────────────────────────────
    private readonly List<double> _sessionScores = [];

    private readonly Random _rng = new();

    // ── UI 상태 ─────────────────────────────────────────────────────────
    private bool _joystickConnected;

    // ── 저장 경로 ─────────────────────────────────────────────────────────
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DodgeBlitz", "best.txt");

    // ── 생성자 ────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyDarkTitleBar();
            try
            {
                var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"));
                if (sri != null) Icon = BitmapFrame.Create(sri.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch { }
        };

        _bestTime = LoadBestTime();

        // Sounds 정적 초기화를 백그라운드에서 미리 수행 (첫 StartGame 시 UI 스레드 블로킹 방지)
        Task.Run(() => _ = Sounds.Bgm);

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

        _survivalTime  = 0;
        _spawnTimer    = 0.5;
        _prevLevel     = "";
        _milestoneIndex = 0;
        _milestoneTimer = 0;
        MilestoneText.Visibility = Visibility.Collapsed;

        _player = new Player(AreaW, AreaH);
        GameCanvas.Children.Add(_player.Visual!);

        TitlePanel.Visibility    = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        PausePanel.Visibility    = Visibility.Collapsed;
        HudPanel.Visibility      = Visibility.Visible;

        _state = GameState.Playing;
        _input.Reset();

        SoundGen.PlayBgm(Sounds.Bgm, 0.28);
    }

    // ── 게임 종료 ─────────────────────────────────────────────────────────
    private void EndGame()
    {
        _state = GameState.GameOver;

        bool isNewBest = _survivalTime > _bestTime;
        if (isNewBest)
        {
            _bestTime = _survivalTime;
            SaveBestTime(_bestTime);
        }

        _sessionScores.Add(_survivalTime);
        _joystick.Rumble(0.35);

        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.HitSfx);

        // 딜레이 후 게임오버 SFX
        System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            { try { Dispatcher.InvokeAsync(() => SoundGen.Sfx(Sounds.GameOverSfx)); } catch { } });

        // 파티클 폭발
        SpawnParticles(_player!.X + _player.Width / 2, _player.Y + _player.Height / 2,
                       24, Color.FromRgb(0, 255, 204));

        MilestoneText.Visibility = Visibility.Collapsed;

        FinalTimeText.Text = $"SURVIVED  {_survivalTime:F1}s";
        FinalBestText.Text = isNewBest
            ? $"NEW BEST!  {_bestTime:F1}s"
            : $"BEST  {_bestTime:F1}s";

        // Top 5 히스토리
        var top5 = _sessionScores.OrderByDescending(s => s).Take(5).ToList();
        if (top5.Count > 1)
        {
            HistoryText.Text = "─ Session Top " + top5.Count + " ─\n"
                + string.Join("\n", top5.Select((s, i) => $"  {i + 1}. {s:F1}s"));
        }
        else
        {
            HistoryText.Text = "";
        }

        GameOverPanel.Visibility = Visibility.Visible;
        HudPanel.Visibility      = Visibility.Collapsed;
        UpdateTitleBest();
    }

    // ── 일시정지 ──────────────────────────────────────────────────────────
    private void TogglePause()
    {
        if (_state == GameState.Playing)
        {
            _state = GameState.Paused;
            _engine.Pause();
            SoundGen.PauseBgm();
            PausePanel.Visibility = Visibility.Visible;
        }
        else if (_state == GameState.Paused)
        {
            _state = GameState.Playing;
            _engine.Resume();
            SoundGen.ResumeBgm();
            PausePanel.Visibility = Visibility.Collapsed;
        }
    }

    // ── 게임 루프 ─────────────────────────────────────────────────────────
    private void OnUpdate(double dt)
    {
        // 조이스틱 폴링 — 방향 동기화 + 버튼 처리
        _joystick.Poll();
        _input.JoyLeft  = _joystick.Left;
        _input.JoyRight = _joystick.Right;
        _input.JoyUp    = _joystick.Up;
        _input.JoyDown  = _joystick.Down;

        if (_joystick.StartJustPressed && _state is GameState.Title or GameState.GameOver)
            StartGame();
        if (_joystick.BackJustPressed  && _state == GameState.Playing)
            BackToTitle();

        // 조이스틱 연결 상태 변경 시 UI 업데이트
        if (_joystick.IsConnected != _joystickConnected)
        {
            _joystickConnected = _joystick.IsConnected;
            JoystickStatusText.Text = _joystickConnected ? "🎮  Controller Connected" : "";
        }

        foreach (var s in _stars) s.Update(dt);

        if (_state == GameState.Playing)
            UpdateGame(dt);
    }

    private void OnRender()
    {
        foreach (var s in _stars) s.SyncPosition();

        if (_state is GameState.Playing or GameState.GameOver or GameState.Paused)
        {
            _player?.SyncPosition();
            foreach (var b in _bullets)   b.SyncPosition();
            foreach (var p in _particles) p.SyncPosition();
        }
    }

    private void UpdateGame(double dt)
    {
        if (_player is null) return;

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

        // 마일스톤 팝업 체크
        if (_milestoneIndex < Milestones.Length && _survivalTime >= Milestones[_milestoneIndex])
        {
            ShowMilestone($"{Milestones[_milestoneIndex]:F0}s!");
            _milestoneIndex++;
        }

        // 마일스톤 타이머
        if (_milestoneTimer > 0)
        {
            _milestoneTimer -= dt;
            if (_milestoneTimer <= 0)
                MilestoneText.Visibility = Visibility.Collapsed;
        }

        UpdateHud();
    }

    // ── 마일스톤 팝업 ─────────────────────────────────────────────────────
    private void ShowMilestone(string text)
    {
        MilestoneText.Text       = text;
        MilestoneText.Visibility = Visibility.Visible;
        _milestoneTimer          = 1.4;
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

    private static readonly int[] _patternsNovice   = [0];
    private static readonly int[] _patternsSkilled  = [0, 0, 2];
    private static readonly int[] _patternsExpert   = [0, 1, 2, 3];
    private static readonly int[] _patternsLegend   = [0, 1, 2, 3, 4];

    private int[] GetActivePatterns() => _survivalTime switch
    {
        < 10  => _patternsNovice,
        < 20  => _patternsSkilled,
        < 35  => _patternsExpert,
        _     => _patternsLegend
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

    private static Color GetLevelColor(string level) => level switch
    {
        "NOVICE"  => Color.FromRgb(0,   255, 204), // 시안
        "SKILLED" => Color.FromRgb(255, 215,   0), // 황금
        "EXPERT"  => Color.FromRgb(255, 136,   0), // 주황
        "VETERAN" => Color.FromRgb(255, 102, 170), // 핑크
        _         => Color.FromRgb(255,  68,  68)  // 빨강 (LEGEND)
    };

    // 추적형 총알 (플레이어 방향 + 오프셋)
    private void SpawnTracking(double speed, Color color)
    {
        var (sx, sy) = RandomEdgePoint();
        double px = _player!.X + _player.Width  / 2;
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
        double px = _player!.X + _player.Width  / 2;
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
        TimeText.Text  = $"{_survivalTime:F1}";
        BestText.Text  = $"{Math.Max(_bestTime, _survivalTime):F1}";

        var lv    = GetLevelName();
        var color = GetLevelColor(lv);
        LevelText.Text       = lv;
        LevelText.Foreground = new SolidColorBrush(color);
        LevelGlow.Color      = color;
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
            if (HelpPanel.Visibility == Visibility.Visible)
                HelpPanel.Visibility = Visibility.Collapsed;
            else if (_state is GameState.Title or GameState.GameOver)
                StartGame();
            else if (_state == GameState.Paused)
                TogglePause();
        }
        else if (e.Key == Key.P)
        {
            if (_state is GameState.Playing or GameState.Paused)
                TogglePause();
        }
        else if (e.Key == Key.H)
        {
            if (_state == GameState.Title || HelpPanel.Visibility == Visibility.Visible)
                HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (e.Key == Key.M)
        {
            SoundGen.ToggleMute();
        }
        else if (e.Key == Key.Escape)
        {
            if (HelpPanel.Visibility == Visibility.Visible)
                HelpPanel.Visibility = Visibility.Collapsed;
            else if (_state == GameState.Playing)
                BackToTitle();
            else if (_state == GameState.Paused)
                BackToTitle();
            else
                Close();
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        _input.KeyUp(e.Key);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _engine.Stop();
        SoundGen.StopBgm();
    }

    private void BackToTitle()
    {
        if (_state == GameState.Paused)
            _engine.Resume();

        SoundGen.StopBgm();

        foreach (var b in _bullets)   GameCanvas.Children.Remove(b.Visual);
        foreach (var p in _particles) GameCanvas.Children.Remove(p.Visual);
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);

        _bullets.Clear();
        _particles.Clear();

        _state = GameState.Title;
        HudPanel.Visibility      = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        PausePanel.Visibility    = Visibility.Collapsed;
        MilestoneText.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility    = Visibility.Visible;
        UpdateTitleBest();
    }

    // ── 최고 기록 영속화 ──────────────────────────────────────────────────
    private static double LoadBestTime()
    {
        try
        {
            if (File.Exists(SavePath) &&
                double.TryParse(File.ReadAllText(SavePath), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                return v;
        }
        catch { }
        return 0;
    }

    private static void SaveBestTime(double time)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath, time.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch { }
    }
}
