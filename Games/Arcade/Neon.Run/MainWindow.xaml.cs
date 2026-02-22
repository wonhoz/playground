using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using NeonRun.Engine;

namespace NeonRun;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 상수 ──────────────────────────────────────
    private const double LaneWidth = 2.0;      // 레인 간격
    private const double TunnelWidth = 8.0;     // 터널 폭
    private const double TunnelHeight = 5.0;    // 터널 높이
    private const double SegmentLength = 4.0;   // 터널 세그먼트 길이
    private const int VisibleSegments = 25;     // 앞에 보이는 세그먼트 수
    private const double PlayerY = 0.5;         // 플레이어 기본 Y
    private const double JumpHeight = 2.5;      // 점프 높이
    private const double JumpDuration = 0.6;    // 점프 시간
    private const double LaneMoveSpeed = 10.0;  // 레인 이동 속도

    // ── 엔진 ──────────────────────────────────────
    private readonly GameLoop _loop = new();
    private readonly TrackGenerator _trackGen = new();
    private readonly List<Obstacle> _obstacles = [];

    // ── 상태 ──────────────────────────────────────
    private enum GameState { Title, Playing, GameOver }
    private GameState _state = GameState.Title;

    private double _playerZ;           // 전진 거리
    private double _playerX;           // 현재 X (-LaneWidth, 0, +LaneWidth)
    private int _targetLane;           // 목표 레인 (-1, 0, 1)
    private double _speed;             // 현재 속도 (units/s)
    private double _baseSpeed = 15.0;
    private const double MinBaseSpeed = 8.0;
    private const double MaxBaseSpeed = 35.0;
    private int _crystals;
    private int _score;
    private double _jumpTimer;         // 점프 진행 시간 (-1 = 안 점프)
    private bool _isJumping;
    private double _playerVisualY;

    // 입력 상태는 KeyDown/KeyUp에서 직접 처리

    // 3D 비주얼
    private readonly Model3DGroup _sceneGroup = new();
    private readonly List<GeometryModel3D> _tunnelModels = [];
    private readonly List<GeometryModel3D> _floorGridModels = [];
    private readonly Dictionary<Obstacle, GeometryModel3D> _obstacleModels = [];
    private GeometryModel3D? _playerModel;

    // 터널 네온 색상
    private static readonly Color NeonCyan = Color.FromRgb(0x00, 0xFF, 0xCC);
    private static readonly Color NeonPink = Color.FromRgb(0xFF, 0x66, 0xAA);
    private static readonly Color NeonBlue = Color.FromRgb(0x3A, 0x86, 0xFF);
    private static readonly Color NeonGold = Color.FromRgb(0xFF, 0xD7, 0x00);
    private static readonly Color NeonRed = Color.FromRgb(0xE7, 0x4C, 0x3C);

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
            InitScene();
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 3D 씬 초기화 ──────────────────────────────

    private void InitScene()
    {
        _sceneGroup.Children.Clear();

        // 앰비언트 라이트
        _sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(30, 30, 50)));

        // 디렉셔널 라이트
        _sceneGroup.Children.Add(new DirectionalLight(
            Color.FromRgb(80, 80, 120),
            new Vector3D(0.2, -1, 0.5)));

        // 포인트 라이트 (플레이어 주변)
        _sceneGroup.Children.Add(new PointLight(
            Color.FromRgb(0, 200, 180), new Point3D(0, 2, 0))
        { Range = 30 });

        var visual = new ModelVisual3D { Content = _sceneGroup };
        GameViewport.Children.Add(visual);
    }

    // ── 게임 시작 ──────────────────────────────────

    private void StartGame()
    {
        _state = GameState.Playing;
        SoundGen.PlayBgm(Sounds.Bgm);
        _playerZ = 0;
        _playerX = 0;
        _targetLane = 0;
        _speed = _baseSpeed;
        _crystals = 0;
        _score = 0;
        _jumpTimer = -1;
        _isJumping = false;
        _playerVisualY = PlayerY;

        _obstacles.Clear();
        _trackGen.Reset();

        // 기존 비주얼 클리어 (라이트 3개 유지)
        while (_sceneGroup.Children.Count > 3)
            _sceneGroup.Children.RemoveAt(_sceneGroup.Children.Count - 1);
        _tunnelModels.Clear();
        _floorGridModels.Clear();
        _obstacleModels.Clear();

        // 플레이어 생성
        _playerModel = CreateBox(0.6, 0.6, 0.6, NeonCyan, 0.8);
        _sceneGroup.Children.Add(_playerModel);

        // 초기 터널 세그먼트
        BuildTunnelSegments();

        TitlePanel.Visibility = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        MultiplierText.Visibility = Visibility.Collapsed;
    }

    // ── 터널 구축 ──────────────────────────────────

    private void BuildTunnelSegments()
    {
        // 기존 터널 제거
        foreach (var m in _tunnelModels) _sceneGroup.Children.Remove(m);
        foreach (var m in _floorGridModels) _sceneGroup.Children.Remove(m);
        _tunnelModels.Clear();
        _floorGridModels.Clear();

        double startZ = Math.Floor(_playerZ / SegmentLength) * SegmentLength;

        for (int i = -2; i < VisibleSegments; i++)
        {
            double z = startZ + i * SegmentLength;
            AddTunnelSegment(z);
        }
    }

    private void AddTunnelSegment(double z)
    {
        double halfW = TunnelWidth / 2;
        byte pulse = (byte)(40 + (int)(Math.Abs(Math.Sin(z * 0.15)) * 30));

        // 바닥
        var floor = CreatePlane(TunnelWidth, SegmentLength,
            Color.FromArgb(255, 8, 8, (byte)(20 + pulse / 4)));
        SetTransform(floor, 0, 0, z + SegmentLength / 2);
        _sceneGroup.Children.Add(floor);
        _tunnelModels.Add(floor);

        // 바닥 그리드 라인 (네온)
        var gridLine = CreatePlane(TunnelWidth, 0.05, Color.FromArgb(pulse, NeonCyan.R, NeonCyan.G, NeonCyan.B));
        SetTransform(gridLine, 0, 0.01, z);
        _sceneGroup.Children.Add(gridLine);
        _floorGridModels.Add(gridLine);

        // 레인 구분선
        for (int lane = -1; lane <= 1; lane += 2)
        {
            var laneLine = CreatePlane(0.03, SegmentLength,
                Color.FromArgb((byte)(pulse / 2), NeonPink.R, NeonPink.G, NeonPink.B));
            SetTransform(laneLine, lane * LaneWidth, 0.01, z + SegmentLength / 2);
            _sceneGroup.Children.Add(laneLine);
            _floorGridModels.Add(laneLine);
        }

        // 좌우 벽
        var leftWall = CreateWallPlane(SegmentLength, TunnelHeight,
            Color.FromArgb((byte)(15 + pulse / 3), NeonBlue.R, NeonBlue.G, NeonBlue.B));
        SetTransformWall(leftWall, -halfW, TunnelHeight / 2, z + SegmentLength / 2, true);
        _sceneGroup.Children.Add(leftWall);
        _tunnelModels.Add(leftWall);

        var rightWall = CreateWallPlane(SegmentLength, TunnelHeight,
            Color.FromArgb((byte)(15 + pulse / 3), NeonPink.R, NeonPink.G, NeonPink.B));
        SetTransformWall(rightWall, halfW, TunnelHeight / 2, z + SegmentLength / 2, false);
        _sceneGroup.Children.Add(rightWall);
        _tunnelModels.Add(rightWall);

        // 벽 네온 스트라이프
        var leftStripe = CreateWallPlane(SegmentLength, 0.08,
            Color.FromArgb(pulse, NeonBlue.R, NeonBlue.G, NeonBlue.B));
        SetTransformWall(leftStripe, -halfW + 0.01, 1.0, z + SegmentLength / 2, true);
        _sceneGroup.Children.Add(leftStripe);
        _floorGridModels.Add(leftStripe);

        var rightStripe = CreateWallPlane(SegmentLength, 0.08,
            Color.FromArgb(pulse, NeonPink.R, NeonPink.G, NeonPink.B));
        SetTransformWall(rightStripe, halfW - 0.01, 1.0, z + SegmentLength / 2, false);
        _sceneGroup.Children.Add(rightStripe);
        _floorGridModels.Add(rightStripe);

        // 천장
        var ceiling = CreatePlane(TunnelWidth, SegmentLength,
            Color.FromArgb(255, 5, 5, 15));
        SetTransform(ceiling, 0, TunnelHeight, z + SegmentLength / 2);
        _sceneGroup.Children.Add(ceiling);
        _tunnelModels.Add(ceiling);
    }

    // ── 업데이트 ──────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        // 속도 증가 (시간에 따라)
        _speed = _baseSpeed + _playerZ * 0.02;
        double cappedSpeed = Math.Min(_speed, 60);

        // 전진
        _playerZ += cappedSpeed * dt;

        // 레인 이동
        double targetX = _targetLane * LaneWidth;
        if (Math.Abs(_playerX - targetX) > 0.01)
        {
            double dir = Math.Sign(targetX - _playerX);
            _playerX += dir * LaneMoveSpeed * dt;
            if ((dir > 0 && _playerX > targetX) || (dir < 0 && _playerX < targetX))
                _playerX = targetX;
        }

        // 점프
        if (_isJumping)
        {
            _jumpTimer += dt;
            if (_jumpTimer >= JumpDuration)
            {
                _isJumping = false;
                _jumpTimer = -1;
                _playerVisualY = PlayerY;
            }
            else
            {
                double t = _jumpTimer / JumpDuration;
                _playerVisualY = PlayerY + JumpHeight * Math.Sin(t * Math.PI);
            }
        }

        // 장애물 생성
        var newObstacles = _trackGen.Update(_playerZ, cappedSpeed, _obstacles);
        foreach (var obs in newObstacles)
        {
            _obstacles.Add(obs);
            var model = CreateObstacleModel(obs);
            _obstacleModels[obs] = model;
            _sceneGroup.Children.Add(model);
        }

        // 충돌 체크
        foreach (var obs in _obstacles)
        {
            if (obs.Passed || obs.Collected) continue;

            double relZ = obs.Z - _playerZ;
            if (relZ > 2 || relZ < -1) continue;

            // 같은 레인인지 체크
            double obsX = obs.Lane * LaneWidth;
            if (Math.Abs(_playerX - obsX) > LaneWidth * 0.6) continue;

            if (Math.Abs(relZ) < 0.8)
            {
                if (obs.Type == ObstacleType.Crystal)
                {
                    obs.Collected = true;
                    _crystals++;
                    SoundGen.Sfx(Sounds.CrystalSfx);
                    _score += 50;
                    if (_obstacleModels.TryGetValue(obs, out var cm))
                    {
                        _sceneGroup.Children.Remove(cm);
                        _obstacleModels.Remove(obs);
                    }
                }
                else if (obs.Type == ObstacleType.Wall)
                {
                    // 벽은 못 피함
                    GameOver();
                    return;
                }
                else if (obs.Type == ObstacleType.LowBar)
                {
                    // 낮은 바: 점프로 회피 가능
                    if (_playerVisualY < PlayerY + 1.0)
                    {
                        GameOver();
                        return;
                    }
                }
            }
        }

        // 지나간 장애물 처리
        foreach (var obs in _obstacles)
        {
            if (!obs.Passed && obs.Z < _playerZ - 5)
            {
                obs.Passed = true;
                if (obs.Type != ObstacleType.Crystal)
                    _score += 10;
                if (_obstacleModels.TryGetValue(obs, out var pm))
                {
                    _sceneGroup.Children.Remove(pm);
                    _obstacleModels.Remove(obs);
                }
            }
        }

        // 터널 리빌드 (일정 거리마다)
        BuildTunnelSegments();

        // HUD
        DistText.Text = ((int)_playerZ).ToString();
        SpeedText.Text = $"{(int)(cappedSpeed * 3.6)}  [±: {(int)_baseSpeed}]";
        CrystalText.Text = _crystals.ToString();

        int multiplier = 1 + (int)(_playerZ / 100);
        if (multiplier > 1)
        {
            MultiplierText.Text = $"×{multiplier}";
            MultiplierText.Visibility = Visibility.Visible;
        }
    }

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // 카메라 위치 (플레이어 뒤)
        Camera.Position = new Point3D(_playerX * 0.3, 2.5, _playerZ - 5);
        Camera.LookDirection = new Vector3D(_playerX * 0.1 - Camera.Position.X * 0.1, -0.3, 1);

        // 플레이어 위치
        if (_playerModel != null)
        {
            var group = new Transform3DGroup();
            group.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), _playerZ * 50 % 360)));
            group.Children.Add(new TranslateTransform3D(_playerX, _playerVisualY, _playerZ));
            _playerModel.Transform = group;
        }

        // 장애물 위치
        foreach (var (obs, model) in _obstacleModels)
        {
            double x = obs.Lane * LaneWidth;
            double y = obs.Type switch
            {
                ObstacleType.Crystal => 1.0 + Math.Sin(_playerZ * 2 + obs.Z) * 0.3,
                ObstacleType.LowBar => 0.4,
                _ => 1.2
            };
            var t = new Transform3DGroup();
            if (obs.Type == ObstacleType.Crystal)
            {
                t.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 1, 0), (_playerZ * 80 + obs.Z * 30) % 360)));
            }
            t.Children.Add(new TranslateTransform3D(x, y, obs.Z));
            model.Transform = t;
        }

        // 포인트 라이트를 플레이어 근처로
        if (_sceneGroup.Children.Count > 2 && _sceneGroup.Children[2] is PointLight pl)
        {
            pl.Position = new Point3D(_playerX, 2, _playerZ);
        }
    }

    // ── 게임 오버 ─────────────────────────────────

    private void GameOver()
    {
        _state = GameState.GameOver;
        SoundGen.StopBgm();
        SoundGen.Sfx(Sounds.CrashSfx);
        int multiplier = 1 + (int)(_playerZ / 100);
        int finalScore = _score * multiplier;
        FinalDistText.Text = $"DISTANCE: {(int)_playerZ}m";
        FinalCrystalText.Text = $"CRYSTALS: {_crystals}";
        FinalScoreText.Text = $"SCORE: {finalScore:N0}";
        HudPanel.Visibility = Visibility.Collapsed;
        MultiplierText.Visibility = Visibility.Collapsed;
        GameOverPanel.Visibility = Visibility.Visible;
    }

    // ── 입력 ──────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.A:
                if (_state == GameState.Playing && _targetLane < 1)
                    _targetLane++;
                break;
            case Key.Right or Key.D:
                if (_state == GameState.Playing && _targetLane > -1)
                    _targetLane--;
                break;
            case Key.OemPlus or Key.Add:
                if (_state == GameState.Playing)
                    _baseSpeed = Math.Min(MaxBaseSpeed, _baseSpeed + 2.5);
                break;
            case Key.OemMinus or Key.Subtract:
                if (_state == GameState.Playing)
                    _baseSpeed = Math.Max(MinBaseSpeed, _baseSpeed - 2.5);
                break;
            case Key.Space:
                if (_state == GameState.Playing && !_isJumping)
                {
                    _isJumping = true;
                    _jumpTimer = 0;
                    SoundGen.Sfx(Sounds.JumpSfx);
                }
                break;
            case Key.Return:
                if (_state == GameState.Title || _state == GameState.GameOver)
                    StartGame();
                break;
            case Key.Escape:
                if (_state == GameState.Playing)
                    GameOver();
                else if (_state == GameState.GameOver)
                {
                    _state = GameState.Title;
                    GameOverPanel.Visibility = Visibility.Collapsed;
                    HudPanel.Visibility = Visibility.Collapsed;
                    TitlePanel.Visibility = Visibility.Visible;
                }
                break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) { }

    // ── 타이틀 속도 선택 ──────────────────────────
    private void SpeedSlow_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _baseSpeed = 10.0; UpdateSpeedButtons(); }
    private void SpeedNorm_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _baseSpeed = 15.0; UpdateSpeedButtons(); }
    private void SpeedFast_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _baseSpeed = 22.0; UpdateSpeedButtons(); }

    private void UpdateSpeedButtons()
    {
        var active = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xCC));
        var dim    = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66));
        SpeedSlowBtn.BorderBrush = _baseSpeed < 12 ? new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF)) : dim;
        SpeedNormBtn.BorderBrush = _baseSpeed is >= 12 and <= 18 ? active : dim;
        SpeedFastBtn.BorderBrush = _baseSpeed > 18 ? new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0xAA)) : dim;
        SpeedSlowBtn.BorderThickness = new Thickness(_baseSpeed < 12 ? 3 : 1);
        SpeedNormBtn.BorderThickness = new Thickness(_baseSpeed is >= 12 and <= 18 ? 3 : 1);
        SpeedFastBtn.BorderThickness = new Thickness(_baseSpeed > 18 ? 3 : 1);
    }

    // ── 3D 메시 헬퍼 ──────────────────────────────

    private static GeometryModel3D CreateBox(double w, double h, double d, Color color, double emissive = 0)
    {
        double hw = w / 2, hh = h / 2, hd = d / 2;
        var mesh = new MeshGeometry3D();

        // 6면 쿼드 (삼각형 12개)
        Point3D[] corners =
        [
            new(-hw, -hh, -hd), new( hw, -hh, -hd), new( hw,  hh, -hd), new(-hw,  hh, -hd),
            new(-hw, -hh,  hd), new( hw, -hh,  hd), new( hw,  hh,  hd), new(-hw,  hh,  hd)
        ];

        int[][] faces =
        [
            [0,1,2,3], [5,4,7,6], [4,0,3,7], [1,5,6,2], [3,2,6,7], [4,5,1,0]
        ];

        Vector3D[] normals =
        [
            new(0,0,-1), new(0,0,1), new(-1,0,0), new(1,0,0), new(0,1,0), new(0,-1,0)
        ];

        for (int f = 0; f < 6; f++)
        {
            int baseIdx = mesh.Positions.Count;
            for (int v = 0; v < 4; v++)
            {
                mesh.Positions.Add(corners[faces[f][v]]);
                mesh.Normals.Add(normals[f]);
            }
            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 3);
        }

        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        if (emissive > 0)
        {
            byte er = (byte)(color.R * emissive);
            byte eg = (byte)(color.G * emissive);
            byte eb = (byte)(color.B * emissive);
            mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(er, eg, eb))));
        }

        return new GeometryModel3D(mesh, mat);
    }

    private static GeometryModel3D CreatePlane(double w, double d, Color color)
    {
        double hw = w / 2, hd = d / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = { new(-hw, 0, -hd), new(hw, 0, -hd), new(hw, 0, hd), new(-hw, 0, hd) },
            Normals = { new(0, 1, 0), new(0, 1, 0), new(0, 1, 0), new(0, 1, 0) },
            TriangleIndices = { 0, 1, 2, 0, 2, 3 }
        };

        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        byte er = (byte)Math.Min(255, color.R / 3);
        byte eg = (byte)Math.Min(255, color.G / 3);
        byte eb = (byte)Math.Min(255, color.B / 3);
        mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(color.A, er, eg, eb))));

        return new GeometryModel3D(mesh, mat) { BackMaterial = mat };
    }

    private static GeometryModel3D CreateWallPlane(double d, double h, Color color)
    {
        double hd = d / 2, hh = h / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = { new(0, -hh, -hd), new(0, -hh, hd), new(0, hh, hd), new(0, hh, -hd) },
            Normals = { new(1, 0, 0), new(1, 0, 0), new(1, 0, 0), new(1, 0, 0) },
            TriangleIndices = { 0, 1, 2, 0, 2, 3 }
        };

        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        byte er = (byte)Math.Min(255, color.R / 3);
        byte eg = (byte)Math.Min(255, color.G / 3);
        byte eb = (byte)Math.Min(255, color.B / 3);
        mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(color.A, er, eg, eb))));

        return new GeometryModel3D(mesh, mat) { BackMaterial = mat };
    }

    private static void SetTransform(GeometryModel3D model, double x, double y, double z)
    {
        model.Transform = new TranslateTransform3D(x, y, z);
    }

    private static void SetTransformWall(GeometryModel3D model, double x, double y, double z, bool faceRight)
    {
        var group = new Transform3DGroup();
        if (!faceRight)
            group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 180)));
        group.Children.Add(new TranslateTransform3D(x, y, z));
        model.Transform = group;
    }

    private GeometryModel3D CreateObstacleModel(Obstacle obs)
    {
        return obs.Type switch
        {
            ObstacleType.Wall => CreateBox(LaneWidth * 0.9, 3.0, 0.6, NeonRed, 0.8),
            ObstacleType.LowBar => CreateBox(LaneWidth * 0.9, 0.55, 0.5, Color.FromRgb(0xFF, 0xA0, 0x00), 0.7),
            ObstacleType.Crystal => CreateBox(0.6, 0.6, 0.6, NeonGold, 1.0),
            _ => CreateBox(0.5, 0.5, 0.5, NeonCyan, 0.5)
        };
    }
}
