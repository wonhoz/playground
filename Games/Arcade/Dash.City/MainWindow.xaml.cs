using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DashCity.Engine;

namespace DashCity;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 상수 ──────────────────────────────────────
    private const double LaneWidth = 2.8;
    private const double RoadWidth = 12.0;
    private const double SegLen = 6.0;
    private const int VisibleSegs = 22;
    private const double BaseSpeed = 18.0;
    private const double MaxSpeed = 55.0;

    // 플레이어 물리
    private const double Gravity = 35.0;
    private const double JumpForce = 14.0;
    private const double SlideTime = 0.6;

    // ── 색상 ──────────────────────────────────────
    private static readonly Color CRoad = Color.FromRgb(0x12, 0x12, 0x22);
    private static readonly Color CRoadLine = Color.FromRgb(0x2A, 0x2A, 0x4A);
    private static readonly Color CSidewalk = Color.FromRgb(0x1A, 0x1A, 0x2E);
    private static readonly Color CBuilding1 = Color.FromRgb(0x15, 0x15, 0x30);
    private static readonly Color CBuilding2 = Color.FromRgb(0x1A, 0x18, 0x35);
    private static readonly Color CNeonCyan = Color.FromRgb(0x00, 0xFF, 0xCC);
    private static readonly Color CNeonPink = Color.FromRgb(0xFF, 0x66, 0xAA);
    private static readonly Color CNeonGold = Color.FromRgb(0xFF, 0xD7, 0x00);
    private static readonly Color CNeonRed = Color.FromRgb(0xE7, 0x4C, 0x3C);
    private static readonly Color CNeonBlue = Color.FromRgb(0x3A, 0x86, 0xFF);
    private static readonly Color CNeonPurple = Color.FromRgb(0x9B, 0x59, 0xB6);
    private static readonly Color CTrain = Color.FromRgb(0x55, 0x55, 0x80);

    // ── 필드 ──────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly WorldGenerator _worldGen = new();
    private readonly Random _rng = new();

    private enum GameState { Title, Playing, Over }
    private GameState _state = GameState.Title;

    // 플레이어
    private double _playerZ, _playerX, _playerY;
    private double _playerVY;
    private int _targetLane;
    private bool _isJumping, _isSliding;
    private double _slideTimer;
    private bool _isGrounded;

    // 스코어
    private int _score, _coins;
    private double _speed;
    private double _baseSpeed = BaseSpeed;

    // 파워업
    private bool _hasShield;
    private double _shieldTimer;
    private bool _hasMagnet;
    private double _magnetTimer;
    private int _scoreMultiplier = 1;
    private double _multiplierTimer;
    private bool _hasJetpack;
    private double _jetpackTimer;

    // 월드
    private readonly List<WorldObject> _objects = [];
    private readonly Model3DGroup _scene = new();
    private readonly List<GeometryModel3D> _envModels = [];
    private readonly Dictionary<WorldObject, GeometryModel3D> _objModels = [];
    private GeometryModel3D? _playerBody;
    private GeometryModel3D? _playerHead;
    private GeometryModel3D? _shieldVisual;
    private double _lastEnvZ = -20;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource src)
            {
                int v = 1;
                DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int));
            }
            InitScene();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 씬 초기화 ─────────────────────────────────

    private void InitScene()
    {
        _scene.Children.Add(new AmbientLight(Color.FromRgb(25, 25, 40)));
        _scene.Children.Add(new DirectionalLight(Color.FromRgb(60, 60, 90), new Vector3D(0.3, -1, 0.5)));
        _scene.Children.Add(new PointLight(CNeonCyan, new Point3D(0, 5, 0)) { Range = 40, ConstantAttenuation = 0.5 });

        var vis = new ModelVisual3D { Content = _scene };
        GameViewport.Children.Add(vis);
    }

    // ── 게임 시작 ─────────────────────────────────

    private void StartGame()
    {
        _state = GameState.Playing;
        SoundGen.PlayBgm(Sounds.Bgm);
        _playerZ = 0; _playerX = 0; _playerY = 0;
        _playerVY = 0; _targetLane = 0;
        _isJumping = false; _isSliding = false; _isGrounded = true;
        _score = 0; _coins = 0; _speed = BaseSpeed; _baseSpeed = BaseSpeed;
        _hasShield = false; _hasMagnet = false; _hasJetpack = false;
        _scoreMultiplier = 1;
        _shieldTimer = 0; _magnetTimer = 0; _multiplierTimer = 0; _jetpackTimer = 0;
        _slideTimer = 0;

        _objects.Clear();
        _worldGen.Reset();

        // 씬 클리어 (라이트 3개 유지)
        while (_scene.Children.Count > 3)
            _scene.Children.RemoveAt(_scene.Children.Count - 1);
        _envModels.Clear();
        _objModels.Clear();
        _lastEnvZ = -20;

        // 플레이어 생성 (몸통 + 머리)
        _playerBody = MeshHelper.CreateBox(0.7, 1.2, 0.5, CNeonCyan, 0.7);
        _playerHead = MeshHelper.CreateBox(0.45, 0.45, 0.45, Color.FromRgb(0xE0, 0xD0, 0xB0), 0.3);
        _shieldVisual = MeshHelper.CreateBox(1.2, 1.8, 1.0, Color.FromArgb(60, 0x3A, 0x86, 0xFF), 0.5);
        _scene.Children.Add(_playerBody);
        _scene.Children.Add(_playerHead);
        _scene.Children.Add(_shieldVisual);

        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
    }

    // ── 업데이트 ──────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        // 속도 증가
        _speed = Math.Min(MaxSpeed, _baseSpeed + _playerZ * 0.015);

        // 전진
        _playerZ += _speed * dt;
        _score += (int)(_speed * dt * _scoreMultiplier);

        // 레인 이동 (부드러운 보간)
        double targetX = _targetLane * LaneWidth;
        double laneSpeed = 14.0;
        if (Math.Abs(_playerX - targetX) > 0.05)
        {
            double dir = Math.Sign(targetX - _playerX);
            _playerX += dir * laneSpeed * dt;
            if ((dir > 0 && _playerX > targetX) || (dir < 0 && _playerX < targetX))
                _playerX = targetX;
        }

        // 중력 + 점프
        if (_hasJetpack)
        {
            _playerY = 5.0;
            _isGrounded = false;
        }
        else
        {
            _playerVY -= Gravity * dt;
            _playerY += _playerVY * dt;
            if (_playerY <= 0)
            {
                _playerY = 0;
                _playerVY = 0;
                _isGrounded = true;
                _isJumping = false;
            }
            else
            {
                _isGrounded = false;
            }
        }

        // 슬라이드 타이머
        if (_isSliding)
        {
            _slideTimer -= dt;
            if (_slideTimer <= 0) _isSliding = false;
        }

        // 파워업 타이머
        UpdatePowerUps(dt);

        // 월드 생성
        var newObjs = _worldGen.Generate(_playerZ, _speed);
        foreach (var obj in newObjs)
        {
            _objects.Add(obj);
            var model = CreateObjectModel(obj);
            _objModels[obj] = model;
            _scene.Children.Add(model);
        }

        // 환경 생성
        BuildEnvironment();

        // 충돌 체크 + 수집
        CheckCollisions();

        // 오래된 오브젝트 제거
        CleanupObjects();

        // HUD
        ScoreText.Text = _score.ToString("N0");
        DistText.Text = $"{(int)_playerZ}m";
        CoinText.Text = _coins.ToString();
        SpeedText.Text = $"{(int)(_speed * 3.6)} km/h  [±: {(int)_baseSpeed}]";
        MultText.Text = _scoreMultiplier.ToString();

        string powerText = "";
        if (_hasShield) powerText += $"SHIELD {_shieldTimer:F0}s  ";
        if (_hasMagnet) powerText += $"MAGNET {_magnetTimer:F0}s  ";
        if (_hasJetpack) powerText += $"JETPACK {_jetpackTimer:F0}s  ";
        if (_multiplierTimer > 0) powerText += $"×2 {_multiplierTimer:F0}s";
        PowerUpText.Text = powerText;
    }

    private void UpdatePowerUps(double dt)
    {
        if (_hasShield) { _shieldTimer -= dt; if (_shieldTimer <= 0) _hasShield = false; }
        if (_hasMagnet) { _magnetTimer -= dt; if (_magnetTimer <= 0) _hasMagnet = false; }
        if (_hasJetpack) { _jetpackTimer -= dt; if (_jetpackTimer <= 0) _hasJetpack = false; }
        if (_multiplierTimer > 0) { _multiplierTimer -= dt; if (_multiplierTimer <= 0) _scoreMultiplier = 1; }
    }

    private void CheckCollisions()
    {
        foreach (var obj in _objects)
        {
            if (!obj.Active) continue;

            double relZ = obj.Z - _playerZ;
            if (relZ > 3 || relZ < -1.5) continue;

            double objX = obj.Lane * LaneWidth;

            // 자석: 코인 끌어당기기
            if (_hasMagnet && obj.Kind == ObjectKind.Coin && Math.Abs(relZ) < 8)
            {
                double dist = Math.Sqrt((objX - _playerX) * (objX - _playerX) + relZ * relZ);
                if (dist < 6)
                {
                    obj.Active = false;
                    _coins++;
                    _score += 10 * _scoreMultiplier;
                    RemoveObjectVisual(obj);
                    continue;
                }
            }

            if (Math.Abs(objX - _playerX) > LaneWidth * 0.55) continue;
            if (Math.Abs(relZ) > 1.0) continue;

            if (obj.Kind == ObjectKind.Coin)
            {
                if (Math.Abs(obj.Y - (_playerY + 0.8)) < 1.5)
                {
                    obj.Active = false;
                    _coins++;
                    _score += 10 * _scoreMultiplier;
                    SoundGen.Sfx(Sounds.CoinSfx);
                    RemoveObjectVisual(obj);
                }
            }
            else if (obj.IsPowerUp)
            {
                obj.Active = false;
                ActivatePowerUp(obj.Kind);
                SoundGen.Sfx(Sounds.PowerUpSfx);
                RemoveObjectVisual(obj);
            }
            else if (obj.IsObstacle && !_hasJetpack)
            {
                bool hit = obj.Kind switch
                {
                    ObjectKind.Barrier => !_isJumping && _playerY < 1.0,
                    ObjectKind.Train => true,
                    ObjectKind.Beam => !_isSliding,
                    _ => true
                };

                if (hit)
                {
                    if (_hasShield)
                    {
                        _hasShield = false;
                        _shieldTimer = 0;
                        obj.Active = false;
                        SoundGen.Sfx(Sounds.ShieldHitSfx);
                        RemoveObjectVisual(obj);
                    }
                    else
                    {
                        Die();
                        return;
                    }
                }
            }
        }
    }

    private void ActivatePowerUp(ObjectKind kind)
    {
        switch (kind)
        {
            case ObjectKind.Magnet:
                _hasMagnet = true;
                _magnetTimer = 8;
                break;
            case ObjectKind.Shield:
                _hasShield = true;
                _shieldTimer = 10;
                break;
            case ObjectKind.Multiplier:
                _scoreMultiplier = 2;
                _multiplierTimer = 10;
                break;
            case ObjectKind.Jetpack:
                _hasJetpack = true;
                _jetpackTimer = 6;
                break;
        }
    }

    private void RemoveObjectVisual(WorldObject obj)
    {
        if (_objModels.TryGetValue(obj, out var m))
        {
            _scene.Children.Remove(m);
            _objModels.Remove(obj);
        }
    }

    private void CleanupObjects()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].Z < _playerZ - 15 || !_objects[i].Active)
            {
                RemoveObjectVisual(_objects[i]);
                _objects.RemoveAt(i);
            }
        }

        // 환경 모델 제거 (너무 뒤에 있는 것)
        for (int i = _envModels.Count - 1; i >= 0; i--)
        {
            if (_envModels[i].Transform is TranslateTransform3D t && t.OffsetZ < _playerZ - 20)
            {
                _scene.Children.Remove(_envModels[i]);
                _envModels.RemoveAt(i);
            }
            else if (_envModels[i].Transform is Transform3DGroup g &&
                     g.Children.OfType<TranslateTransform3D>().FirstOrDefault() is { } tt &&
                     tt.OffsetZ < _playerZ - 20)
            {
                _scene.Children.Remove(_envModels[i]);
                _envModels.RemoveAt(i);
            }
        }
    }

    private void Die()
    {
        _state = GameState.Over;
        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.CrashSfx);
        GOScoreText.Text = $"SCORE: {_score:N0}";
        GODistText.Text = $"DISTANCE: {(int)_playerZ}m";
        GOCoinText.Text = $"COINS: {_coins}";
        HudPanel.Visibility = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Visible;
    }

    // ── 환경 생성 ─────────────────────────────────

    private void BuildEnvironment()
    {
        double buildTo = _playerZ + VisibleSegs * SegLen;

        while (_lastEnvZ < buildTo)
        {
            _lastEnvZ += SegLen;
            double z = _lastEnvZ;

            // 도로
            var road = MeshHelper.CreatePlane(RoadWidth, SegLen, CRoad, 0.05);
            MeshHelper.SetPosition(road, 0, -0.01, z);
            _scene.Children.Add(road);
            _envModels.Add(road);

            // 도로 중앙선
            var centerLine = MeshHelper.CreatePlane(0.08, SegLen * 0.4, CNeonCyan, 0.4);
            MeshHelper.SetPosition(centerLine, 0, 0.005, z);
            _scene.Children.Add(centerLine);
            _envModels.Add(centerLine);

            // 레인 구분선
            for (int l = -1; l <= 1; l += 2)
            {
                var laneLine = MeshHelper.CreatePlane(0.05, SegLen, CRoadLine, 0.2);
                MeshHelper.SetPosition(laneLine, l * LaneWidth, 0.005, z);
                _scene.Children.Add(laneLine);
                _envModels.Add(laneLine);
            }

            // 인도
            for (int side = -1; side <= 1; side += 2)
            {
                double sideX = side * (RoadWidth / 2 + 1.0);
                var sidewalk = MeshHelper.CreateBox(2.0, 0.3, SegLen, CSidewalk, 0.05);
                MeshHelper.SetPosition(sidewalk, sideX, 0.15, z);
                _scene.Children.Add(sidewalk);
                _envModels.Add(sidewalk);
            }

            // 빌딩 (양쪽)
            if ((int)(z / SegLen) % 2 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    double bx = side * (RoadWidth / 2 + 3.5 + _rng.NextDouble() * 2);
                    double bw = 2.5 + _rng.NextDouble() * 2;
                    double bh = 6 + _rng.NextDouble() * 12;
                    var bColor = _rng.Next(2) == 0 ? CBuilding1 : CBuilding2;
                    var building = MeshHelper.CreateBox(bw, bh, SegLen * 0.8, bColor, 0.02);
                    MeshHelper.SetPosition(building, bx, bh / 2, z);
                    _scene.Children.Add(building);
                    _envModels.Add(building);

                    // 빌딩 네온 라인
                    if (_rng.NextDouble() < 0.6)
                    {
                        var neonColor = _rng.Next(3) switch
                        {
                            0 => CNeonCyan,
                            1 => CNeonPink,
                            _ => CNeonBlue
                        };
                        double ny = 1 + _rng.NextDouble() * (bh - 2);
                        var neon = MeshHelper.CreateBox(bw + 0.1, 0.08, 0.1, neonColor, 1.0);
                        MeshHelper.SetPosition(neon, bx, ny, z - SegLen * 0.4 * side);
                        _scene.Children.Add(neon);
                        _envModels.Add(neon);
                    }

                    // 빌딩 창문 (작은 발광 사각형들)
                    if (bh > 8 && _rng.NextDouble() < 0.5)
                    {
                        double winY = 3 + _rng.NextDouble() * (bh - 5);
                        var windowColor = Color.FromArgb(100, 0xFF, 0xEE, 0x88);
                        var win = MeshHelper.CreateBox(0.4, 0.5, 0.1, windowColor, 0.8);
                        MeshHelper.SetPosition(win, bx - side * bw * 0.3, winY, z - SegLen * 0.4 * side);
                        _scene.Children.Add(win);
                        _envModels.Add(win);
                    }
                }
            }

            // 가로등 (일정 간격)
            if ((int)(z / SegLen) % 3 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    double lx = side * (RoadWidth / 2 + 0.3);
                    var pole = MeshHelper.CreateCylinder(0.06, 3.5, 8, Color.FromRgb(0x40, 0x40, 0x50), 0.1);
                    MeshHelper.SetPosition(pole, lx, 1.75, z);
                    _scene.Children.Add(pole);
                    _envModels.Add(pole);

                    var lamp = MeshHelper.CreateBox(0.3, 0.15, 0.3,
                        side > 0 ? CNeonPink : CNeonCyan, 1.0);
                    MeshHelper.SetPosition(lamp, lx, 3.6, z);
                    _scene.Children.Add(lamp);
                    _envModels.Add(lamp);
                }
            }
        }
    }

    // ── 렌더 ──────────────────────────────────────

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // 카메라
        double camHeight = _hasJetpack ? 8.0 : 4.5;
        double camBack = _hasJetpack ? 12.0 : 8.0;
        Camera.Position = new Point3D(
            _playerX * 0.25,
            camHeight + _playerY * 0.3,
            _playerZ - camBack);
        Camera.LookDirection = new Vector3D(
            (_playerX - Camera.Position.X) * 0.15,
            -0.25 - _playerY * 0.02,
            1);

        // 플레이어 몸통
        if (_playerBody != null)
        {
            double bodyH = _isSliding ? 0.4 : 1.2;
            double bodyY = _playerY + (_isSliding ? 0.2 : 0.6);

            var bodyGroup = new Transform3DGroup();
            if (_isSliding)
                bodyGroup.Children.Add(new ScaleTransform3D(1.2, 0.35, 1.5));
            bodyGroup.Children.Add(new TranslateTransform3D(_playerX, bodyY, _playerZ));
            _playerBody.Transform = bodyGroup;
        }

        // 플레이어 머리
        if (_playerHead != null)
        {
            double headY = _playerY + (_isSliding ? 0.55 : 1.45);
            _playerHead.Transform = new TranslateTransform3D(_playerX, headY, _playerZ);
            _playerHead.Material = _isSliding
                ? new DiffuseMaterial(Brushes.Transparent)
                : MeshHelper.CreateBox(0.1, 0.1, 0.1, Color.FromRgb(0xE0, 0xD0, 0xB0), 0.3).Material;
        }

        // 쉴드 비주얼
        if (_shieldVisual != null)
        {
            _shieldVisual.Transform = new TranslateTransform3D(_playerX, _playerY + 0.9, _playerZ);
            // 쉴드가 없으면 투명
            if (!_hasShield)
                _shieldVisual.Material = new DiffuseMaterial(Brushes.Transparent);
            else
                _shieldVisual.Material = new EmissiveMaterial(
                    new SolidColorBrush(Color.FromArgb(40, 0x3A, 0x86, 0xFF)));
        }

        // 포인트 라이트 추적
        if (_scene.Children.Count > 2 && _scene.Children[2] is PointLight pl)
            pl.Position = new Point3D(_playerX, 5, _playerZ + 5);

        // 오브젝트 위치 업데이트
        foreach (var (obj, model) in _objModels)
        {
            double x = obj.Lane * LaneWidth;
            double y = obj.Y;

            // 코인 회전 + 부유 애니메이션
            if (obj.Kind == ObjectKind.Coin)
            {
                y += Math.Sin(_playerZ * 1.5 + obj.Z) * 0.15;
                MeshHelper.SetPositionAndRotation(model, x, y, obj.Z,
                    (_playerZ * 100 + obj.Z * 40) % 360);
            }
            else if (obj.IsPowerUp)
            {
                y += Math.Sin(_playerZ * 2 + obj.Z) * 0.2;
                MeshHelper.SetPositionAndRotation(model, x, y, obj.Z,
                    (_playerZ * 60) % 360);
            }
            else
            {
                MeshHelper.SetPosition(model, x, y, obj.Z);
            }
        }
    }

    // ── 입력 ──────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_state == GameState.Playing)
        {
            switch (e.Key)
            {
                case Key.Left or Key.A:
                    if (_targetLane < 1) _targetLane++;
                    break;
                case Key.Right or Key.D:
                    if (_targetLane > -1) _targetLane--;
                    break;
                case Key.OemPlus or Key.Add:
                    _baseSpeed = Math.Min(35.0, _baseSpeed + 2.5);
                    break;
                case Key.OemMinus or Key.Subtract:
                    _baseSpeed = Math.Max(10.0, _baseSpeed - 2.5);
                    break;
                case Key.Up or Key.Space or Key.W:
                    if (_isGrounded && !_hasJetpack)
                    {
                        _playerVY = JumpForce;
                        _isJumping = true;
                        _isGrounded = false;
                        _isSliding = false;
                        SoundGen.Sfx(Sounds.JumpSfx);
                    }
                    break;
                case Key.Down or Key.S:
                    if (!_isJumping || _playerY < 0.5)
                    {
                        _isSliding = true;
                        _slideTimer = SlideTime;
                        SoundGen.Sfx(Sounds.SlideSfx);
                        if (_isJumping)
                        {
                            _playerVY = -15;
                        }
                    }
                    break;
            }
        }

        if (e.Key == Key.Return)
        {
            if (_state == GameState.Title || _state == GameState.Over)
                StartGame();
        }
        if (e.Key == Key.Escape)
        {
            if (_state == GameState.Playing) Die();
            else if (_state == GameState.Over)
            {
                _state = GameState.Title;
                GameOverPanel.Visibility = Visibility.Collapsed;
                HudPanel.Visibility = Visibility.Collapsed;
                TitlePanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) { }

    // ── 오브젝트 모델 ─────────────────────────────

    private GeometryModel3D CreateObjectModel(WorldObject obj)
    {
        return obj.Kind switch
        {
            ObjectKind.Barrier => MeshHelper.CreateBox(LaneWidth * 0.85, 1.3, 0.7, CNeonRed, 0.8),
            ObjectKind.Train => MeshHelper.CreateBox(LaneWidth * 0.9, 3.0, 4.0, CTrain, 0.15),
            ObjectKind.Beam => MeshHelper.CreateBox(RoadWidth, 0.35, 0.4,
                Color.FromRgb(0xFF, 0x88, 0x00), 0.8),

            ObjectKind.Coin => MeshHelper.CreateCylinder(0.4, 0.12, 8, CNeonGold, 1.0),

            ObjectKind.Magnet => MeshHelper.CreateBox(0.6, 0.6, 0.6, CNeonRed, 0.8),
            ObjectKind.Shield => MeshHelper.CreateBox(0.6, 0.6, 0.6, CNeonBlue, 0.8),
            ObjectKind.Multiplier => MeshHelper.CreateBox(0.6, 0.6, 0.6, CNeonPink, 0.8),
            ObjectKind.Jetpack => MeshHelper.CreateBox(0.6, 0.6, 0.6, CNeonPurple, 0.8),

            _ => MeshHelper.CreateBox(0.5, 0.5, 0.5, CNeonCyan, 0.5)
        };
    }
}
