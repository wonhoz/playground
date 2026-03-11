namespace DodgeCraft;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 상수 ──────────────────────────────────────────────────────────────
    private const double CanvasW    = 660;
    private const double CanvasH    = 720;
    private const double PlayerR    = 8;
    private const int    MaxSlots   = 5;
    private const int    MaxLives   = 3;

    // 구조물 비용
    private static readonly int[] StructureCost = [0, 2, 3, 2, 4]; // index=StructureType+1

    // ── 엔진 ──────────────────────────────────────────────────────────────
    private readonly GameLoop _loop = new();

    // ── 게임 상태 ──────────────────────────────────────────────────────────
    private enum GameState { Title, Playing, GameOver }
    private GameState _state = GameState.Title;

    private double _playerX = CanvasW / 2, _playerY = CanvasH * 0.7;
    private int    _lives, _score, _wave, _resource;
    private double _waveTimer, _invincibleTimer;
    private int    _bestScore;

    private readonly List<Enemy>     _enemies    = [];
    private readonly List<Bullet>    _bullets    = [];
    private readonly List<Structure> _structures = [];
    private readonly List<Particle>  _particles  = [];

    // 배치 모드
    private Structure.StructureType? _placingType;
    private double _placingAngle;  // 거울/팬 각도 (마우스 위치에 따라 자동 결정)

    // ── 비주얼 ────────────────────────────────────────────────────────────
    private Ellipse?  _playerShape;
    private Ellipse?  _playerGlow;
    private readonly List<UIElement> _dynamicElements = [];

    // 색상
    private static readonly Color ColPlayer    = Color.FromRgb(0xFF, 0x80, 0xFF);
    private static readonly Color ColWall      = Color.FromRgb(0x40, 0xA0, 0xFF);
    private static readonly Color ColMirror    = Color.FromRgb(0xC0, 0xFF, 0xC0);
    private static readonly Color ColFan       = Color.FromRgb(0x80, 0xFF, 0xFF);
    private static readonly Color ColBomb      = Color.FromRgb(0xFF, 0xA0, 0x00);
    private static readonly Color ColEnemy     = Color.FromRgb(0xFF, 0x40, 0x40);

    private static readonly Random _rng = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource src)
            { int v = 1; DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int)); }

            BuildPalette();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 팔레트 UI 구성 ────────────────────────────────────────────────────

    private void BuildPalette()
    {
        var types = new[] { (Structure.StructureType.Wall, "🧱1", ColWall),
                            (Structure.StructureType.Mirror,"🪞2", ColMirror),
                            (Structure.StructureType.Fan,  "💨3", ColFan),
                            (Structure.StructureType.Bomb, "💥4", ColBomb) };
        foreach (var (type, label, col) in types)
        {
            var btn = new Border
            {
                Width = 42, Height = 42, CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(60, col.R, col.G, col.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, col.R, col.G, col.B)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Tag = type,
            };
            var tb = new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(col),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            btn.Child = tb;
            btn.MouseLeftButtonDown += (s, _) =>
            {
                if (s is Border b && b.Tag is Structure.StructureType t)
                    _placingType = t;
            };
            PalettePanel.Children.Add(btn);
        }
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────

    private void StartGame()
    {
        _state     = GameState.Playing;
        _lives     = MaxLives;
        _score     = 0;
        _wave      = 1;
        _resource  = 6;
        _waveTimer = 0;
        _invincibleTimer = 0;
        _placingType = null;

        _enemies.Clear(); _bullets.Clear(); _structures.Clear(); _particles.Clear();

        _playerX = CanvasW / 2;
        _playerY = CanvasH * 0.72;

        foreach (var el in _dynamicElements) GameCanvas.Children.Remove(el);
        _dynamicElements.Clear();

        // 플레이어 도형 생성
        if (_playerGlow != null) GameCanvas.Children.Remove(_playerGlow);
        _playerGlow = new Ellipse { Width = PlayerR * 4, Height = PlayerR * 4,
            Fill = new RadialGradientBrush(Color.FromArgb(80, 0xFF, 0x80, 0xFF), Colors.Transparent) };
        GameCanvas.Children.Add(_playerGlow);

        if (_playerShape != null) GameCanvas.Children.Remove(_playerShape);
        _playerShape = new Ellipse { Width = PlayerR * 2, Height = PlayerR * 2,
            Fill = new SolidColorBrush(ColPlayer) };
        GameCanvas.Children.Add(_playerShape);

        SpawnWave();

        TitlePanel.Visibility    = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        HudPanel.Visibility      = Visibility.Visible;

        UpdateHud();
    }

    // ── 웨이브 관리 ───────────────────────────────────────────────────────

    private void SpawnWave()
    {
        int count = 1 + _wave / 2;
        count = Math.Min(count, 5);

        for (int i = 0; i < count; i++)
        {
            var patterns = Enum.GetValues<BulletPatterns.PatternType>();
            var pat = patterns[_rng.Next(patterns.Length)];
            double fi = Math.Max(0.6, 2.5 - _wave * 0.15);

            _enemies.Add(new Enemy
            {
                X = 60 + _rng.NextDouble() * (CanvasW - 120),
                Y = 40  + _rng.NextDouble() * (CanvasH * 0.3),
                VX = (30 + _wave * 5) * (_rng.NextDouble() < 0.5 ? 1 : -1),
                VY = (15 + _wave * 3) * (_rng.NextDouble() < 0.5 ? 1 : -1),
                Hp = 2 + _wave / 3,
                Pattern = pat,
                FireInterval = fi,
                FireCooldown = _rng.NextDouble() * fi,
            });
        }
    }

    // ── 업데이트 ──────────────────────────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        _waveTimer += dt;
        _score     += (int)(dt * 10);
        if (_invincibleTimer > 0) _invincibleTimer -= dt;

        // 적 업데이트 + 발사
        foreach (var e in _enemies)
        {
            e.Update(dt, CanvasW, CanvasH);

            if (e.CanFire())
            {
                e.ResetFire();
                var newBullets = BulletPatterns.Spawn(
                    e.Pattern, e.X, e.Y, _playerX, _playerY, _wave);
                // 고레벨에서 관통 탄 추가
                if (_wave >= 6 && _rng.NextDouble() < 0.2)
                    foreach (var b in newBullets) b.Penetrating = true;
                _bullets.AddRange(newBullets);
                _resource++;  // 회피 성공 시 자원 획득
            }
        }

        // 탄환 업데이트
        foreach (var b in _bullets)
        {
            b.Update(dt, _playerX, _playerY);

            // 화면 밖 제거
            if (b.X < -20 || b.X > CanvasW + 20 || b.Y < -20 || b.Y > CanvasH + 20)
                b.IsAlive = false;
        }

        // 구조물 - 탄환 상호작용
        foreach (var s in _structures)
        {
            s.Update(dt);
            foreach (var b in _bullets)
            {
                if (!b.IsAlive) continue;
                var result = s.Interact(b);
                if (result == Structure.InteractionResult.Reflected)
                {
                    // 반사된 탄이 적에게 맞으면 점수+
                    _score += 30;
                }
                if (result != Structure.InteractionResult.None)
                    _particles.AddRange(Particle.Burst(b.X, b.Y,
                        result == Structure.InteractionResult.Reflected ? Colors.LimeGreen : Colors.White));
            }
        }

        // 반사 탄 → 적 히트 체크
        foreach (var b in _bullets.Where(b => b.IsAlive && b.VY < 0))  // 위로 가는 탄 = 반사됐을 가능성
        {
            foreach (var e in _enemies)
            {
                if (!e.IsAlive) continue;
                double dx = b.X - e.X, dy = b.Y - e.Y;
                if (dx * dx + dy * dy < (b.Radius + 14) * (b.Radius + 14))
                {
                    b.IsAlive = false;
                    e.Hit();
                    _score += 50;
                    _particles.AddRange(Particle.Burst(e.X, e.Y, ColEnemy));
                }
            }
        }

        // 플레이어 피격 체크
        if (_invincibleTimer <= 0)
        {
            foreach (var b in _bullets.Where(b => b.IsAlive))
            {
                if (b.HitTest(_playerX, _playerY, PlayerR))
                {
                    b.IsAlive = false;
                    _lives--;
                    _invincibleTimer = 2.0;
                    _particles.AddRange(Particle.Burst(_playerX, _playerY, ColPlayer, 12));
                    _resource += 3;  // 피격 시 자원 소량 지급

                    if (_lives <= 0)
                    {
                        GameOver();
                        return;
                    }
                    break;
                }
            }
        }

        // 파티클 업데이트
        foreach (var p in _particles) p.Update(dt);

        // 죽은 객체 제거
        _bullets.RemoveAll(b => !b.IsAlive);
        _structures.RemoveAll(s => !s.IsAlive);
        _particles.RemoveAll(p => !p.IsAlive);
        _enemies.RemoveAll(e => !e.IsAlive);

        // 웨이브 클리어
        if (_enemies.Count == 0 && _waveTimer > 1)
        {
            _wave++;
            _waveTimer = 0;
            _resource += 5;
            _bullets.Clear();
            SpawnWave();
        }

        _resource = Math.Min(_resource, 99);
        UpdateHud();
    }

    // ── 렌더 ──────────────────────────────────────────────────────────────

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        foreach (var el in _dynamicElements) GameCanvas.Children.Remove(el);
        _dynamicElements.Clear();

        // 구분선 (플레이어 영역)
        var divLine = new Line
        {
            X1 = 0, Y1 = CanvasH * 0.5, X2 = CanvasW, Y2 = CanvasH * 0.5,
            Stroke = new SolidColorBrush(Color.FromArgb(30, 0x40, 0x40, 0x80)),
            StrokeThickness = 1, StrokeDashArray = [8, 6],
        };
        GameCanvas.Children.Add(divLine);
        _dynamicElements.Add(divLine);

        // 적 렌더
        foreach (var e in _enemies)
        {
            var ell = new Ellipse { Width = 28, Height = 28,
                Fill = new SolidColorBrush(Color.FromArgb(200, ColEnemy.R, ColEnemy.G, ColEnemy.B)),
                Stroke = new SolidColorBrush(ColEnemy), StrokeThickness = 1.5 };
            Canvas.SetLeft(ell, e.X - 14); Canvas.SetTop(ell, e.Y - 14);
            GameCanvas.Children.Add(ell); _dynamicElements.Add(ell);
        }

        // 구조물 렌더
        foreach (var s in _structures)
        {
            var col = s.Type switch
            {
                Structure.StructureType.Wall   => ColWall,
                Structure.StructureType.Mirror => ColMirror,
                Structure.StructureType.Fan    => ColFan,
                _                              => ColBomb,
            };
            double alpha = Math.Min(1.0, s.Lifetime / 5.0);  // 5초 이하 페이드
            byte a = (byte)(180 * alpha);

            var rect = s.Type == Structure.StructureType.Wall
                ? new Rectangle { Width = s.Width, Height = s.Height,
                    Fill = new SolidColorBrush(Color.FromArgb(a, col.R, col.G, col.B)),
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)(a+40), col.R, col.G, col.B)),
                    StrokeThickness = 1.5 }
                : new Rectangle { Width = s.Width, Height = s.Width,
                    Fill = new SolidColorBrush(Color.FromArgb(a, col.R, col.G, col.B)),
                    Stroke = new SolidColorBrush(Color.FromArgb((byte)(a+40), col.R, col.G, col.B)),
                    StrokeThickness = 1.5 };

            if (s.Type == Structure.StructureType.Mirror)
            {
                var tg = new TransformGroup();
                tg.Children.Add(new RotateTransform(s.Angle * 180 / Math.PI, s.Width / 2, s.Height / 2));
                rect.RenderTransform = tg;
            }

            Canvas.SetLeft(rect, s.X - s.Width / 2); Canvas.SetTop(rect, s.Y - s.Height / 2);
            GameCanvas.Children.Add(rect); _dynamicElements.Add(rect);

            // HP 바
            if (s.Hp > 0 && s.Type == Structure.StructureType.Wall)
            {
                var hpBar = new Rectangle { Width = s.Width * s.Hp / 5.0, Height = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(a, 0x40, 0xA0, 0xFF)) };
                Canvas.SetLeft(hpBar, s.X - s.Width / 2);
                Canvas.SetTop(hpBar, s.Y + s.Height / 2 + 2);
                GameCanvas.Children.Add(hpBar); _dynamicElements.Add(hpBar);
            }
        }

        // 탄환 렌더
        foreach (var b in _bullets)
        {
            byte glowA = (byte)(b.Penetrating ? 200 : 160);
            var ell = new Ellipse { Width = b.Radius * 2, Height = b.Radius * 2,
                Fill = new SolidColorBrush(Color.FromArgb(glowA, b.Color.R, b.Color.G, b.Color.B)) };
            Canvas.SetLeft(ell, b.X - b.Radius); Canvas.SetTop(ell, b.Y - b.Radius);
            GameCanvas.Children.Add(ell); _dynamicElements.Add(ell);
        }

        // 파티클 렌더
        foreach (var p in _particles)
        {
            byte a = (byte)(200 * p.Alpha);
            var ell = new Ellipse { Width = p.Size, Height = p.Size,
                Fill = new SolidColorBrush(Color.FromArgb(a, p.Color.R, p.Color.G, p.Color.B)) };
            Canvas.SetLeft(ell, p.X - p.Size / 2); Canvas.SetTop(ell, p.Y - p.Size / 2);
            GameCanvas.Children.Add(ell); _dynamicElements.Add(ell);
        }

        // 배치 미리보기 (마우스 위치)
        if (_placingType.HasValue && _state == GameState.Playing)
        {
            var col = _placingType.Value switch
            {
                Structure.StructureType.Wall   => ColWall,
                Structure.StructureType.Mirror => ColMirror,
                Structure.StructureType.Fan    => ColFan,
                _                              => ColBomb,
            };
            var prev = new Ellipse { Width = 36, Height = 36,
                Stroke = new SolidColorBrush(Color.FromArgb(120, col.R, col.G, col.B)),
                StrokeThickness = 2, StrokeDashArray = [4, 3],
                Fill = new SolidColorBrush(Color.FromArgb(30, col.R, col.G, col.B)) };
            Canvas.SetLeft(prev, _playerX + 40 - 18); Canvas.SetTop(prev, _playerY - 18);
            GameCanvas.Children.Add(prev); _dynamicElements.Add(prev);
        }

        // 플레이어 위치
        if (_playerShape != null)
        {
            double flash = _invincibleTimer > 0 ? (Math.Sin(_invincibleTimer * 15) > 0 ? 0.3 : 1.0) : 1.0;
            _playerShape.Opacity = flash;
            Canvas.SetLeft(_playerShape, _playerX - PlayerR);
            Canvas.SetTop(_playerShape,  _playerY - PlayerR);
        }
        if (_playerGlow != null)
        {
            Canvas.SetLeft(_playerGlow, _playerX - PlayerR * 2);
            Canvas.SetTop(_playerGlow,  _playerY - PlayerR * 2);
        }
    }

    // ── HUD ───────────────────────────────────────────────────────────────

    private void UpdateHud()
    {
        TxtScore.Text    = $"SCORE {_score:N0}";
        TxtWave.Text     = $"WAVE {_wave}";
        TxtLives.Text    = "  " + string.Concat(Enumerable.Repeat("♥ ", _lives));
        TxtResource.Text = _resource.ToString();
        TxtSlots.Text    = $"슬롯: {_structures.Count}/{MaxSlots}";
    }

    // ── 게임 오버 ─────────────────────────────────────────────────────────

    private void GameOver()
    {
        _state = GameState.GameOver;
        _loop.Stop();

        bool newBest = _score > _bestScore;
        if (newBest) _bestScore = _score;

        TxtFinalScore.Text  = $"SCORE: {_score:N0}";
        TxtFinalWave.Text   = $"WAVE: {_wave}";
        TxtNewBest.Text     = "★ 최고 기록!";
        TxtNewBest.Visibility = newBest ? Visibility.Visible : Visibility.Collapsed;

        if (_bestScore > 0)
            TxtBestTitle.Text = $"최고 기록: {_bestScore:N0}";

        HudPanel.Visibility      = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Visible;
    }

    // ── 입력 ──────────────────────────────────────────────────────────────

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_state != GameState.Playing) return;
        var pos = e.GetPosition(GameCanvas);
        _playerX = Math.Clamp(pos.X, PlayerR, CanvasW - PlayerR);
        _playerY = Math.Clamp(pos.Y, CanvasH * 0.45, CanvasH - PlayerR);

        // 거울/팬 각도: 플레이어에서 마우스로의 방향
        _placingAngle = Math.Atan2(pos.Y - _playerY, pos.X - _playerX);
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state != GameState.Playing || !_placingType.HasValue) return;

        int cost = _placingType.Value switch
        {
            Structure.StructureType.Wall   => 2,
            Structure.StructureType.Mirror => 3,
            Structure.StructureType.Fan    => 2,
            Structure.StructureType.Bomb   => 4,
            _                              => 99,
        };

        if (_resource < cost || _structures.Count >= MaxSlots) return;

        var pos = e.GetPosition(GameCanvas);
        var s = Structure.Create(_placingType.Value, pos.X, pos.Y, _placingAngle);
        _structures.Add(s);
        _resource -= cost;
        UpdateHud();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.D1: _placingType = Structure.StructureType.Wall;   break;
            case Key.D2: _placingType = Structure.StructureType.Mirror; break;
            case Key.D3: _placingType = Structure.StructureType.Fan;    break;
            case Key.D4: _placingType = Structure.StructureType.Bomb;   break;
            case Key.Escape:
                if (_state == GameState.Playing) { GameOver(); }
                else if (_state == GameState.GameOver)
                {
                    _state = GameState.Title;
                    GameOverPanel.Visibility = Visibility.Collapsed;
                    TitlePanel.Visibility    = Visibility.Visible;
                }
                break;
            case Key.Return:
                if (_state is GameState.Title or GameState.GameOver)
                {
                    _loop.Start();
                    StartGame();
                }
                break;
        }
    }
}
