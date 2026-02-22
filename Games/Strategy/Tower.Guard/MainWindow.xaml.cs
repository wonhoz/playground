using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TowerGuard.Engine;
using TowerGuard.Entities;

namespace TowerGuard;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const double TileSize = GridMap.TileSize;

    private readonly GameLoop _loop = new();
    private readonly GridMap _map = new();
    private readonly WaveManager _waveManager = new();

    private readonly List<Enemy> _enemies = [];
    private readonly List<Tower> _towers = [];
    private readonly List<Projectile> _projectiles = [];

    // Visual tracking
    private readonly Dictionary<Enemy, Shape> _enemyVisuals = [];
    private readonly Dictionary<Enemy, Rectangle> _enemyHpBars = [];
    private readonly Dictionary<Enemy, Rectangle> _enemyHpBgBars = [];
    private readonly Dictionary<Tower, Shape> _towerVisuals = [];
    private readonly Dictionary<Tower, TextBlock> _towerLevelTexts = [];
    private readonly Dictionary<Projectile, Ellipse> _projectileVisuals = [];

    private int _gold = 200;
    private int _lives = 20;

    private TowerType? _selectedTowerType;
    private Tower? _contextTower;
    private double _enemySpeedMult = 1.0; // 타이틀 속도 선택 배율

    // Range indicator
    private Ellipse? _rangeIndicator;

    private enum GameState { Title, Playing, Victory, GameOver }
    private GameState _state = GameState.Title;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
        }

        _waveManager.OnSpawnEnemy += SpawnEnemy;
        _waveManager.OnWaveComplete += OnWaveComplete;
        _waveManager.OnAllWavesComplete += OnAllWavesComplete;

        _loop.OnUpdate += OnUpdate;
        _loop.OnRender += OnRender;
        _loop.Start();
        Focus();
    }

    // ── Game Start ──────────────────────────────────────

    private void StartGame()
    {
        ClearAll();

        _gold = 200;
        _lives = 20;
        _selectedTowerType = TowerType.Arrow;
        _contextTower = null;

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        TowerPanel.Visibility = Visibility.Visible;
        TowerContextMenu.Visibility = Visibility.Collapsed;

        DrawGrid();
        UpdateSelectedTowerText();

        _waveManager.StartFirstWave();
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void ClearAll()
    {
        foreach (var v in _enemyVisuals.Values) GameCanvas.Children.Remove(v);
        foreach (var v in _enemyHpBars.Values) GameCanvas.Children.Remove(v);
        foreach (var v in _enemyHpBgBars.Values) GameCanvas.Children.Remove(v);
        foreach (var v in _towerVisuals.Values) GameCanvas.Children.Remove(v);
        foreach (var v in _towerLevelTexts.Values) GameCanvas.Children.Remove(v);
        foreach (var v in _projectileVisuals.Values) GameCanvas.Children.Remove(v);

        _enemies.Clear();
        _towers.Clear();
        _projectiles.Clear();
        _enemyVisuals.Clear();
        _enemyHpBars.Clear();
        _enemyHpBgBars.Clear();
        _towerVisuals.Clear();
        _towerLevelTexts.Clear();
        _projectileVisuals.Clear();

        if (_rangeIndicator is not null)
        {
            GameCanvas.Children.Remove(_rangeIndicator);
            _rangeIndicator = null;
        }

        GameCanvas.Children.Clear();
    }

    // ── Grid Drawing ──────────────────────────────────────

    private void DrawGrid()
    {
        for (int x = 0; x < GridMap.Cols; x++)
        {
            for (int y = 0; y < GridMap.Rows; y++)
            {
                var tile = _map.Tiles[x, y];
                Color fill;
                Color stroke;

                switch (tile)
                {
                    case TileType.Path:
                    case TileType.Start:
                    case TileType.End:
                        fill = (Color)ColorConverter.ConvertFromString("#2A1A0A");
                        stroke = (Color)ColorConverter.ConvertFromString("#3A2A1A");
                        break;
                    default: // Buildable
                        fill = (Color)ColorConverter.ConvertFromString("#1A1A2E");
                        stroke = (Color)ColorConverter.ConvertFromString("#2A2A3A");
                        break;
                }

                var rect = new Rectangle
                {
                    Width = TileSize,
                    Height = TileSize,
                    Fill = new SolidColorBrush(fill),
                    Stroke = new SolidColorBrush(stroke),
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(rect, x * TileSize);
                Canvas.SetTop(rect, y * TileSize);
                GameCanvas.Children.Add(rect);

                // Start/End markers
                if (tile == TileType.Start)
                {
                    var txt = new TextBlock
                    {
                        Text = "S",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFCC")),
                        FontFamily = new FontFamily("Consolas")
                    };
                    Canvas.SetLeft(txt, x * TileSize + 8);
                    Canvas.SetTop(txt, y * TileSize + 5);
                    GameCanvas.Children.Add(txt);
                }
                else if (tile == TileType.End)
                {
                    var txt = new TextBlock
                    {
                        Text = "E",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF66AA")),
                        FontFamily = new FontFamily("Consolas")
                    };
                    Canvas.SetLeft(txt, x * TileSize + 8);
                    Canvas.SetTop(txt, y * TileSize + 5);
                    GameCanvas.Children.Add(txt);
                }
            }
        }
    }

    // ── Enemy Spawning ──────────────────────────────────

    private void SpawnEnemy(EnemyType type)
    {
        var enemy = new Enemy(type, _map.PathTiles, _waveManager.HpMultiplier);
        enemy.Speed *= _enemySpeedMult;
        _enemies.Add(enemy);

        var size = type == EnemyType.Boss ? 20.0 : type == EnemyType.Tank ? 16.0 : 12.0;
        var visual = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(enemy.ColorHex))
        };

        _enemyVisuals[enemy] = visual;
        GameCanvas.Children.Add(visual);

        // HP bar background
        var hpBg = new Rectangle
        {
            Width = 20,
            Height = 3,
            Fill = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x40))
        };
        _enemyHpBgBars[enemy] = hpBg;
        GameCanvas.Children.Add(hpBg);

        // HP bar
        var hpBar = new Rectangle
        {
            Width = 20,
            Height = 3,
            Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
        };
        _enemyHpBars[enemy] = hpBar;
        GameCanvas.Children.Add(hpBar);
    }

    // ── Tower Placement ──────────────────────────────────

    private void PlaceTower(int gridX, int gridY)
    {
        if (_selectedTowerType is not { } towerType) return;

        int cost = Tower.GetCost(towerType);
        if (_gold < cost) return;

        // Check not already occupied
        if (_towers.Any(t => t.GridX == gridX && t.GridY == gridY)) return;

        _gold -= cost;
        SoundGen.Sfx(Sounds.TowerPlaceSfx);
        var tower = new Tower(towerType, gridX, gridY);
        _towers.Add(tower);

        // Visual
        var size = 24.0;
        var rect = new Rectangle
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tower.ColorHex)),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1,
            RadiusX = 4,
            RadiusY = 4,
            Opacity = 0.9
        };
        Canvas.SetLeft(rect, gridX * TileSize + (TileSize - size) / 2);
        Canvas.SetTop(rect, gridY * TileSize + (TileSize - size) / 2);
        _towerVisuals[tower] = rect;
        GameCanvas.Children.Add(rect);

        // Level text
        var lvlText = new TextBlock
        {
            Text = "1",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas")
        };
        Canvas.SetLeft(lvlText, gridX * TileSize + 12);
        Canvas.SetTop(lvlText, gridY * TileSize + 10);
        _towerLevelTexts[tower] = lvlText;
        GameCanvas.Children.Add(lvlText);
    }

    // ── Game Loop ──────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        // Wave manager
        int aliveCount = _enemies.Count(e => e.IsAlive && !e.ReachedEnd);
        _waveManager.Update(dt, aliveCount);

        // Update enemies
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            enemy.Update(dt);

            if (enemy.ReachedEnd)
            {
                _lives--;
                RemoveEnemy(enemy);
                _enemies.RemoveAt(i);

                if (_lives <= 0)
                {
                    _lives = 0;
                    _state = GameState.GameOver;
                    ShowGameOver();
                    return;
                }
                continue;
            }

            if (!enemy.IsAlive)
            {
                _gold += enemy.Reward;
                SoundGen.Sfx(Sounds.EnemyDeathSfx);
                RemoveEnemy(enemy);
                _enemies.RemoveAt(i);
            }
        }

        // Healer logic
        foreach (var enemy in _enemies)
        {
            if (enemy.Type != EnemyType.Healer || !enemy.IsAlive) continue;
            foreach (var other in _enemies)
            {
                if (other == enemy || !other.IsAlive || other.Hp >= other.MaxHp) continue;
                double dx = other.X - enemy.X;
                double dy = other.Y - enemy.Y;
                if (dx * dx + dy * dy < 64 * 64)
                {
                    other.Heal(5.0 * dt);
                }
            }
        }

        // Tower firing
        foreach (var tower in _towers)
        {
            tower.FireCooldown -= dt;
            if (tower.FireCooldown > 0) continue;

            // Find target in range (closest to end = highest path progress)
            Enemy? target = null;
            double bestProgress = -1;

            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive || enemy.ReachedEnd) continue;
                double dx = enemy.X - tower.CenterX;
                double dy = enemy.Y - tower.CenterY;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist <= tower.Range && enemy.PathProgress > bestProgress)
                {
                    bestProgress = enemy.PathProgress;
                    target = enemy;
                }
            }

            if (target is null) continue;

            tower.FireCooldown = tower.FireRate;
            SoundGen.Sfx(Sounds.TowerShootSfx);

            if (tower.Type == TowerType.Lightning)
            {
                // Chain lightning: damage multiple enemies
                var hit = new HashSet<Enemy> { target };
                target.TakeDamage(tower.Damage);
                var current = target;

                for (int chain = 1; chain < tower.ChainCount; chain++)
                {
                    Enemy? next = null;
                    double bestDist = double.MaxValue;
                    foreach (var e in _enemies)
                    {
                        if (!e.IsAlive || e.ReachedEnd || hit.Contains(e)) continue;
                        double cdx = e.X - current.X;
                        double cdy = e.Y - current.Y;
                        double cdist = Math.Sqrt(cdx * cdx + cdy * cdy);
                        if (cdist < 96 && cdist < bestDist)
                        {
                            bestDist = cdist;
                            next = e;
                        }
                    }
                    if (next is null) break;
                    next.TakeDamage(tower.Damage * 0.7);
                    hit.Add(next);
                    current = next;
                }

                // Visual: projectile to first target only
                var proj = new Projectile(tower, target);
                _projectiles.Add(proj);
                AddProjectileVisual(proj);
            }
            else
            {
                var proj = new Projectile(tower, target);
                _projectiles.Add(proj);
                AddProjectileVisual(proj);
            }
        }

        // Update projectiles
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var proj = _projectiles[i];
            proj.Update(dt);

            if (!proj.IsAlive)
            {
                // Apply hit effects
                ApplyProjectileHit(proj);
                RemoveProjectileVisual(proj);
                _projectiles.RemoveAt(i);
            }
        }
    }

    private void ApplyProjectileHit(Projectile proj)
    {
        if (!proj.Target.IsAlive && proj.Source.Type != TowerType.Cannon) return;

        switch (proj.Source.Type)
        {
            case TowerType.Arrow:
                proj.Target.TakeDamage(proj.Damage);
                break;

            case TowerType.Cannon:
                // Area damage
                double splashPx = proj.Source.SplashRadius * TileSize;
                foreach (var e in _enemies)
                {
                    if (!e.IsAlive || e.ReachedEnd) continue;
                    double dx = e.X - proj.Target.X;
                    double dy = e.Y - proj.Target.Y;
                    if (dx * dx + dy * dy <= splashPx * splashPx)
                    {
                        e.TakeDamage(proj.Damage);
                    }
                }
                break;

            case TowerType.Ice:
                proj.Target.TakeDamage(proj.Damage);
                if (proj.Target.IsAlive)
                {
                    proj.Target.ApplySlow(proj.Source.SlowFactor, proj.Source.SlowDuration);
                }
                break;

            case TowerType.Lightning:
                // Chain damage already applied at fire time
                break;
        }
    }

    private void AddProjectileVisual(Projectile proj)
    {
        var visual = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(proj.ColorHex))
        };
        _projectileVisuals[proj] = visual;
        GameCanvas.Children.Add(visual);
    }

    private void RemoveProjectileVisual(Projectile proj)
    {
        if (_projectileVisuals.Remove(proj, out var visual))
        {
            GameCanvas.Children.Remove(visual);
        }
    }

    private void RemoveEnemy(Enemy enemy)
    {
        if (_enemyVisuals.Remove(enemy, out var visual))
            GameCanvas.Children.Remove(visual);
        if (_enemyHpBars.Remove(enemy, out var hpBar))
            GameCanvas.Children.Remove(hpBar);
        if (_enemyHpBgBars.Remove(enemy, out var hpBg))
            GameCanvas.Children.Remove(hpBg);
    }

    // ── Render ──────────────────────────────────────

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // Update enemy positions
        foreach (var enemy in _enemies)
        {
            if (_enemyVisuals.TryGetValue(enemy, out var visual))
            {
                Canvas.SetLeft(visual, enemy.X - visual.Width / 2);
                Canvas.SetTop(visual, enemy.Y - visual.Height / 2);
            }
            if (_enemyHpBgBars.TryGetValue(enemy, out var hpBg))
            {
                Canvas.SetLeft(hpBg, enemy.X - 10);
                Canvas.SetTop(hpBg, enemy.Y - 14);
            }
            if (_enemyHpBars.TryGetValue(enemy, out var hpBar))
            {
                double ratio = Math.Max(0, enemy.Hp / enemy.MaxHp);
                hpBar.Width = 20 * ratio;
                hpBar.Fill = ratio > 0.5
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                    : ratio > 0.25
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00))
                        : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                Canvas.SetLeft(hpBar, enemy.X - 10);
                Canvas.SetTop(hpBar, enemy.Y - 14);
            }

            // Show slow effect
            if (_enemyVisuals.TryGetValue(enemy, out var ev) && ev is Ellipse ell)
            {
                ell.Stroke = enemy.SlowTimer > 0
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A86FF"))
                    : null;
                ell.StrokeThickness = enemy.SlowTimer > 0 ? 2 : 0;
            }
        }

        // Update projectile positions
        foreach (var proj in _projectiles)
        {
            if (_projectileVisuals.TryGetValue(proj, out var visual))
            {
                Canvas.SetLeft(visual, proj.X - 3);
                Canvas.SetTop(visual, proj.Y - 3);
            }
        }

        // HUD
        GoldText.Text = _gold.ToString();
        LivesText.Text = _lives.ToString();

        int displayWave = _waveManager.CurrentWave;
        if (displayWave < 1) displayWave = 1;
        WaveText.Text = $"{displayWave}/{_waveManager.TotalWaves}";

        if (_waveManager.WaitingForNextWave)
        {
            int sec = (int)Math.Ceiling(_waveManager.TimerUntilNextWave);
            WaveTimerText.Text = $"Next wave: {sec}s [SPACE]";
        }
        else if (_waveManager.WaveInProgress)
        {
            WaveTimerText.Text = "Wave in progress...";
        }
        else
        {
            WaveTimerText.Text = "";
        }
    }

    // ── Wave Events ──────────────────────────────────

    private void OnWaveComplete()
    {
        SoundGen.Sfx(Sounds.WaveStartSfx);
    }

    private void OnAllWavesComplete()
    {
        _state = GameState.Victory;
        ShowVictory();
    }

    private void ShowVictory()
    {
        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.VictorySfx);
        HudPanel.Visibility = Visibility.Collapsed;
        TowerPanel.Visibility = Visibility.Collapsed;
        TowerContextMenu.Visibility = Visibility.Collapsed;
        VictoryStatsText.Text = $"Lives: {_lives}  |  Gold: {_gold}  |  Towers: {_towers.Count}";
        VictoryOverlay.Visibility = Visibility.Visible;
    }

    private void ShowGameOver()
    {
        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.GameOverSfx);
        HudPanel.Visibility = Visibility.Collapsed;
        TowerPanel.Visibility = Visibility.Collapsed;
        TowerContextMenu.Visibility = Visibility.Collapsed;
        GameOverWaveText.Text = $"Defeated on wave {_waveManager.CurrentWave}";
        GameOverOverlay.Visibility = Visibility.Visible;
    }

    // ── Input ──────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _state is GameState.Title or GameState.GameOver or GameState.Victory:
                StartGame();
                break;
            case Key.Escape when _state == GameState.Playing:
                SoundGen.StopBgm();
                _loop.Stop();
                _state = GameState.Title;
                ShowTitle();
                break;
            case Key.Space when _state == GameState.Playing:
                _waveManager.ForceStartNextWave();
                break;
            case Key.D1 or Key.NumPad1 when _state == GameState.Playing:
                _selectedTowerType = TowerType.Arrow;
                UpdateSelectedTowerText();
                break;
            case Key.D2 or Key.NumPad2 when _state == GameState.Playing:
                _selectedTowerType = TowerType.Cannon;
                UpdateSelectedTowerText();
                break;
            case Key.D3 or Key.NumPad3 when _state == GameState.Playing:
                _selectedTowerType = TowerType.Ice;
                UpdateSelectedTowerText();
                break;
            case Key.D4 or Key.NumPad4 when _state == GameState.Playing:
                _selectedTowerType = TowerType.Lightning;
                UpdateSelectedTowerText();
                break;
        }
    }

    private void Window_MouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Playing) return;

        // Close context menu on any left click
        TowerContextMenu.Visibility = Visibility.Collapsed;
        RemoveRangeIndicator();

        // Check if click is on the game canvas
        var pos = e.GetPosition(GameCanvas);
        if (pos.X < 0 || pos.X >= GridMap.Cols * TileSize ||
            pos.Y < 0 || pos.Y >= GridMap.Rows * TileSize) return;

        int gridX = (int)(pos.X / TileSize);
        int gridY = (int)(pos.Y / TileSize);

        if (_map.IsBuildable(gridX, gridY) && !_towers.Any(t => t.GridX == gridX && t.GridY == gridY))
        {
            PlaceTower(gridX, gridY);
        }

        Focus();
    }

    private void Window_MouseRightDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Playing) return;

        var pos = e.GetPosition(GameCanvas);
        if (pos.X < 0 || pos.X >= GridMap.Cols * TileSize ||
            pos.Y < 0 || pos.Y >= GridMap.Rows * TileSize)
        {
            TowerContextMenu.Visibility = Visibility.Collapsed;
            RemoveRangeIndicator();
            return;
        }

        int gridX = (int)(pos.X / TileSize);
        int gridY = (int)(pos.Y / TileSize);

        var tower = _towers.Find(t => t.GridX == gridX && t.GridY == gridY);
        if (tower is null)
        {
            TowerContextMenu.Visibility = Visibility.Collapsed;
            RemoveRangeIndicator();
            return;
        }

        _contextTower = tower;
        ShowTowerContextMenu(tower, pos);
        e.Handled = true;
    }

    private void ShowTowerContextMenu(Tower tower, Point pos)
    {
        ContextTowerName.Text = $"{Tower.GetDisplayName(tower.Type)} Lv.{tower.Level}";
        ContextTowerName.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tower.ColorHex));
        ContextTowerStats.Text = $"DMG: {tower.Damage:F0}  Rate: {tower.FireRate:F1}s\nRange: {tower.Range / TileSize:F1} tiles";

        if (tower.CanUpgrade)
        {
            UpgradeBtn.Visibility = Visibility.Visible;
            UpgradeBtnText.Text = $"Upgrade ({tower.UpgradeCost}g)";
        }
        else
        {
            UpgradeBtn.Visibility = Visibility.Collapsed;
        }

        SellBtnText.Text = $"Sell (+{tower.SellValue}g)";

        // Position context menu near the tower
        double menuX = tower.GridX * TileSize + TileSize + GameCanvas.Margin.Left + 4;
        double menuY = tower.GridY * TileSize + GameCanvas.Margin.Top;

        // Keep in bounds
        if (menuX + 160 > ActualWidth) menuX = tower.GridX * TileSize + GameCanvas.Margin.Left - 164;
        if (menuY + 140 > ActualHeight) menuY = ActualHeight - 150;

        TowerContextMenu.Margin = new Thickness(menuX, menuY, 0, 0);
        TowerContextMenu.Visibility = Visibility.Visible;

        // Show range indicator
        ShowRangeIndicator(tower);
    }

    private void ShowRangeIndicator(Tower tower)
    {
        RemoveRangeIndicator();
        _rangeIndicator = new Ellipse
        {
            Width = tower.Range * 2,
            Height = tower.Range * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Color.FromArgb(15, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_rangeIndicator, tower.CenterX - tower.Range);
        Canvas.SetTop(_rangeIndicator, tower.CenterY - tower.Range);
        GameCanvas.Children.Add(_rangeIndicator);
    }

    private void RemoveRangeIndicator()
    {
        if (_rangeIndicator is not null)
        {
            GameCanvas.Children.Remove(_rangeIndicator);
            _rangeIndicator = null;
        }
    }

    private void UpgradeBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (_contextTower is null || !_contextTower.CanUpgrade) return;

        int cost = _contextTower.UpgradeCost;
        if (_gold < cost) return;

        _gold -= cost;
        _contextTower.Upgrade();

        // Update level text
        if (_towerLevelTexts.TryGetValue(_contextTower, out var lvlText))
        {
            lvlText.Text = _contextTower.Level.ToString();
        }

        // Refresh context menu
        ShowTowerContextMenu(_contextTower, new Point(0, 0));
        e.Handled = true;
    }

    private void SellBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (_contextTower is null) return;

        _gold += _contextTower.SellValue;

        // Remove visuals
        if (_towerVisuals.Remove(_contextTower, out var visual))
            GameCanvas.Children.Remove(visual);
        if (_towerLevelTexts.Remove(_contextTower, out var lvlText))
            GameCanvas.Children.Remove(lvlText);

        _towers.Remove(_contextTower);
        _contextTower = null;

        TowerContextMenu.Visibility = Visibility.Collapsed;
        RemoveRangeIndicator();
        e.Handled = true;
    }

    private void CloseContextMenu_Click(object sender, MouseButtonEventArgs e)
    {
        TowerContextMenu.Visibility = Visibility.Collapsed;
        RemoveRangeIndicator();
        _contextTower = null;
        e.Handled = true;
    }

    private void TowerBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag)
        {
            _selectedTowerType = Enum.Parse<TowerType>(tag);
            UpdateSelectedTowerText();
        }
        e.Handled = true;
    }

    private void UpdateSelectedTowerText()
    {
        if (_selectedTowerType is { } t)
        {
            SelectedTowerText.Text = $"Selected: {Tower.GetDisplayName(t)}";
            SelectedTowerText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(new Tower(t, 0, 0).ColorHex));
        }
    }

    private void ShowTitle()
    {
        ClearAll();
        HudPanel.Visibility = Visibility.Collapsed;
        TowerPanel.Visibility = Visibility.Collapsed;
        TowerContextMenu.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        _loop.Start();
    }

    // ── 타이틀 속도 선택 ──────────────────────────
    private void SpeedSlow_Click(object s, MouseButtonEventArgs e) { _enemySpeedMult = 0.65; UpdateSpeedButtons(); }
    private void SpeedNorm_Click(object s, MouseButtonEventArgs e) { _enemySpeedMult = 1.0;  UpdateSpeedButtons(); }
    private void SpeedFast_Click(object s, MouseButtonEventArgs e) { _enemySpeedMult = 1.5;  UpdateSpeedButtons(); }

    private void UpdateSpeedButtons()
    {
        var active = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
        var dim    = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66));
        SpeedSlowBtn.BorderBrush = _enemySpeedMult < 0.9 ? new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF)) : dim;
        SpeedNormBtn.BorderBrush = _enemySpeedMult is >= 0.9 and <= 1.1 ? active : dim;
        SpeedFastBtn.BorderBrush = _enemySpeedMult > 1.1 ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : dim;
        SpeedSlowBtn.BorderThickness = new Thickness(_enemySpeedMult < 0.9 ? 3 : 1);
        SpeedNormBtn.BorderThickness = new Thickness(_enemySpeedMult is >= 0.9 and <= 1.1 ? 3 : 1);
        SpeedFastBtn.BorderThickness = new Thickness(_enemySpeedMult > 1.1 ? 3 : 1);
    }
}
