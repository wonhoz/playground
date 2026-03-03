using System.Runtime.InteropServices;
using System.Windows.Interop;
using StackCrash.Game;
using StackCrash.Levels;
using StackCrash.Models;

namespace StackCrash;

public partial class MainWindow : Window
{
    // ── 물리 상수 ────────────────────────────────────────────────────
    private const double PPM           = 60.0;
    private const double GROUND_OFFSET = 40.0;    // 캔버스 하단에서 지면까지 px
    private const float  STEP_DT       = 1f / 60f;

    // ── DWM 다크 타이틀바 ────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 상태 ─────────────────────────────────────────────────────────
    private GameEngine       _engine   = new();
    private LevelDef?        _level;
    private int              _levelIdx = 0;
    private int              _movesUsed;
    private bool             _isRunning;
    private bool             _gameOver;
    private DispatcherTimer? _timer;

    // ── 캔버스 좌표 ──────────────────────────────────────────────────
    private double _groundScreenY;
    private double _canvasCenterX;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));

        // 레벨 콤보 박스 초기화
        for (int i = 0; i < LevelData.All.Count; i++)
            CmbLevel.Items.Add($"Level {i + 1}: {LevelData.All[i].Name}");
        CmbLevel.SelectedIndex = 0;

        // 재질 범례
        BuildLegend();

        // 게임 루프 타이머
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(STEP_DT)
        };
        _timer.Tick += GameLoop;
        _timer.Start();
    }

    // ── 게임 루프 ────────────────────────────────────────────────────
    private void GameLoop(object? sender, EventArgs e)
    {
        if (!_isRunning || _gameOver) return;

        _engine.Step(STEP_DT);

        // 비주얼 동기화
        foreach (var block in _engine.Blocks)
            block.SyncVisual(_groundScreenY, _canvasCenterX);

        // 정착 판정
        if (_engine.IsSettled())
        {
            _engine.PurgeDeadBlocks(GameCanvas);
            UpdateBlockCount();
            CheckGameEnd();
        }
    }

    // ── 레벨 로드 ────────────────────────────────────────────────────
    private void LoadLevel(int idx)
    {
        _levelIdx  = idx;
        _level     = LevelData.All[idx];
        _movesUsed = 0;
        _isRunning = false;
        _gameOver  = false;

        ResultPanel.Visibility = Visibility.Collapsed;
        LosePanel.Visibility   = Visibility.Collapsed;
        BtnSimulate.IsEnabled  = true;
        BtnSimulate.Content    = "▶ 시뮬레이션";

        GameCanvas.Children.Clear();

        // 좌표 계산 (캔버스 크기 사용)
        UpdateCanvasCoords();

        // 엔진 초기화
        _engine = new GameEngine();
        _engine.BlockDestroyed += OnBlockDestroyed;
        _engine.BlockExploded  += OnBlockExploded;
        _engine.LoadLevel(_level);

        // 블록 생성
        foreach (var def in _level.Blocks)
            _engine.CreateBlock(def, GameCanvas, _groundScreenY, _canvasCenterX);

        // 지면 비주얼
        DrawGround();

        // UI 업데이트
        TxtLevelName.Text = $"Level {idx + 1}: {_level.Name}";
        TxtLevelDesc.Text = _level.Description;
        TxtMoves.Text     = "0";
        TxtMaxMoves.Text  = _level.MaxMoves > 0 ? $"/ {_level.MaxMoves}" : "/ ∞";
        TxtStatus.Text    = "블록을 클릭해 제거하고 ▶ 시뮬레이션을 실행하세요.";
        UpdateBlockCount();
        BuildStarPanel();
    }

    // ── 블록 클릭 제거 ───────────────────────────────────────────────
    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_gameOver) return;
        if (_isRunning) return;   // 시뮬레이션 중 클릭 금지

        var pos = e.GetPosition(GameCanvas);
        var hit = VisualTreeHelper.HitTest(GameCanvas, pos);
        if (hit is null) return;

        // 클릭한 비주얼의 DataContext(Tag)에서 GameBlock 찾기
        var element = hit.VisualHit as FrameworkElement;
        while (element is not null)
        {
            if (element.Tag is GameBlock block && !block.IsRemoved)
            {
                _movesUsed++;
                TxtMoves.Text = _movesUsed.ToString();
                _engine.RemoveBlock(block, GameCanvas);
                UpdateBlockCount();

                // 제거 후 이동 수 초과 확인 (승리 조건 아닌 경우)
                var state = _engine.CheckWin(_movesUsed);
                if (state == WinState.Lost) ShowLose();
                break;
            }
            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }
    }

    // ── 시뮬레이션 시작/정지 ────────────────────────────────────────
    private void BtnSimulate_Click(object sender, RoutedEventArgs e)
    {
        if (_level is null || _gameOver) return;

        _isRunning = !_isRunning;

        if (_isRunning)
        {
            _engine.StartSimulation();
            BtnSimulate.Content = "⏸ 일시정지";
            TxtStatus.Text      = "시뮬레이션 실행 중...";
        }
        else
        {
            _engine.StopSimulation();
            BtnSimulate.Content = "▶ 시뮬레이션";
            TxtStatus.Text      = "일시정지됨. 블록을 클릭하거나 다시 실행하세요.";
        }
    }

    // ── 리셋 ─────────────────────────────────────────────────────────
    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        if (_level is not null)
            LoadLevel(_levelIdx);
    }

    // ── 다음 레벨 ────────────────────────────────────────────────────
    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        int next = _levelIdx + 1;
        if (next < LevelData.All.Count)
        {
            CmbLevel.SelectedIndex = next;
        }
        else
        {
            TxtStatus.Text = "모든 레벨 완료! 처음부터 다시 도전해보세요.";
            CmbLevel.SelectedIndex = 0;
        }
    }

    // ── 레벨 선택 ────────────────────────────────────────────────────
    private void CmbLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        LoadLevel(CmbLevel.SelectedIndex);
    }

    // ── 캔버스 크기 변경 ─────────────────────────────────────────────
    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasCoords();
        if (_level is not null)
        {
            // 지면 재배치
            DrawGround();
            // 블록 비주얼 재동기화
            foreach (var b in _engine.Blocks)
                b.SyncVisual(_groundScreenY, _canvasCenterX);
        }
    }

    private void UpdateCanvasCoords()
    {
        _groundScreenY = GameCanvas.ActualHeight - GROUND_OFFSET;
        _canvasCenterX = GameCanvas.ActualWidth  / 2.0;
        if (_groundScreenY <= 0) _groundScreenY = 500;
        if (_canvasCenterX <= 0) _canvasCenterX = 350;
    }

    // ── 승리/패배 판정 ───────────────────────────────────────────────
    private void CheckGameEnd()
    {
        var state = _engine.CheckWin(_movesUsed);
        if (state == WinState.Won)
        {
            _engine.StopSimulation();
            _isRunning = false;
            _gameOver  = true;
            ShowWin();
        }
        else if (state == WinState.Lost)
        {
            _engine.StopSimulation();
            _isRunning = false;
            _gameOver  = true;
            ShowLose();
        }
    }

    private void ShowWin()
    {
        int stars = _engine.CalcStars(_movesUsed);
        TxtResult.Text  = "성공!";
        TxtStars.Text   = new string('★', stars) + new string('☆', 3 - stars);
        TxtStars.Foreground = (SolidColorBrush)FindResource("YellowBrush");
        ResultPanel.Visibility = Visibility.Visible;
        LosePanel.Visibility   = Visibility.Collapsed;
        BtnSimulate.IsEnabled  = false;
        TxtStatus.Text = $"레벨 클리어! {stars}성 달성 🎉";
    }

    private void ShowLose()
    {
        LosePanel.Visibility   = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        BtnSimulate.IsEnabled  = false;
        TxtStatus.Text = "이동 수 초과! 다시 도전하세요.";
    }

    // ── 블록 이벤트 핸들러 ───────────────────────────────────────────
    private void OnBlockDestroyed(GameBlock block)
    {
        // 파괴 이펙트 (간단한 투명도 애니메이션은 이미 Visual 제거로 처리됨)
    }

    private void OnBlockExploded(GameBlock block)
    {
        TxtStatus.Text = "💥 폭발 연쇄!";
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────
    private void UpdateBlockCount()
    {
        int total     = _engine.Blocks.Count;
        int remaining = _engine.Blocks.Count(b => !b.IsRemoved);
        TxtBlockCount.Text = $"블록: {remaining}/{total}";
    }

    private void DrawGround()
    {
        // 기존 지면 제거
        var old = GameCanvas.Children.OfType<Rectangle>()
                            .Where(r => r.Tag is "ground").ToList();
        foreach (var r in old) GameCanvas.Children.Remove(r);

        double w = Math.Max(GameCanvas.ActualWidth, 700);
        var ground = new Rectangle
        {
            Width  = w,
            Height = GROUND_OFFSET,
            Tag    = "ground",
            Fill   = new SolidColorBrush(Color.FromRgb(0x2D, 0x33, 0x3B)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x4C, 0x56)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(ground, 0);
        Canvas.SetTop (ground, _groundScreenY);
        GameCanvas.Children.Insert(0, ground);
    }

    private void BuildStarPanel()
    {
        if (_level is null) return;
        StarPanel.Children.Clear();

        void AddRow(string stars, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = stars,
                Foreground = (SolidColorBrush)FindResource("YellowBrush"),
                FontSize = 13, Width = 50,
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = (SolidColorBrush)FindResource("DimBrush"),
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            });
            StarPanel.Children.Add(sp);
        }

        AddRow("★★★", $"{_level.Star3Moves}회 이하");
        AddRow("★★☆", $"{_level.Star2Moves}회 이하");
        AddRow("★☆☆", "클리어");
    }

    private void BuildLegend()
    {
        LegendPanel.Children.Clear();
        foreach (var (mat, def) in Materials.All)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2),
            };
            var swatch = new Border
            {
                Width = 14, Height = 14,
                Background = (SolidColorBrush)new BrushConverter().ConvertFrom(def.Fill)!,
                BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom(def.Stroke)!,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = $"{def.Name} (HP:{def.MaxHp})",
                Foreground = (SolidColorBrush)FindResource("DimBrush"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            sp.Children.Add(swatch);
            sp.Children.Add(label);
            LegendPanel.Children.Add(sp);
        }
    }
}
