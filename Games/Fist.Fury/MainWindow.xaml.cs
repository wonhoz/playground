using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using FistFury.Engine;
using FistFury.Entities;

namespace FistFury;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const double ArenaW = 784;
    private const double ArenaH = 522;
    private const double FloorY = 420; // Fighter.GroundY 와 일치

    private readonly GameLoop _loop = new();
    private readonly KeyInput _input = new();
    private readonly Random _rng = new();

    private Player _player = null!;
    private readonly List<Enemy> _enemies = [];
    private readonly List<HitEffect> _effects = [];

    private enum GameState { Title, Playing, Paused, WaveClear, GameOver, Victory }
    private GameState _state = GameState.Title;

    private int _highScore;
    private int _wave;
    private double _waveClearTimer;
    private bool _bossWave;

    // 웨이브 정의: (EnemyKind, Count)[]
    private static readonly (EnemyKind Kind, int Count)[][] WaveDefs =
    [
        [(EnemyKind.Thug, 3)],
        [(EnemyKind.Thug, 3), (EnemyKind.Ninja, 1)],
        [(EnemyKind.Thug, 2), (EnemyKind.Ninja, 2), (EnemyKind.Brute, 1)],
        [(EnemyKind.Ninja, 3), (EnemyKind.Brute, 2)],
        [(EnemyKind.Boss, 1)], // 보스전
    ];

    // 배경
    private Rectangle _floor = null!;
    private Rectangle _bgCity = null!;

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

            DrawBackground();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 배경 ──────────────────────────────────────────

    private void DrawBackground()
    {
        // 도시 실루엣 (배경)
        _bgCity = new Rectangle
        {
            Width = ArenaW,
            Height = 200,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x0D, 0x0D, 0x2B),
                Color.FromRgb(0x1A, 0x1A, 0x3E), 90)
        };
        Canvas.SetTop(_bgCity, FloorY - 200);
        GameCanvas.Children.Add(_bgCity);

        // 빌딩 실루엣들
        DrawBuildings();

        // 바닥
        _floor = new Rectangle
        {
            Width = ArenaW,
            Height = ArenaH - FloorY,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x2A, 0x2A, 0x44),
                Color.FromRgb(0x1A, 0x1A, 0x30), 90)
        };
        Canvas.SetTop(_floor, FloorY + 80); // Fighter 높이 감안
        GameCanvas.Children.Add(_floor);

        // 바닥 라인
        var floorLine = new Rectangle
        {
            Width = ArenaW, Height = 2,
            Fill = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66))
        };
        Canvas.SetTop(floorLine, FloorY + 80);
        GameCanvas.Children.Add(floorLine);
    }

    private void DrawBuildings()
    {
        var buildingColor = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x30));
        var windowColor = new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xCC, 0x44));

        double[] positions = [20, 100, 200, 320, 430, 550, 650, 720];
        double[] widths = [60, 80, 50, 90, 70, 55, 65, 50];
        double[] heights = [120, 160, 90, 140, 110, 130, 100, 150];

        for (int i = 0; i < positions.Length; i++)
        {
            var bld = new Rectangle
            {
                Width = widths[i],
                Height = heights[i],
                Fill = buildingColor,
                RadiusX = 2, RadiusY = 2
            };
            Canvas.SetLeft(bld, positions[i]);
            Canvas.SetTop(bld, FloorY + 80 - heights[i]);
            GameCanvas.Children.Add(bld);

            // 창문
            for (double wy = FloorY + 80 - heights[i] + 10; wy < FloorY + 70; wy += 18)
            {
                for (double wx = positions[i] + 8; wx < positions[i] + widths[i] - 10; wx += 14)
                {
                    if (_rng.NextDouble() < 0.4) continue;
                    var win = new Rectangle
                    {
                        Width = 6, Height = 8,
                        Fill = windowColor
                    };
                    Canvas.SetLeft(win, wx);
                    Canvas.SetTop(win, wy);
                    GameCanvas.Children.Add(win);
                }
            }
        }
    }

    // ── 게임 시작/초기화 ──────────────────────────────

    private void StartGame()
    {
        // 클리어
        foreach (var e in _enemies) GameCanvas.Children.Remove(e.Visual);
        foreach (var fx in _effects) GameCanvas.Children.Remove(fx.Visual);
        _enemies.Clear();
        _effects.Clear();

        if (_player?.Visual is not null)
            GameCanvas.Children.Remove(_player.Visual);

        _player = new Player(_input, 100);
        GameCanvas.Children.Add(_player.Visual);

        _wave = 0;
        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        BossHpPanel.Visibility = Visibility.Collapsed;

        NextWave();
    }

    private void NextWave()
    {
        _wave++;
        if (_wave > WaveDefs.Length)
        {
            // 전체 클리어!
            _state = GameState.Victory;
            HudPanel.Visibility = Visibility.Collapsed;
            BossHpPanel.Visibility = Visibility.Collapsed;
            VictoryScoreText.Text = $"FINAL SCORE: {_player.Score}";
            if (_player.Score > _highScore) _highScore = _player.Score;
            VictoryOverlay.Visibility = Visibility.Visible;
            return;
        }

        WaveText.Text = $"WAVE {_wave}";
        _bossWave = false;

        var waveDef = WaveDefs[_wave - 1];
        foreach (var (kind, count) in waveDef)
        {
            if (kind == EnemyKind.Boss) _bossWave = true;
            for (int i = 0; i < count; i++)
            {
                // 적 스폰 위치: 플레이어 반대쪽 + 랜덤 오프셋
                double spawnX = _rng.NextDouble() < 0.5
                    ? ArenaW - 80 - _rng.NextDouble() * 100
                    : 50 + _rng.NextDouble() * 100;

                var enemy = new Enemy(kind, spawnX, _player, _rng);
                _enemies.Add(enemy);
                GameCanvas.Children.Add(enemy.Visual);
            }
        }

        // 웨이브 시작 텍스트
        if (_bossWave)
        {
            BossHpPanel.Visibility = Visibility.Visible;
            BossNameText.Text = "★ BOSS ★";
        }
    }

    // ── 게임 루프 ──────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state == GameState.WaveClear)
        {
            _waveClearTimer -= dt;
            if (_waveClearTimer <= 0)
            {
                WaveClearText.Visibility = Visibility.Collapsed;
                _state = GameState.Playing;
                NextWave();
            }
            return;
        }

        if (_state != GameState.Playing) return;

        // 플레이어 업데이트
        _player.Update(dt);

        // 적 업데이트
        foreach (var enemy in _enemies)
            enemy.Update(dt);

        // 충돌 감지
        CheckCombat(dt);

        // 이펙트 업데이트
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            _effects[i].Update(dt);
            if (!_effects[i].IsAlive)
            {
                GameCanvas.Children.Remove(_effects[i].Visual);
                _effects.RemoveAt(i);
            }
        }

        // 죽은 적 제거
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (!_enemies[i].IsAlive)
            {
                _player.Score += _enemies[i].ScoreValue;
                SpawnHitEffect(_enemies[i].X + _enemies[i].Width / 2,
                    _enemies[i].Y + _enemies[i].Height / 2, true);
                GameCanvas.Children.Remove(_enemies[i].Visual);
                _enemies.RemoveAt(i);
            }
        }

        // HUD 업데이트
        UpdateHud();

        // 웨이브 클리어 체크
        if (_enemies.Count == 0 && _state == GameState.Playing)
        {
            _state = GameState.WaveClear;
            _waveClearTimer = 1.5;
            WaveClearText.Text = _wave >= WaveDefs.Length ? "ALL CLEAR!" : $"WAVE {_wave} CLEAR!";
            WaveClearText.Visibility = Visibility.Visible;
        }

        // 게임오버 체크
        if (!_player.IsAlive)
        {
            _state = GameState.GameOver;
            HudPanel.Visibility = Visibility.Collapsed;
            BossHpPanel.Visibility = Visibility.Collapsed;
            FinalScoreText.Text = $"SCORE: {_player.Score}";
            if (_player.Score > _highScore)
            {
                _highScore = _player.Score;
                NewHighScoreText.Text = "★ NEW HIGH SCORE! ★";
            }
            else
            {
                NewHighScoreText.Text = "";
            }
            GameOverOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CheckCombat(double dt)
    {
        // 플레이어 → 적
        foreach (var enemy in _enemies)
        {
            if (_player.TryHit(enemy))
            {
                SpawnHitEffect(
                    (enemy.X + _player.X + _player.Width) / 2,
                    enemy.Y + enemy.Height / 2,
                    _player.State == FighterState.Special);
            }
        }

        // 적 → 플레이어
        foreach (var enemy in _enemies)
        {
            if (enemy.TryHit(_player))
            {
                SpawnHitEffect(
                    _player.X + _player.Width / 2,
                    _player.Y + _player.Height / 2,
                    false);
            }
        }
    }

    private void SpawnHitEffect(double x, double y, bool isBig)
    {
        var color = isBig ? Color.FromRgb(0xFF, 0xD7, 0x00) : Color.FromRgb(0xFF, 0x66, 0x00);
        var fx = new HitEffect(x, y, color, isBig);
        _effects.Add(fx);
        GameCanvas.Children.Add(fx.Visual);
    }

    private void UpdateHud()
    {
        ScoreText.Text = $"SCORE: {_player.Score}";

        // 플레이어 HP 바
        double hpRatio = Math.Max(0, (double)_player.Hp / _player.MaxHp);
        PlayerHpBar.Width = 220 * hpRatio;
        PlayerHpBar.Background = hpRatio > 0.5
            ? new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF))
            : hpRatio > 0.25
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00))
                : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

        // 콤보 표시
        if (_player.ComboCount >= 2)
            ComboText.Text = $"{_player.ComboCount} COMBO!";
        else
            ComboText.Text = "";

        // 보스 HP
        if (_bossWave && _enemies.Count > 0)
        {
            var boss = _enemies.Find(e => e.Kind == EnemyKind.Boss);
            if (boss is not null)
            {
                double bossRatio = Math.Max(0, (double)boss.Hp / boss.MaxHp);
                BossHpBar.Width = 300 * bossRatio;
            }
        }
    }

    private void OnRender()
    {
        if (_state is GameState.Playing or GameState.Paused or GameState.WaveClear)
        {
            _player.SyncPosition();
            foreach (var e in _enemies) e.SyncPosition();
        }
    }

    // ── 입력 ──────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _input.Press(e.Key);

        switch (e.Key)
        {
            case Key.Enter when _state is GameState.Title or GameState.GameOver or GameState.Victory:
                StartGame();
                break;
            case Key.P when _state == GameState.Playing:
                _state = GameState.Paused;
                _loop.Pause();
                PauseOverlay.Visibility = Visibility.Visible;
                break;
            case Key.P when _state == GameState.Paused:
                _state = GameState.Playing;
                _loop.Resume();
                PauseOverlay.Visibility = Visibility.Collapsed;
                break;
            case Key.Escape when _state == GameState.Playing:
                _loop.Stop();
                _state = GameState.Title;
                ShowTitle();
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) => _input.Release(e.Key);

    private void ShowTitle()
    {
        foreach (var e in _enemies) GameCanvas.Children.Remove(e.Visual);
        foreach (var fx in _effects) GameCanvas.Children.Remove(fx.Visual);
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);
        _enemies.Clear(); _effects.Clear();

        HudPanel.Visibility = Visibility.Collapsed;
        BossHpPanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        WaveClearText.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        HighScoreTitle.Text = _highScore > 0 ? $"HIGH SCORE: {_highScore}" : "";

        _input.Reset();
        _loop.Start();
    }
}
