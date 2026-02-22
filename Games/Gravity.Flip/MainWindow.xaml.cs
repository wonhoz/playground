using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using GravityFlip.Engine;
using GravityFlip.Entities;

namespace GravityFlip;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const double ViewW = 584;
    private const double ViewH = 462;
    private const int MaxLevel = 8;

    private readonly GameLoop _loop = new();
    private readonly Random _rng = new();

    // Game state
    private enum GameState { Title, Playing, Dead, LevelComplete, GameComplete }
    private GameState _state = GameState.Title;

    // Entities
    private readonly Player _player = new();
    private Level _level = null!;
    private int _currentLevel = 1;
    private int _totalCoins;
    private int _totalDeaths;

    // Camera
    private double _cameraX;

    // Death
    private double _deathTimer;
    private const double DeathDelay = 1.0;

    // Visual elements
    private Rectangle? _playerRect;
    private readonly List<Rectangle> _platformRects = [];
    private readonly List<Polygon> _hazardPolys = [];
    private readonly List<Ellipse> _coinEllipses = [];
    private Rectangle? _portalRect;
    private double _portalPulse;

    // Particles
    private readonly List<(Rectangle Rect, double Vx, double Vy, double Life)> _particles = [];

    // Background stars
    private readonly List<(Rectangle Rect, double X, double Y)> _stars = [];

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

            CreateStars();

            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── Stars ────────────────────────────────────────────

    private void CreateStars()
    {
        for (int i = 0; i < 60; i++)
        {
            double x = _rng.NextDouble() * ViewW;
            double y = _rng.NextDouble() * ViewH;
            double size = 1 + _rng.NextDouble() * 2;
            byte brightness = (byte)(30 + _rng.Next(40));
            var rect = new Rectangle
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(Color.FromRgb(brightness, brightness, (byte)(brightness + 20))),
                Opacity = 0.3 + _rng.NextDouble() * 0.4
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            GameCanvas.Children.Add(rect);
            _stars.Add((rect, x, y));
        }
    }

    // ── Start / Load Level ───────────────────────────────

    private void StartGame()
    {
        _currentLevel = 1;
        _totalCoins = 0;
        _totalDeaths = 0;
        LoadLevel(_currentLevel);
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void LoadLevel(int levelNum)
    {
        ClearLevelVisuals();

        _level = Level.Create(levelNum);
        _level.ResetAll();

        _player.Reset(50, Level.FloorY - Player.Size);
        _cameraX = 0;
        _deathTimer = 0;

        // Build visuals
        BuildPlatformVisuals();
        BuildHazardVisuals();
        BuildCoinVisuals();
        BuildPortalVisual();
        BuildPlayerVisual();

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        LevelCompleteOverlay.Visibility = Visibility.Collapsed;
        GameCompleteOverlay.Visibility = Visibility.Collapsed;
        DeathFlash.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        GravityIndicator.Visibility = Visibility.Visible;
        SoundGen.Sfx(Sounds.LevelStartSfx);
        UpdateHud();
    }

    private void ClearLevelVisuals()
    {
        foreach (var r in _platformRects) GameCanvas.Children.Remove(r);
        _platformRects.Clear();
        foreach (var p in _hazardPolys) GameCanvas.Children.Remove(p);
        _hazardPolys.Clear();
        foreach (var e in _coinEllipses) GameCanvas.Children.Remove(e);
        _coinEllipses.Clear();
        foreach (var (rect, _, _, _) in _particles) GameCanvas.Children.Remove(rect);
        _particles.Clear();

        if (_portalRect is not null) { GameCanvas.Children.Remove(_portalRect); _portalRect = null; }
        if (_playerRect is not null) { GameCanvas.Children.Remove(_playerRect); _playerRect = null; }
    }

    // ── Build Visuals ────────────────────────────────────

    private void BuildPlayerVisual()
    {
        _playerRect = new Rectangle
        {
            Width = Player.Size, Height = Player.Size,
            Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xCC)),
            RadiusX = 3, RadiusY = 3,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        GameCanvas.Children.Add(_playerRect);
    }

    private void BuildPlatformVisuals()
    {
        foreach (var plat in _level.Platforms)
        {
            var color = plat.Type switch
            {
                PlatformType.Normal => Color.FromRgb(0x3A, 0x3A, 0x5A),
                PlatformType.Moving => Color.FromRgb(0x4A, 0x6A, 0x4A),
                PlatformType.Crumbling => Color.FromRgb(0x6A, 0x4A, 0x3A),
                PlatformType.Bouncy => Color.FromRgb(0x6A, 0x3A, 0x6A),
                _ => Color.FromRgb(0x3A, 0x3A, 0x5A)
            };

            var rect = new Rectangle
            {
                Width = plat.Width, Height = plat.Height,
                Fill = new SolidColorBrush(color),
                RadiusX = 2, RadiusY = 2
            };
            _platformRects.Add(rect);
            GameCanvas.Children.Add(rect);
        }
    }

    private void BuildHazardVisuals()
    {
        foreach (var haz in _level.Hazards)
        {
            var poly = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
            };

            if (haz.PointsUp)
            {
                poly.Points =
                [
                    new Point(0, haz.Height),
                    new Point(haz.Width / 2, 0),
                    new Point(haz.Width, haz.Height)
                ];
            }
            else
            {
                poly.Points =
                [
                    new Point(0, 0),
                    new Point(haz.Width / 2, haz.Height),
                    new Point(haz.Width, 0)
                ];
            }

            _hazardPolys.Add(poly);
            GameCanvas.Children.Add(poly);
        }
    }

    private void BuildCoinVisuals()
    {
        foreach (var coin in _level.Coins)
        {
            var ell = new Ellipse
            {
                Width = Coin.Size, Height = Coin.Size,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            };
            _coinEllipses.Add(ell);
            GameCanvas.Children.Add(ell);
        }
    }

    private void BuildPortalVisual()
    {
        _portalRect = new Rectangle
        {
            Width = 24, Height = 40,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0xAA)),
            RadiusX = 6, RadiusY = 6
        };
        GameCanvas.Children.Add(_portalRect);
    }

    // ── Update ───────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state == GameState.Dead)
        {
            _deathTimer -= dt;
            if (_deathTimer <= 0)
            {
                DeathFlash.Visibility = Visibility.Collapsed;
                LoadLevel(_currentLevel);
            }
            UpdateParticles(dt);
            return;
        }

        if (_state != GameState.Playing) return;

        // Auto scroll
        _player.X += _level.ScrollSpeed * dt;
        _player.Update(dt);

        // Update platforms
        foreach (var plat in _level.Platforms)
            plat.Update(dt);

        // Camera follows player
        _cameraX = _player.X - 100;
        if (_cameraX < 0) _cameraX = 0;

        // Collision with platforms
        ResolveCollisions();

        // Check hazards
        CheckHazards();

        // Check coins
        CheckCoins();

        // Check portal
        CheckPortal();

        // Check out of bounds (fell off level)
        if (_player.Y > Level.FloorY + 100 || _player.Y < Level.CeilingY - 100)
            KillPlayer();

        // Check if player passed beyond level without portal
        if (_player.X > _level.LevelWidth + 100)
            KillPlayer();

        // Update particles
        UpdateParticles(dt);

        // Portal pulse
        _portalPulse += dt * 3;

        // HUD
        UpdateHud();
    }

    private void ResolveCollisions()
    {
        _player.IsGrounded = false;

        double px = _player.X;
        double py = _player.Y;
        double pw = Player.Size;
        double ph = Player.Size;

        foreach (var plat in _level.Platforms)
        {
            if (plat.IsDestroyed) continue;

            double platX = plat.X;
            double platY = plat.Y;
            double platW = plat.Width;
            double platH = plat.Height;

            // AABB overlap check
            if (px + pw <= platX || px >= platX + platW ||
                py + ph <= platY || py >= platY + platH)
                continue;

            // Determine which side the player hit from
            double overlapLeft = (px + pw) - platX;
            double overlapRight = (platX + platW) - px;
            double overlapTop = (py + ph) - platY;
            double overlapBottom = (platY + platH) - py;

            double minOverlapX = Math.Min(overlapLeft, overlapRight);
            double minOverlapY = Math.Min(overlapTop, overlapBottom);

            if (minOverlapY < minOverlapX)
            {
                if (overlapTop < overlapBottom)
                {
                    // Landing on top (gravity down)
                    _player.Y = platY - ph;
                    if (_player.GravityDown)
                    {
                        if (_player.VelocityY >= 0)
                        {
                            if (plat.Type == PlatformType.Bouncy)
                            {
                                _player.VelocityY = -350;
                            }
                            else
                            {
                                _player.VelocityY = 0;
                                _player.IsGrounded = true;
                            }
                        }
                        if (plat.Type == PlatformType.Crumbling)
                            plat.StartCrumble();
                    }
                }
                else
                {
                    // Hitting bottom (gravity up)
                    _player.Y = platY + platH;
                    if (!_player.GravityDown)
                    {
                        if (_player.VelocityY <= 0)
                        {
                            if (plat.Type == PlatformType.Bouncy)
                            {
                                _player.VelocityY = 350;
                            }
                            else
                            {
                                _player.VelocityY = 0;
                                _player.IsGrounded = true;
                            }
                        }
                        if (plat.Type == PlatformType.Crumbling)
                            plat.StartCrumble();
                    }
                }
            }
        }
    }

    private void CheckHazards()
    {
        double px = _player.X;
        double py = _player.Y;
        double pw = Player.Size;
        double ph = Player.Size;

        foreach (var haz in _level.Hazards)
        {
            if (px + pw > haz.X + 2 && px < haz.X + haz.Width - 2 &&
                py + ph > haz.Y + 2 && py < haz.Y + haz.Height - 2)
            {
                KillPlayer();
                return;
            }
        }
    }

    private void CheckCoins()
    {
        double px = _player.X;
        double py = _player.Y;
        double pw = Player.Size;
        double ph = Player.Size;

        for (int i = 0; i < _level.Coins.Count; i++)
        {
            var coin = _level.Coins[i];
            if (coin.Collected) continue;

            if (px + pw > coin.X && px < coin.X + Coin.Size &&
                py + ph > coin.Y && py < coin.Y + Coin.Size)
            {
                coin.Collected = true;
                _totalCoins++;
                SoundGen.Sfx(Sounds.CoinSfx);
                if (i < _coinEllipses.Count)
                    _coinEllipses[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    private void CheckPortal()
    {
        double px = _player.X;
        double py = _player.Y;
        double pw = Player.Size;
        double ph = Player.Size;

        if (px + pw > _level.PortalX && px < _level.PortalX + 24 &&
            py + ph > _level.PortalY && py < _level.PortalY + 40)
        {
            SoundGen.Sfx(Sounds.PortalSfx);
            SoundGen.StopBgm();
            if (_currentLevel >= MaxLevel)
            {
                _state = GameState.GameComplete;
                HudPanel.Visibility = Visibility.Collapsed;
                GravityIndicator.Visibility = Visibility.Collapsed;
                GameCompleteStats.Text = $"COINS: {_totalCoins}  |  DEATHS: {_totalDeaths}";
                GameCompleteOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                _state = GameState.LevelComplete;
                HudPanel.Visibility = Visibility.Collapsed;
                GravityIndicator.Visibility = Visibility.Collapsed;
                LevelCompleteCoins.Text = $"COINS: {_totalCoins}";
                LevelCompleteOverlay.Visibility = Visibility.Visible;
            }
        }
    }

    private void KillPlayer()
    {
        if (_player.IsDead) return;
        _player.IsDead = true;
        _totalDeaths++;
        _state = GameState.Dead;
        _deathTimer = DeathDelay;
        DeathFlash.Visibility = Visibility.Visible;
        SoundGen.Sfx(Sounds.DeathSfx);

        // Spawn death particles
        SpawnParticles(_player.X + Player.Size / 2, _player.Y + Player.Size / 2, 15,
            Color.FromRgb(0xE7, 0x4C, 0x3C));
    }

    // ── Particles ────────────────────────────────────────

    private void SpawnParticles(double x, double y, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 60 + _rng.NextDouble() * 140;
            double vx = Math.Cos(angle) * speed;
            double vy = Math.Sin(angle) * speed;
            double size = 2 + _rng.NextDouble() * 4;

            var rect = new Rectangle
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(color),
                Opacity = 0.9
            };
            GameCanvas.Children.Add(rect);
            _particles.Add((rect, vx, vy, 0.6 + _rng.NextDouble() * 0.4));
        }
    }

    private void SpawnFlipParticles()
    {
        SpawnParticles(_player.X + Player.Size / 2, _player.Y + Player.Size / 2, 8,
            Color.FromRgb(0x00, 0xFF, 0xCC));
    }

    private void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (rect, vx, vy, life) = _particles[i];
            double newLife = life - dt;
            if (newLife <= 0)
            {
                GameCanvas.Children.Remove(rect);
                _particles.RemoveAt(i);
                continue;
            }

            double cx = Canvas.GetLeft(rect) + vx * dt;
            double cy = Canvas.GetTop(rect) + vy * dt;
            Canvas.SetLeft(rect, cx);
            Canvas.SetTop(rect, cy);
            rect.Opacity = Math.Max(0, newLife / 1.0);
            _particles[i] = (rect, vx, vy, newLife);
        }
    }

    // ── Render ───────────────────────────────────────────

    private void OnRender()
    {
        if (_state is GameState.Title or GameState.GameComplete)
            return;

        double camX = _cameraX;

        // Stars parallax
        for (int i = 0; i < _stars.Count; i++)
        {
            var (rect, sx, sy) = _stars[i];
            double drawX = sx - camX * 0.05;
            drawX = ((drawX % ViewW) + ViewW) % ViewW;
            Canvas.SetLeft(rect, drawX);
            Canvas.SetTop(rect, sy);
        }

        // Platforms
        for (int i = 0; i < _level.Platforms.Count && i < _platformRects.Count; i++)
        {
            var plat = _level.Platforms[i];
            var rect = _platformRects[i];

            if (plat.IsDestroyed)
            {
                rect.Visibility = Visibility.Collapsed;
                continue;
            }

            rect.Visibility = Visibility.Visible;
            Canvas.SetLeft(rect, plat.X - camX);
            Canvas.SetTop(rect, plat.Y);
            rect.Width = plat.Width;

            if (plat.Type == PlatformType.Crumbling && plat.IsCrumbling)
                rect.Opacity = Math.Max(0.2, plat.CrumbleTimer / 0.5);
            else
                rect.Opacity = 1.0;
        }

        // Hazards
        for (int i = 0; i < _level.Hazards.Count && i < _hazardPolys.Count; i++)
        {
            Canvas.SetLeft(_hazardPolys[i], _level.Hazards[i].X - camX);
            Canvas.SetTop(_hazardPolys[i], _level.Hazards[i].Y);
        }

        // Coins
        for (int i = 0; i < _level.Coins.Count && i < _coinEllipses.Count; i++)
        {
            if (_level.Coins[i].Collected)
            {
                _coinEllipses[i].Visibility = Visibility.Collapsed;
                continue;
            }
            _coinEllipses[i].Visibility = Visibility.Visible;
            Canvas.SetLeft(_coinEllipses[i], _level.Coins[i].X - camX);
            Canvas.SetTop(_coinEllipses[i], _level.Coins[i].Y);
        }

        // Portal
        if (_portalRect is not null)
        {
            Canvas.SetLeft(_portalRect, _level.PortalX - camX);
            Canvas.SetTop(_portalRect, _level.PortalY);
            double pulse = 0.7 + 0.3 * Math.Sin(_portalPulse);
            _portalRect.Opacity = pulse;
        }

        // Player
        if (_playerRect is not null)
        {
            if (_player.IsDead)
            {
                _playerRect.Visibility = Visibility.Collapsed;
            }
            else
            {
                _playerRect.Visibility = Visibility.Visible;
                Canvas.SetLeft(_playerRect, _player.X - camX);
                Canvas.SetTop(_playerRect, _player.Y);

                // Rotate 45 degrees for diamond look when flipping
                double targetAngle = _player.GravityDown ? 0 : 45;
                _playerRect.RenderTransform = new RotateTransform(targetAngle);
            }
        }

        // Gravity indicator
        GravityIndicator.Text = _player.GravityDown ? "GRAVITY: DOWN" : "GRAVITY: UP";
    }

    // ── HUD ──────────────────────────────────────────────

    private void UpdateHud()
    {
        LevelText.Text = $"LEVEL {_currentLevel}";
        CoinText.Text = _totalCoins.ToString();
        DeathText.Text = _totalDeaths.ToString();
    }

    // ── Input ────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _state == GameState.Title:
                StartGame();
                break;

            case Key.Enter when _state == GameState.LevelComplete:
                _currentLevel++;
                LoadLevel(_currentLevel);
                SoundGen.PlayBgm(Sounds.Bgm);
                break;

            case Key.Enter when _state == GameState.GameComplete:
                _state = GameState.Title;
                GameCompleteOverlay.Visibility = Visibility.Collapsed;
                ShowTitle();
                break;

            case Key.Space when _state == GameState.Playing:
                _player.FlipGravity();
                SpawnFlipParticles();
                SoundGen.Sfx(Sounds.FlipSfx);
                break;

            case Key.R when _state == GameState.Playing:
                _totalDeaths++;
                LoadLevel(_currentLevel);
                break;

            case Key.Escape when _state is GameState.Playing or GameState.LevelComplete:
                ShowTitle();
                break;
        }
    }

    private void ShowTitle()
    {
        _state = GameState.Title;
        SoundGen.StopBgm();
        ClearLevelVisuals();
        HudPanel.Visibility = Visibility.Collapsed;
        GravityIndicator.Visibility = Visibility.Collapsed;
        LevelCompleteOverlay.Visibility = Visibility.Collapsed;
        GameCompleteOverlay.Visibility = Visibility.Collapsed;
        DeathFlash.Visibility = Visibility.Collapsed;

        if (_totalCoins > 0 || _totalDeaths > 0)
            TitleStats.Text = $"LAST RUN - COINS: {_totalCoins}  DEATHS: {_totalDeaths}";
        else
            TitleStats.Text = "";

        TitlePanel.Visibility = Visibility.Visible;
    }
}
