using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DungeonDash.Engine;
using DungeonDash.Entities;

namespace DungeonDash;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const double ViewW = 804;
    private const double ViewH = 602;
    private const double TileSize = 20;
    private const int MaxFloor = 5;

    private readonly GameLoop _loop = new();
    private readonly KeyInput _input = new();
    private readonly Random _rng = new();
    private readonly DungeonGenerator _dungeon;

    private Player _player = null!;
    private readonly List<Monster> _monsters = [];
    private readonly List<Pickup> _pickups = [];
    private readonly List<DamageText> _dmgTexts = [];
    private Tile[,] _map = null!;

    // 렌더링
    private readonly List<Rectangle> _tileRects = [];
    private Rectangle? _skillCircle;

    // 카메라
    private double _camX, _camY;

    // 상태
    private enum GameState { Title, Playing, Paused, FloorTransition, GameOver, Clear }
    private GameState _state = GameState.Title;
    private int _highScore;
    private double _transitionTimer;

    public MainWindow()
    {
        _dungeon = new DungeonGenerator(_rng);

        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int value = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
            }

            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 게임 시작 ──────────────────────────────────────

    private void StartGame()
    {
        ClearEntities();

        _player = new Player(_input, 0, 0);
        _player.Floor = 1;
        _player.Score = 0;
        _player.Atk = 15;
        _player.MaxHp = 100;
        _player.Hp = 100;
        _player.DashSpeed = 450;

        GenerateFloor(_player.Floor);

        GameCanvas.Children.Add(_player.Visual);

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        ClearOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        FloorTransition.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        MiniMapCanvas.Visibility = Visibility.Visible;

        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void GenerateFloor(int floor)
    {
        // 기존 타일/몬스터/아이템 제거
        foreach (var r in _tileRects) GameCanvas.Children.Remove(r);
        _tileRects.Clear();
        foreach (var m in _monsters) GameCanvas.Children.Remove(m.Visual);
        _monsters.Clear();
        foreach (var p in _pickups) GameCanvas.Children.Remove(p.Visual);
        _pickups.Clear();
        foreach (var d in _dmgTexts) GameCanvas.Children.Remove(d.Visual);
        _dmgTexts.Clear();
        if (_skillCircle is not null) { GameCanvas.Children.Remove(_skillCircle); _skillCircle = null; }

        _map = _dungeon.Generate(floor);

        // 타일 렌더링
        for (int x = 0; x < _dungeon.Width; x++)
        {
            for (int y = 0; y < _dungeon.Height; y++)
            {
                var tile = _map[x, y];
                if (tile == Tile.Wall) continue; // 벽은 어둠으로 보임

                var color = tile switch
                {
                    Tile.Floor => Color.FromRgb(0x22, 0x22, 0x38),
                    Tile.StairsDown => Color.FromRgb(0x44, 0x33, 0x00),
                    Tile.Chest => Color.FromRgb(0x55, 0x44, 0x00),
                    _ => Color.FromRgb(0x22, 0x22, 0x38)
                };

                var rect = new Rectangle
                {
                    Width = TileSize, Height = TileSize,
                    Fill = new SolidColorBrush(color),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                    StrokeThickness = 0.5
                };
                _tileRects.Add(rect);
                GameCanvas.Children.Add(rect);

                // 계단/상자 아이콘
                if (tile is Tile.StairsDown or Tile.Chest)
                {
                    var icon = new TextBlock
                    {
                        Text = tile == Tile.StairsDown ? "▼" : "◆",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(tile == Tile.StairsDown
                            ? Color.FromRgb(0xFF, 0xD7, 0x00)
                            : Color.FromRgb(0xFF, 0xAA, 0x00)),
                        FontFamily = new FontFamily("Consolas")
                    };
                    Canvas.SetLeft(icon, x * TileSize + 4);
                    Canvas.SetTop(icon, y * TileSize + 2);
                    _tileRects.Add(null!); // placeholder
                    GameCanvas.Children.Add(icon);
                }
            }
        }

        // 플레이어 위치 (첫 번째 방 중앙)
        if (_dungeon.Rooms.Count > 0)
        {
            var room = _dungeon.Rooms[0];
            _player.X = (room.X + room.W / 2) * TileSize;
            _player.Y = (room.Y + room.H / 2) * TileSize;
        }

        // 몬스터 스폰
        SpawnMonsters(floor);

        // 보스 HP 표시
        var boss = _monsters.Find(m => m.IsBoss);
        if (boss is not null)
        {
            BossHpPanel.Visibility = Visibility.Visible;
            BossNameText.Text = $"★ {boss.Kind.ToString().ToUpper()} BOSS ★";
        }
        else
        {
            BossHpPanel.Visibility = Visibility.Collapsed;
        }

        DrawMiniMap();
    }

    private void SpawnMonsters(int floor)
    {
        // 각 방(첫 번째 제외)에 몬스터 배치
        for (int i = 1; i < _dungeon.Rooms.Count; i++)
        {
            var room = _dungeon.Rooms[i];
            int count = 1 + _rng.Next(2) + floor / 2;

            // 마지막 방 (계단 있는 곳)은 보스 또는 강적
            bool isLastRoom = i == _dungeon.Rooms.Count - 1;

            for (int j = 0; j < count; j++)
            {
                double mx = (room.X + 1 + _rng.Next(Math.Max(1, room.W - 2))) * TileSize;
                double my = (room.Y + 1 + _rng.Next(Math.Max(1, room.H - 2))) * TileSize;

                MonsterKind kind;
                bool isBoss = false;

                if (isLastRoom && j == 0 && floor >= 3)
                {
                    kind = floor >= 5 ? MonsterKind.Dragon : MonsterKind.Demon;
                    isBoss = true;
                }
                else
                {
                    kind = floor switch
                    {
                        1 => _rng.NextDouble() < 0.7 ? MonsterKind.Slime : MonsterKind.Skeleton,
                        2 => _rng.NextDouble() < 0.5 ? MonsterKind.Skeleton : MonsterKind.Ghost,
                        3 => (MonsterKind)_rng.Next(3), // Slime~Ghost
                        4 => _rng.NextDouble() < 0.6 ? MonsterKind.Ghost : MonsterKind.Demon,
                        _ => _rng.NextDouble() < 0.4 ? MonsterKind.Demon : MonsterKind.Ghost
                    };
                }

                var monster = new Monster(kind, mx, my, _rng, isBoss);
                _monsters.Add(monster);
                GameCanvas.Children.Add(monster.Visual);
            }
        }
    }

    // ── 게임 루프 ──────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state == GameState.FloorTransition)
        {
            _transitionTimer -= dt;
            if (_transitionTimer <= 0)
            {
                FloorTransition.Visibility = Visibility.Collapsed;
                _state = GameState.Playing;
            }
            return;
        }

        if (_state != GameState.Playing) return;

        _player.Update(dt, _map);

        // 몬스터
        foreach (var m in _monsters)
            m.Update(dt, _player, _map);

        // 전투
        HandleCombat();

        // 아이템 수집
        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            if (!_pickups[i].Collected && _player.Bounds.IntersectsWith(_pickups[i].Bounds))
            {
                _pickups[i].Apply(_player);
                GameCanvas.Children.Remove(_pickups[i].Visual);
                _pickups.RemoveAt(i);
                SoundGen.Sfx(Sounds.ItemPickupSfx);
            }
        }

        // 데미지 텍스트
        for (int i = _dmgTexts.Count - 1; i >= 0; i--)
        {
            _dmgTexts[i].Update(dt);
            if (!_dmgTexts[i].IsAlive)
            {
                GameCanvas.Children.Remove(_dmgTexts[i].Visual);
                _dmgTexts.RemoveAt(i);
            }
        }

        // 스킬 이펙트
        if (_player.IsSkilling)
        {
            if (_skillCircle is null)
            {
                _skillCircle = new Rectangle
                {
                    Width = 80, Height = 80,
                    Fill = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xD7, 0x00)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                    StrokeThickness = 2,
                    RadiusX = 40, RadiusY = 40
                };
                GameCanvas.Children.Add(_skillCircle);
                SoundGen.Sfx(Sounds.AttackSfx);

                // 범위 내 몬스터에 데미지
                double cx = _player.X + Player.Size / 2;
                double cy = _player.Y + Player.Size / 2;
                foreach (var m in _monsters)
                {
                    if (!m.IsAlive) continue;
                    double dist = Math.Sqrt(Math.Pow(m.X + m.Size / 2 - cx, 2) + Math.Pow(m.Y + m.Size / 2 - cy, 2));
                    if (dist < 50)
                    {
                        int dmg = _player.Atk * 2;
                        m.TakeDamage(dmg);
                        SpawnDmgText(m.X, m.Y - 10, dmg, false);
                    }
                }
            }
        }
        else if (_skillCircle is not null)
        {
            GameCanvas.Children.Remove(_skillCircle);
            _skillCircle = null;
        }

        // 죽은 몬스터 처리
        for (int i = _monsters.Count - 1; i >= 0; i--)
        {
            if (!_monsters[i].IsAlive)
            {
                _player.Score += _monsters[i].ScoreValue;

                // 아이템 드롭
                if (_rng.NextDouble() < 0.35)
                {
                    var kind = _rng.NextDouble() < 0.6 ? PickupKind.Health
                        : _rng.NextDouble() < 0.5 ? PickupKind.AtkBoost : PickupKind.SpeedBoost;
                    var pickup = new Pickup(kind, _monsters[i].X, _monsters[i].Y);
                    _pickups.Add(pickup);
                    GameCanvas.Children.Add(pickup.Visual);
                }

                GameCanvas.Children.Remove(_monsters[i].Visual);
                _monsters.RemoveAt(i);
            }
        }

        // 계단 체크
        int ptx = _player.TileX, pty = _player.TileY;
        if (ptx >= 0 && ptx < _dungeon.Width && pty >= 0 && pty < _dungeon.Height)
        {
            if (_map[ptx, pty] == Tile.StairsDown)
            {
                _player.Floor++;
                if (_player.Floor > MaxFloor)
                {
                    // 클리어!
                    _state = GameState.Clear;
                    HudPanel.Visibility = Visibility.Collapsed;
                    MiniMapCanvas.Visibility = Visibility.Collapsed;
                    BossHpPanel.Visibility = Visibility.Collapsed;
                    ClearScoreText.Text = $"SCORE: {_player.Score}";
                    if (_player.Score > _highScore) _highScore = _player.Score;
                    ClearOverlay.Visibility = Visibility.Visible;
                    SoundGen.StopBgm();
                    SoundGen.Sfx(Sounds.StairsSfx);
                }
                else
                {
                    // 다음 층
                    GameCanvas.Children.Remove(_player.Visual);
                    GenerateFloor(_player.Floor);
                    GameCanvas.Children.Add(_player.Visual);
                    FloorTransitionText.Text = $"B{_player.Floor}F";
                    FloorTransition.Visibility = Visibility.Visible;
                    _state = GameState.FloorTransition;
                    _transitionTimer = 1.2;
                    SoundGen.Sfx(Sounds.StairsSfx);
                }
            }
            // 상자 열기
            else if (_map[ptx, pty] == Tile.Chest)
            {
                _map[ptx, pty] = Tile.Floor;
                _player.Score += 200;
                SoundGen.Sfx(Sounds.DoorSfx);

                var kind = _rng.NextDouble() < 0.5 ? PickupKind.Health
                    : _rng.NextDouble() < 0.5 ? PickupKind.AtkBoost : PickupKind.SpeedBoost;
                var pickup = new Pickup(kind, ptx * TileSize, pty * TileSize);
                _pickups.Add(pickup);
                GameCanvas.Children.Add(pickup.Visual);
            }
        }

        // 게임오버
        if (!_player.IsAlive)
        {
            _state = GameState.GameOver;
            HudPanel.Visibility = Visibility.Collapsed;
            MiniMapCanvas.Visibility = Visibility.Collapsed;
            BossHpPanel.Visibility = Visibility.Collapsed;
            DeathFloorText.Text = $"B{_player.Floor}F에서 쓰러졌다...";
            FinalScoreText.Text = $"SCORE: {_player.Score}";
            if (_player.Score > _highScore) _highScore = _player.Score;
            GameOverOverlay.Visibility = Visibility.Visible;
            SoundGen.StopBgm();
            SoundGen.Sfx(Sounds.DeathSfx);
        }

        // HUD
        UpdateHud();

        // 카메라
        _camX = _player.X + Player.Size / 2 - ViewW / 2;
        _camY = _player.Y + Player.Size / 2 - ViewH / 2;
    }

    private void HandleCombat()
    {
        // 플레이어 → 몬스터
        if (_player.IsAttacking && _player.AttackHitbox is { } hitbox)
        {
            foreach (var m in _monsters)
            {
                if (!m.IsAlive) continue;
                if (hitbox.IntersectsWith(m.Bounds))
                {
                    m.TakeDamage(_player.Atk);
                    SpawnDmgText(m.X, m.Y - 10, _player.Atk, false);
                    SoundGen.Sfx(Sounds.AttackSfx);
                }
            }
        }

        // 몬스터 → 플레이어
        foreach (var m in _monsters)
        {
            if (!m.IsAlive) continue;
            if (m.CanAttack(_player))
            {
                _player.TakeDamage(m.Atk);
                SpawnDmgText(_player.X, _player.Y - 10, m.Atk, true);
                SoundGen.Sfx(Sounds.MonsterHitSfx);
            }
        }
    }

    private void SpawnDmgText(double x, double y, int dmg, bool isPlayerDmg)
    {
        var txt = new DamageText(x, y, dmg, isPlayerDmg);
        _dmgTexts.Add(txt);
        GameCanvas.Children.Add(txt.Visual);
    }

    private void UpdateHud()
    {
        double hpRatio = Math.Max(0, (double)_player.Hp / _player.MaxHp);
        PlayerHpBar.Width = 180 * hpRatio;
        PlayerHpBar.Background = hpRatio > 0.5
            ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
            : hpRatio > 0.25
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00))
                : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

        HpText.Text = $"{_player.Hp}/{_player.MaxHp}";
        FloorText.Text = $"B{_player.Floor}F";
        ScoreText.Text = $"SCORE: {_player.Score}";
        AtkText.Text = $"ATK: {_player.Atk}";
        SkillText.Text = _player.SkillReady ? "[C] SKILL: READY" : "[C] SKILL: ...";
        SkillText.Foreground = new SolidColorBrush(_player.SkillReady
            ? Color.FromRgb(0x00, 0xFF, 0xCC) : Color.FromRgb(0x66, 0x66, 0x66));

        // 보스 HP
        var boss = _monsters.Find(m => m.IsBoss);
        if (boss is not null)
        {
            double bossRatio = Math.Max(0, (double)boss.Hp / boss.MaxHp);
            BossHpBar.Width = 280 * bossRatio;
        }
    }

    private void OnRender()
    {
        if (_state is not (GameState.Playing or GameState.Paused or GameState.FloorTransition))
            return;

        // 타일 위치 동기화
        int idx = 0;
        for (int x = 0; x < _dungeon.Width; x++)
        {
            for (int y = 0; y < _dungeon.Height; y++)
            {
                if (_map[x, y] == Tile.Wall) continue;

                if (idx < _tileRects.Count && _tileRects[idx] is { } rect)
                {
                    Canvas.SetLeft(rect, x * TileSize - _camX);
                    Canvas.SetTop(rect, y * TileSize - _camY);
                }
                idx++;

                if (_map[x, y] is Tile.StairsDown or Tile.Chest)
                    idx++; // skip icon placeholder
            }
        }

        // 엔티티 동기화
        _player.SyncPosition(_camX, _camY);
        foreach (var m in _monsters) m.SyncPosition(_camX, _camY);
        foreach (var p in _pickups) p.SyncPosition(_camX, _camY);
        foreach (var d in _dmgTexts) d.SyncPosition(_camX, _camY);

        if (_skillCircle is not null)
        {
            Canvas.SetLeft(_skillCircle, _player.X + Player.Size / 2 - 40 - _camX);
            Canvas.SetTop(_skillCircle, _player.Y + Player.Size / 2 - 40 - _camY);
        }

        UpdateMiniMap();
    }

    // ── 미니맵 ──────────────────────────────────────────

    private void DrawMiniMap()
    {
        MiniMapCanvas.Children.Clear();

        var bg = new Rectangle
        {
            Width = 120, Height = 90,
            Fill = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
            StrokeThickness = 1,
            RadiusX = 4, RadiusY = 4
        };
        MiniMapCanvas.Children.Add(bg);

        double scaleX = 118.0 / _dungeon.Width;
        double scaleY = 88.0 / _dungeon.Height;

        for (int x = 0; x < _dungeon.Width; x++)
        {
            for (int y = 0; y < _dungeon.Height; y++)
            {
                if (_map[x, y] == Tile.Wall) continue;
                var dot = new Rectangle
                {
                    Width = Math.Max(2, scaleX),
                    Height = Math.Max(2, scaleY),
                    Fill = new SolidColorBrush(_map[x, y] == Tile.StairsDown
                        ? Color.FromRgb(0xFF, 0xD7, 0x00)
                        : Color.FromRgb(0x44, 0x44, 0x55))
                };
                Canvas.SetLeft(dot, 1 + x * scaleX);
                Canvas.SetTop(dot, 1 + y * scaleY);
                MiniMapCanvas.Children.Add(dot);
            }
        }
    }

    private void UpdateMiniMap()
    {
        double scaleX = 118.0 / _dungeon.Width;
        double scaleY = 88.0 / _dungeon.Height;

        // 기존 플레이어 dot 제거 후 추가 (간단 구현)
        // 미니맵은 DrawMiniMap에서 정적으로 그리고, 플레이어 위치만 갱신
        // 여기서는 간단히 마지막 child로 처리
        if (MiniMapCanvas.Children.Count > 0 && MiniMapCanvas.Children[^1] is Ellipse existingDot)
            MiniMapCanvas.Children.Remove(existingDot);

        var playerDot = new Ellipse
        {
            Width = 4, Height = 4,
            Fill = new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF))
        };
        Canvas.SetLeft(playerDot, 1 + _player.TileX * scaleX - 1);
        Canvas.SetTop(playerDot, 1 + _player.TileY * scaleY - 1);
        MiniMapCanvas.Children.Add(playerDot);
    }

    // ── 입력 ──────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _input.Press(e.Key);

        switch (e.Key)
        {
            case Key.Enter when _state is GameState.Title or GameState.GameOver or GameState.Clear:
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
                SoundGen.StopBgm();
                ShowTitle();
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) => _input.Release(e.Key);

    // ── 유틸 ──────────────────────────────────────────

    private void ClearEntities()
    {
        foreach (var r in _tileRects) if (r is not null) GameCanvas.Children.Remove(r);
        _tileRects.Clear();
        foreach (var m in _monsters) GameCanvas.Children.Remove(m.Visual);
        _monsters.Clear();
        foreach (var p in _pickups) GameCanvas.Children.Remove(p.Visual);
        _pickups.Clear();
        foreach (var d in _dmgTexts) GameCanvas.Children.Remove(d.Visual);
        _dmgTexts.Clear();
        if (_player?.Visual is not null) GameCanvas.Children.Remove(_player.Visual);
        if (_skillCircle is not null) { GameCanvas.Children.Remove(_skillCircle); _skillCircle = null; }

        // 아이콘 등 나머지 정리
        var toRemove = new List<UIElement>();
        foreach (UIElement child in GameCanvas.Children)
        {
            if (child is TextBlock tb && tb.FontFamily?.Source == "Consolas")
                toRemove.Add(child);
        }
        foreach (var el in toRemove) GameCanvas.Children.Remove(el);
    }

    private void ShowTitle()
    {
        ClearEntities();
        MiniMapCanvas.Children.Clear();

        HudPanel.Visibility = Visibility.Collapsed;
        MiniMapCanvas.Visibility = Visibility.Collapsed;
        BossHpPanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        ClearOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        FloorTransition.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        HighScoreTitle.Text = _highScore > 0 ? $"HIGH SCORE: {_highScore}" : "";

        _input.Reset();
        _loop.Start();
    }
}
