using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using BrickBlitz.Engine;

namespace BrickBlitz;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── Constants ──────────────────────────────────────
    private const double CanvasW = 500;
    private const double CanvasH = 700;
    private const int MaxStages = 8;
    private const int StartLives = 3;
    private const double PowerUpDuration = 15.0;
    private const double SlowDuration = 10.0;
    private const double LaserCooldown = 0.4;
    private const double ParticleLifetime = 0.6;

    // ── State ──────────────────────────────────────────
    private enum GameState { Title, Playing, StageClear, GameOver, AllClear }
    private GameState _state = GameState.Title;

    private readonly GameLoop _loop = new();
    private readonly Random _rng = new();

    // Game objects
    private Paddle _paddle = null!;
    private readonly List<Ball> _balls = [];
    private readonly List<Brick> _bricks = [];
    private readonly List<PowerUp> _powerUps = [];
    private readonly List<Laser> _lasers = [];
    private readonly List<Particle> _particles = [];

    // Visuals
    private Rectangle? _paddleRect;
    private readonly List<Ellipse> _ballVisuals = [];
    private readonly List<Rectangle> _brickRects = [];
    private readonly List<(Rectangle rect, TextBlock label)> _powerUpVisuals = [];
    private readonly List<Rectangle> _laserVisuals = [];
    private readonly List<Ellipse> _particleVisuals = [];

    // Input
    private bool _keyLeft, _keyRight;
    private double _mouseX = -1;
    private bool _useMouseInput;

    // Game state
    private int _score;
    private int _lives;
    private int _stage;
    private int _highScore;
    private int _combo;
    private double _comboTimer;

    // Power-up timers
    private double _widePaddleTimer;
    private double _slowBallTimer;
    private double _laserTimer;
    private bool _hasLaser;
    private double _laserCooldownTimer;

    // Ball speed
    private const double NormalBallSpeed = 360;
    private const double SlowBallSpeed = 220;

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

        _loop.OnUpdate += OnUpdate;
        _loop.OnRender += OnRender;
        _loop.Start();
        Focus();
    }

    // ── Game Start / Stage Setup ──────────────────────

    private void StartGame()
    {
        _score = 0;
        _lives = StartLives;
        _stage = 1;
        _combo = 0;
        _comboTimer = 0;
        ClearPowerUpEffects();
        LoadStage(_stage);
        ShowPlaying();
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void LoadStage(int stage)
    {
        ClearAllVisuals();

        _paddle = new Paddle((CanvasW - 80) / 2, CanvasH - 50);
        _balls.Clear();
        _bricks.Clear();
        _powerUps.Clear();
        _lasers.Clear();
        _particles.Clear();
        ClearPowerUpEffects();

        // Create paddle visual
        _paddleRect = new Rectangle
        {
            Width = _paddle.Width,
            Height = _paddle.Height,
            RadiusX = 4,
            RadiusY = 4,
            Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xCC)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x00, 0xFF, 0xCC),
                BlurRadius = 12,
                ShadowDepth = 0
            }
        };
        GameCanvas.Children.Add(_paddleRect);

        // Create ball
        var ball = new Ball(_paddle.CenterX, _paddle.Y - 8) { Stuck = true, Speed = NormalBallSpeed };
        _balls.Add(ball);
        var ballVisual = CreateBallVisual();
        _ballVisuals.Add(ballVisual);
        GameCanvas.Children.Add(ballVisual);

        // Create bricks
        var brickData = StageGenerator.Generate(stage);
        foreach (var b in brickData)
        {
            _bricks.Add(b);
            var rect = CreateBrickVisual(b);
            _brickRects.Add(rect);
            GameCanvas.Children.Add(rect);
        }
    }

    private void NextStage()
    {
        _stage++;
        if (_stage > MaxStages)
        {
            if (_score > _highScore) _highScore = _score;
            SoundGen.StopBgm();
            SoundGen.Sfx(Sounds.StageClearSfx);
            _state = GameState.AllClear;
            AllClearScore.Text = $"FINAL SCORE: {_score}";
            AllClearOverlay.Visibility = Visibility.Visible;
            StageClearOverlay.Visibility = Visibility.Collapsed;
            HudPanel.Visibility = Visibility.Collapsed;
            return;
        }
        LoadStage(_stage);
        ShowPlaying();
    }

    private void ResetBall()
    {
        // Remove extra balls
        for (int i = _ballVisuals.Count - 1; i >= 0; i--)
            GameCanvas.Children.Remove(_ballVisuals[i]);
        _balls.Clear();
        _ballVisuals.Clear();

        var ball = new Ball(_paddle.CenterX, _paddle.Y - 8) { Stuck = true, Speed = _slowBallTimer > 0 ? SlowBallSpeed : NormalBallSpeed };
        _balls.Add(ball);
        var visual = CreateBallVisual();
        _ballVisuals.Add(visual);
        GameCanvas.Children.Add(visual);
    }

    // ── Update ─────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        // Combo timer
        if (_combo > 0)
        {
            _comboTimer -= dt;
            if (_comboTimer <= 0)
            {
                _combo = 0;
                ComboText.Visibility = Visibility.Collapsed;
            }
        }

        // Power-up timers
        UpdatePowerUpTimers(dt);

        // Move paddle
        UpdatePaddle(dt);

        // Update balls
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            var ball = _balls[i];
            if (ball.Stuck)
            {
                ball.X = _paddle.CenterX;
                ball.Y = _paddle.Y - ball.Radius - 1;
                continue;
            }

            ball.Update(dt);

            // Wall collisions
            if (ball.X - ball.Radius < 0)
            {
                ball.X = ball.Radius;
                ball.VX = Math.Abs(ball.VX);
            }
            else if (ball.X + ball.Radius > CanvasW)
            {
                ball.X = CanvasW - ball.Radius;
                ball.VX = -Math.Abs(ball.VX);
            }

            if (ball.Y - ball.Radius < 0)
            {
                ball.Y = ball.Radius;
                ball.VY = Math.Abs(ball.VY);
            }

            // Ball fell below screen
            if (ball.Y > CanvasH + 20)
            {
                ball.Active = false;
                GameCanvas.Children.Remove(_ballVisuals[i]);
                _ballVisuals.RemoveAt(i);
                _balls.RemoveAt(i);
                continue;
            }

            // Paddle collision
            if (ball.VY > 0 && BallHitsPaddle(ball))
            {
                SoundGen.Sfx(Sounds.BallHitSfx);
                ball.Y = _paddle.Y - ball.Radius - 1;

                // Calculate bounce angle based on hit position
                double hitPos = (ball.X - _paddle.X) / _paddle.Width; // 0..1
                hitPos = Math.Clamp(hitPos, 0, 1);
                double angle = (hitPos - 0.5) * Math.PI * 0.8; // -72 to +72 degrees
                ball.SetVelocity(angle);
            }

            // Brick collision
            CheckBrickCollisions(ball);
        }

        // All balls lost
        if (_balls.Count == 0)
        {
            _lives--;
            _combo = 0;
            ComboText.Visibility = Visibility.Collapsed;
            SoundGen.Sfx(Sounds.LifeLostSfx);

            if (_lives <= 0)
            {
                if (_score > _highScore) _highScore = _score;
                SoundGen.StopBgm();
                _state = GameState.GameOver;
                FinalScoreText.Text = $"SCORE: {_score}";
                FinalStageText.Text = $"Reached Stage {_stage}";
                GameOverOverlay.Visibility = Visibility.Visible;
                HudPanel.Visibility = Visibility.Collapsed;
                ComboText.Visibility = Visibility.Collapsed;
                PowerUpText.Visibility = Visibility.Collapsed;
                return;
            }

            ResetBall();
        }

        // Update power-ups (falling)
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var pu = _powerUps[i];
            pu.Update(dt);

            // Collect
            if (pu.Active && pu.Y + pu.Height >= _paddle.Y && pu.Y <= _paddle.Y + _paddle.Height
                && pu.X + pu.Width >= _paddle.X && pu.X <= _paddle.X + _paddle.Width)
            {
                SoundGen.Sfx(Sounds.PowerUpSfx);
                ApplyPowerUp(pu);
                RemovePowerUpVisual(i);
                continue;
            }

            // Fell off screen
            if (pu.Y > CanvasH + 20)
            {
                RemovePowerUpVisual(i);
            }
        }

        // Update lasers
        if (_hasLaser)
        {
            _laserCooldownTimer -= dt;
            if (_laserCooldownTimer <= 0)
            {
                FireLaser();
                _laserCooldownTimer = LaserCooldown;
            }
        }

        for (int i = _lasers.Count - 1; i >= 0; i--)
        {
            var laser = _lasers[i];
            laser.Y -= laser.Speed * dt;

            if (laser.Y < -10)
            {
                GameCanvas.Children.Remove(_laserVisuals[i]);
                _laserVisuals.RemoveAt(i);
                _lasers.RemoveAt(i);
                continue;
            }

            // Laser vs bricks
            for (int j = 0; j < _bricks.Count; j++)
            {
                var brick = _bricks[j];
                if (!brick.Alive) continue;
                if (laser.X >= brick.X && laser.X <= brick.X + brick.Width
                    && laser.Y >= brick.Y && laser.Y <= brick.Y + brick.Height)
                {
                    bool destroyed = brick.Hit();
                    if (destroyed)
                    {
                        OnBrickDestroyed(brick, j);
                    }
                    else if (brick.Alive && brick.Type == BrickType.Hard)
                    {
                        UpdateBrickVisualForDamage(j, brick);
                    }

                    GameCanvas.Children.Remove(_laserVisuals[i]);
                    _laserVisuals.RemoveAt(i);
                    _lasers.RemoveAt(i);
                    break;
                }
            }
        }

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.VY += 200 * dt; // gravity

            if (p.Life <= 0)
            {
                GameCanvas.Children.Remove(_particleVisuals[i]);
                _particleVisuals.RemoveAt(i);
                _particles.RemoveAt(i);
            }
        }

        // Check stage clear
        bool allCleared = true;
        foreach (var b in _bricks)
        {
            if (b.Alive && b.Type != BrickType.Unbreakable)
            {
                allCleared = false;
                break;
            }
        }

        if (allCleared)
        {
            SoundGen.Sfx(Sounds.StageClearSfx);
            _state = GameState.StageClear;
            StageClearScore.Text = $"SCORE: {_score}";
            StageClearOverlay.Visibility = Visibility.Visible;
            ComboText.Visibility = Visibility.Collapsed;
            PowerUpText.Visibility = Visibility.Collapsed;
        }

        // HUD
        UpdateHud();
    }

    private void UpdatePaddle(double dt)
    {
        if (_useMouseInput && _mouseX >= 0)
        {
            double targetX = _mouseX - _paddle.Width / 2;
            _paddle.X = Math.Clamp(targetX, 0, CanvasW - _paddle.Width);
        }
        else
        {
            if (_keyLeft) _paddle.X -= _paddle.Speed * dt;
            if (_keyRight) _paddle.X += _paddle.Speed * dt;
            _paddle.X = Math.Clamp(_paddle.X, 0, CanvasW - _paddle.Width);
        }
    }

    private void UpdatePowerUpTimers(double dt)
    {
        bool anyActive = false;
        string activeText = "";

        if (_widePaddleTimer > 0)
        {
            _widePaddleTimer -= dt;
            if (_widePaddleTimer <= 0)
            {
                _paddle.Width = _paddle.DefaultWidth;
                if (_paddleRect != null) _paddleRect.Width = _paddle.Width;
            }
            else
            {
                anyActive = true;
                activeText += $"WIDE {_widePaddleTimer:F0}s  ";
            }
        }

        if (_slowBallTimer > 0)
        {
            _slowBallTimer -= dt;
            if (_slowBallTimer <= 0)
            {
                foreach (var b in _balls)
                {
                    b.Speed = NormalBallSpeed;
                    b.NormalizeSpeed();
                }
            }
            else
            {
                anyActive = true;
                activeText += $"SLOW {_slowBallTimer:F0}s  ";
            }
        }

        if (_laserTimer > 0)
        {
            _laserTimer -= dt;
            if (_laserTimer <= 0) _hasLaser = false;
            else
            {
                anyActive = true;
                activeText += $"LASER {_laserTimer:F0}s  ";
            }
        }

        if (anyActive)
        {
            PowerUpText.Text = activeText.TrimEnd();
            PowerUpText.Visibility = Visibility.Visible;
        }
        else
        {
            PowerUpText.Visibility = Visibility.Collapsed;
        }
    }

    private bool BallHitsPaddle(Ball ball)
    {
        return ball.X + ball.Radius >= _paddle.X
            && ball.X - ball.Radius <= _paddle.X + _paddle.Width
            && ball.Y + ball.Radius >= _paddle.Y
            && ball.Y - ball.Radius <= _paddle.Y + _paddle.Height;
    }

    private void CheckBrickCollisions(Ball ball)
    {
        for (int i = 0; i < _bricks.Count; i++)
        {
            var brick = _bricks[i];
            if (!brick.Alive) continue;

            // AABB vs circle
            double closestX = Math.Clamp(ball.X, brick.X, brick.X + brick.Width);
            double closestY = Math.Clamp(ball.Y, brick.Y, brick.Y + brick.Height);
            double dx = ball.X - closestX;
            double dy = ball.Y - closestY;
            double distSq = dx * dx + dy * dy;

            if (distSq > ball.Radius * ball.Radius) continue;

            // Determine bounce direction
            double overlapLeft = ball.X + ball.Radius - brick.X;
            double overlapRight = brick.X + brick.Width - (ball.X - ball.Radius);
            double overlapTop = ball.Y + ball.Radius - brick.Y;
            double overlapBottom = brick.Y + brick.Height - (ball.Y - ball.Radius);

            double minOverlapX = Math.Min(overlapLeft, overlapRight);
            double minOverlapY = Math.Min(overlapTop, overlapBottom);

            if (minOverlapX < minOverlapY)
                ball.VX = -ball.VX;
            else
                ball.VY = -ball.VY;

            bool destroyed = brick.Hit();
            if (destroyed)
            {
                OnBrickDestroyed(brick, i);
            }
            else if (brick.Alive && brick.Type == BrickType.Hard)
            {
                UpdateBrickVisualForDamage(i, brick);
            }

            break; // one collision per frame per ball
        }
    }

    private void OnBrickDestroyed(Brick brick, int index)
    {
        SoundGen.Sfx(Sounds.BrickBreakSfx);

        // Combo
        _combo++;
        _comboTimer = 2.0;
        int multiplier = Math.Min(_combo, 8);
        int points = brick.Points * multiplier;
        _score += points;

        if (_combo > 1)
        {
            ComboText.Text = $"COMBO x{_combo}  +{points}";
            ComboText.Visibility = Visibility.Visible;
        }

        // Hide brick visual
        if (index < _brickRects.Count)
            _brickRects[index].Visibility = Visibility.Collapsed;

        // Spawn particles
        SpawnParticles(brick.X + brick.Width / 2, brick.Y + brick.Height / 2, brick.Color);

        // Maybe drop power-up
        if (_rng.NextDouble() < 0.20)
        {
            var type = (PowerUpType)_rng.Next(5);
            var pu = new PowerUp(brick.X + brick.Width / 2 - 15, brick.Y, type);
            _powerUps.Add(pu);

            var rect = new Rectangle
            {
                Width = pu.Width,
                Height = pu.Height,
                RadiusX = 3,
                RadiusY = 3,
                Fill = new SolidColorBrush(Color.FromArgb(180, pu.Color.R, pu.Color.G, pu.Color.B)),
                Stroke = new SolidColorBrush(pu.Color),
                StrokeThickness = 1
            };
            var label = new TextBlock
            {
                Text = pu.Label,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Consolas")
            };
            _powerUpVisuals.Add((rect, label));
            GameCanvas.Children.Add(rect);
            GameCanvas.Children.Add(label);
        }
    }

    private void ApplyPowerUp(PowerUp pu)
    {
        switch (pu.Type)
        {
            case PowerUpType.WidePaddle:
                _paddle.Width = _paddle.DefaultWidth * 1.5;
                if (_paddleRect != null) _paddleRect.Width = _paddle.Width;
                _widePaddleTimer = PowerUpDuration;
                break;

            case PowerUpType.MultiBall:
                var existing = _balls.Where(b => b.Active && !b.Stuck).ToList();
                if (existing.Count > 0)
                {
                    var src = existing[0];
                    for (int k = 0; k < 2; k++)
                    {
                        double angle = (k == 0 ? -0.4 : 0.4);
                        var nb = new Ball(src.X, src.Y)
                        {
                            Stuck = false,
                            Speed = src.Speed,
                        };
                        double cos = Math.Cos(angle);
                        double sin = Math.Sin(angle);
                        nb.VX = src.VX * cos - src.VY * sin;
                        nb.VY = src.VX * sin + src.VY * cos;
                        nb.NormalizeSpeed();
                        _balls.Add(nb);
                        var visual = CreateBallVisual();
                        _ballVisuals.Add(visual);
                        GameCanvas.Children.Add(visual);
                    }
                }
                break;

            case PowerUpType.LaserPaddle:
                _hasLaser = true;
                _laserTimer = PowerUpDuration;
                _laserCooldownTimer = 0;
                break;

            case PowerUpType.SlowBall:
                _slowBallTimer = SlowDuration;
                foreach (var b in _balls)
                {
                    b.Speed = SlowBallSpeed;
                    b.NormalizeSpeed();
                }
                break;

            case PowerUpType.ExtraLife:
                _lives++;
                break;
        }
    }

    private void ClearPowerUpEffects()
    {
        _widePaddleTimer = 0;
        _slowBallTimer = 0;
        _laserTimer = 0;
        _hasLaser = false;
        _laserCooldownTimer = 0;
    }

    private void FireLaser()
    {
        double lx = _paddle.CenterX;
        double ly = _paddle.Y - 4;
        var laser = new Laser(lx, ly);
        _lasers.Add(laser);

        var rect = new Rectangle
        {
            Width = 3,
            Height = 12,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xFF, 0x00, 0x00),
                BlurRadius = 6,
                ShadowDepth = 0
            }
        };
        _laserVisuals.Add(rect);
        GameCanvas.Children.Add(rect);
    }

    private void SpawnParticles(double cx, double cy, Color color)
    {
        for (int i = 0; i < 8; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double speed = 80 + _rng.NextDouble() * 160;
            var p = new Particle
            {
                X = cx,
                Y = cy,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed - 60,
                Life = ParticleLifetime * (0.5 + _rng.NextDouble() * 0.5),
                Color = color
            };
            _particles.Add(p);

            var vis = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(color)
            };
            _particleVisuals.Add(vis);
            GameCanvas.Children.Add(vis);
        }
    }

    // ── Render ──────────────────────────────────────────

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // Paddle
        if (_paddleRect != null)
        {
            Canvas.SetLeft(_paddleRect, _paddle.X);
            Canvas.SetTop(_paddleRect, _paddle.Y);
        }

        // Balls
        for (int i = 0; i < _balls.Count && i < _ballVisuals.Count; i++)
        {
            Canvas.SetLeft(_ballVisuals[i], _balls[i].X - _balls[i].Radius);
            Canvas.SetTop(_ballVisuals[i], _balls[i].Y - _balls[i].Radius);
        }

        // Bricks are static, no position update needed

        // Power-ups
        for (int i = 0; i < _powerUps.Count && i < _powerUpVisuals.Count; i++)
        {
            var (rect, label) = _powerUpVisuals[i];
            Canvas.SetLeft(rect, _powerUps[i].X);
            Canvas.SetTop(rect, _powerUps[i].Y);
            Canvas.SetLeft(label, _powerUps[i].X + 6);
            Canvas.SetTop(label, _powerUps[i].Y + 1);
        }

        // Lasers
        for (int i = 0; i < _lasers.Count && i < _laserVisuals.Count; i++)
        {
            Canvas.SetLeft(_laserVisuals[i], _lasers[i].X - 1.5);
            Canvas.SetTop(_laserVisuals[i], _lasers[i].Y);
        }

        // Particles
        for (int i = 0; i < _particles.Count && i < _particleVisuals.Count; i++)
        {
            var p = _particles[i];
            Canvas.SetLeft(_particleVisuals[i], p.X - 2);
            Canvas.SetTop(_particleVisuals[i], p.Y - 2);
            double alpha = Math.Clamp(p.Life / ParticleLifetime, 0, 1);
            _particleVisuals[i].Opacity = alpha;
        }
    }

    private void UpdateHud()
    {
        ScoreText.Text = $"SCORE: {_score}";
        StageText.Text = $"STAGE {_stage}";
        LivesText.Text = $"LIVES: {_lives}";
    }

    // ── Visual Factory ─────────────────────────────────

    private static Ellipse CreateBallVisual()
    {
        return new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(Colors.White),
            Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 8,
                ShadowDepth = 0
            }
        };
    }

    private static Rectangle CreateBrickVisual(Brick brick)
    {
        var fill = brick.Type switch
        {
            BrickType.Unbreakable => new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            BrickType.Hard => new LinearGradientBrush(
                Color.FromRgb(0xA0, 0xA0, 0xA0),
                Color.FromRgb(0xD0, 0xD0, 0xD0),
                45) as Brush,
            _ => new SolidColorBrush(brick.Color)
        };

        var stroke = brick.Type == BrickType.Unbreakable
            ? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
            : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

        var rect = new Rectangle
        {
            Width = brick.Width,
            Height = brick.Height,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1,
            RadiusX = 2,
            RadiusY = 2
        };

        Canvas.SetLeft(rect, brick.X);
        Canvas.SetTop(rect, brick.Y);

        if (brick.Type != BrickType.Unbreakable)
        {
            rect.Effect = new DropShadowEffect
            {
                Color = brick.Color,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.5
            };
        }

        return rect;
    }

    private static void UpdateBrickVisualForDamage(int index, Brick brick)
    {
        // Hard brick took a hit - crack visual
        // Handled implicitly: just darken slightly
    }

    // ── Cleanup ────────────────────────────────────────

    private void ClearAllVisuals()
    {
        if (_paddleRect != null) { GameCanvas.Children.Remove(_paddleRect); _paddleRect = null; }
        foreach (var v in _ballVisuals) GameCanvas.Children.Remove(v);
        _ballVisuals.Clear();
        foreach (var r in _brickRects) GameCanvas.Children.Remove(r);
        _brickRects.Clear();
        foreach (var (rect, label) in _powerUpVisuals)
        {
            GameCanvas.Children.Remove(rect);
            GameCanvas.Children.Remove(label);
        }
        _powerUpVisuals.Clear();
        foreach (var r in _laserVisuals) GameCanvas.Children.Remove(r);
        _laserVisuals.Clear();
        foreach (var v in _particleVisuals) GameCanvas.Children.Remove(v);
        _particleVisuals.Clear();
    }

    private void RemovePowerUpVisual(int index)
    {
        if (index < _powerUpVisuals.Count)
        {
            var (rect, label) = _powerUpVisuals[index];
            GameCanvas.Children.Remove(rect);
            GameCanvas.Children.Remove(label);
            _powerUpVisuals.RemoveAt(index);
        }
        if (index < _powerUps.Count)
        {
            _powerUps[index].Active = false;
            _powerUps.RemoveAt(index);
        }
    }

    private void ShowPlaying()
    {
        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        StageClearOverlay.Visibility = Visibility.Collapsed;
        AllClearOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        ComboText.Visibility = Visibility.Collapsed;
        PowerUpText.Visibility = Visibility.Collapsed;
        UpdateHud();
    }

    private void ShowTitle()
    {
        ClearAllVisuals();
        _state = GameState.Title;
        TitlePanel.Visibility = Visibility.Visible;
        HudPanel.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        StageClearOverlay.Visibility = Visibility.Collapsed;
        AllClearOverlay.Visibility = Visibility.Collapsed;
        ComboText.Visibility = Visibility.Collapsed;
        PowerUpText.Visibility = Visibility.Collapsed;
        HighScoreTitle.Text = _highScore > 0 ? $"HIGH SCORE: {_highScore}" : "";
    }

    // ── Input ──────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A:
                _keyLeft = true;
                _useMouseInput = false;
                break;
            case Key.Right or Key.D:
                _keyRight = true;
                _useMouseInput = false;
                break;
            case Key.Space when _state == GameState.Playing:
                foreach (var b in _balls)
                {
                    if (b.Stuck) b.Launch();
                }
                break;
            case Key.Enter when _state == GameState.Title:
                StartGame();
                break;
            case Key.Enter when _state == GameState.GameOver:
                StartGame();
                break;
            case Key.Enter when _state == GameState.StageClear:
                NextStage();
                break;
            case Key.Enter when _state == GameState.AllClear:
                ShowTitle();
                break;
            case Key.Escape when _state == GameState.Playing:
                SoundGen.StopBgm();
                _loop.Stop();
                ShowTitle();
                _loop.Start();
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A:
                _keyLeft = false;
                break;
            case Key.Right or Key.D:
                _keyRight = false;
                break;
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);
        _mouseX = pos.X;
        _useMouseInput = true;
    }
}

// ── Helper Types ───────────────────────────────────

internal sealed class Laser
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Speed { get; set; } = 600;

    public Laser(double x, double y)
    {
        X = x;
        Y = y;
    }
}

internal sealed class Particle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Life { get; set; }
    public Color Color { get; set; }
}
