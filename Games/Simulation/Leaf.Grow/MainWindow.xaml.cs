using System.Windows.Input;

namespace LeafGrow;

public partial class MainWindow : Window
{
    // ── 서비스 ───────────────────────────────────────────────────────
    private readonly GrowthService _svc = new();

    // ── 퍼즐 모드 ────────────────────────────────────────────────────
    private bool        _puzzleMode   = false;
    private int         _puzzleIdx    = 0;
    private PuzzleGoal? _currentGoal;

    // ── 비주얼 풀 ────────────────────────────────────────────────────
    private readonly List<Line>    _linePool    = [];
    private readonly List<Ellipse> _flowerPool  = [];

    // ── 브러시 캐시 ──────────────────────────────────────────────────
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = [];

    // ── 생성자 ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // 식물 목록 바인딩
        PlantList.ItemsSource = PlantLibrary.All
            .Select(p => $"{p.KorName}  ({p.Name})")
            .ToList();
        PlantList.SelectedIndex = 0;

        // 퍼즐 목록 바인딩
        PuzzleList.ItemsSource = PuzzleLibrary.All
            .Select(g => g.Title)
            .ToList();
        PuzzleList.SelectedIndex = 0;

        _svc.GrowthUpdated += OnGrowthUpdated;
        Loaded             += (_, _) => { UpdateHud(); RenderPlant(); };
    }

    // ── 성장 이벤트 ──────────────────────────────────────────────────
    private void OnGrowthUpdated()
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateHud();
            RenderPlant();
            if (_puzzleMode) CheckPuzzleGoal();
        });
    }

    // ── HUD 업데이트 ──────────────────────────────────────────────────
    private void UpdateHud()
    {
        var s = _svc.State;
        TxtIteration.Text = $"{s.Iteration} / {s.Species.MaxIter}";
        TxtSpeciesInfo.Text = $" — {s.Species.KorName}";
        TxtDesc.Text = s.Species.Description;

        double rate = s.GrowthRate;
        TxtGrowthRate.Text = $"{rate * 100:F0}%";

        if (GrowthRateBar.Parent is Border parentBorder)
            GrowthRateBar.Width = rate * (parentBorder.ActualWidth - 2);

        TxtSun.Text       = $"{s.Sun * 100:F0}%";
        TxtWater.Text     = $"{s.Water * 100:F0}%";
        TxtNutrients.Text = $"{s.Nutrients * 100:F0}%";

        var branches = s.Segments.Count(seg => seg.Type == SegType.Branch);
        var leaves   = s.Segments.Count(seg => seg.Type == SegType.Leaf);
        var flowers  = s.Segments.Count(seg => seg.Type == SegType.Flower);
        TxtBranches.Text = branches.ToString();
        TxtLeaves.Text   = leaves.ToString();
        TxtFlowers.Text  = flowers.ToString();
    }

    // ── 식물 렌더링 ───────────────────────────────────────────────────
    private void RenderPlant()
    {
        if (PlantCanvas.ActualWidth < 1) return;

        var segs = _svc.State.Segments;

        // 라인 풀 확장
        while (_linePool.Count < segs.Count(s => s.Type != SegType.Flower))
        {
            var line = new Line { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            PlantCanvas.Children.Add(line);
            _linePool.Add(line);
        }

        // 꽃 풀 확장
        int flowerCount = segs.Count(s => s.Type == SegType.Flower);
        while (_flowerPool.Count < flowerCount)
        {
            var el = new Ellipse();
            PlantCanvas.Children.Add(el);
            _flowerPool.Add(el);
        }

        int li = 0, fi = 0;
        foreach (var seg in segs)
        {
            if (seg.Type == SegType.Flower)
            {
                var el = _flowerPool[fi++];
                el.Width  = seg.Thickness * 2;
                el.Height = seg.Thickness * 2;
                el.Fill   = GetBrush(seg.Color);
                el.Opacity = 0.9;
                Canvas.SetLeft(el, seg.X1 - seg.Thickness);
                Canvas.SetTop(el,  seg.Y1 - seg.Thickness);
                el.Visibility = Visibility.Visible;
            }
            else
            {
                var line = _linePool[li++];
                line.X1              = seg.X1;
                line.Y1              = seg.Y1;
                line.X2              = seg.X2;
                line.Y2              = seg.Y2;
                line.Stroke          = GetBrush(seg.Color);
                line.StrokeThickness = seg.Thickness;
                line.Visibility      = Visibility.Visible;
            }
        }

        // 남은 요소 숨기기
        for (int i = li; i < _linePool.Count; i++)   _linePool[i].Visibility   = Visibility.Collapsed;
        for (int i = fi; i < _flowerPool.Count; i++) _flowerPool[i].Visibility = Visibility.Collapsed;
    }

    private SolidColorBrush GetBrush(Color c)
    {
        if (!_brushCache.TryGetValue(c, out var br))
            _brushCache[c] = br = new SolidColorBrush(c);
        return br;
    }

    // ── 퍼즐 확인 ─────────────────────────────────────────────────────
    private void CheckPuzzleGoal()
    {
        if (_currentGoal == null) return;
        var s = _svc.State;

        bool iterOk    = s.Iteration >= _currentGoal.TargetIter;
        bool sunOk     = s.Sun    >= _currentGoal.MinSun    && s.Sun    <= _currentGoal.MaxSun;
        bool waterOk   = s.Water  >= _currentGoal.MinWater  && s.Water  <= _currentGoal.MaxWater;
        bool flowerOk  = !_currentGoal.NeedFlower ||
                         s.Segments.Any(seg => seg.Type == SegType.Flower);

        if (iterOk && sunOk && waterOk && flowerOk)
        {
            TxtSuccessMsg.Text = $"\"{_currentGoal.Title}\" 목표 달성!";
            PuzzleSuccessPanel.Visibility = Visibility.Visible;
        }
    }

    // ── UI 이벤트: 모드 전환 ─────────────────────────────────────────
    private void BtnSandbox_Click(object sender, RoutedEventArgs e)
    {
        _puzzleMode = false;
        PanelSandbox.Visibility     = Visibility.Visible;
        PanelPuzzle.Visibility      = Visibility.Collapsed;
        PuzzleGoalBadge.Visibility  = Visibility.Collapsed;
        PuzzleSuccessPanel.Visibility = Visibility.Collapsed;
        BtnSandbox.Background = (Brush)FindResource("AccentBrush");
        BtnSandbox.Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0x0F, 0x06));
        BtnSandbox.FontWeight = FontWeights.SemiBold;
        BtnPuzzle.Background  = (Brush)FindResource("PanelBrush");
        BtnPuzzle.Foreground  = (Brush)FindResource("FgBrush");
        BtnPuzzle.FontWeight  = FontWeights.Normal;
    }

    private void BtnPuzzle_Click(object sender, RoutedEventArgs e)
    {
        _puzzleMode = true;
        PanelSandbox.Visibility     = Visibility.Collapsed;
        PanelPuzzle.Visibility      = Visibility.Visible;
        PuzzleGoalBadge.Visibility  = Visibility.Visible;
        BtnPuzzle.Background  = (Brush)FindResource("AccentBrush");
        BtnPuzzle.Foreground  = new SolidColorBrush(Color.FromRgb(0x06, 0x0F, 0x06));
        BtnPuzzle.FontWeight  = FontWeights.SemiBold;
        BtnSandbox.Background = (Brush)FindResource("PanelBrush");
        BtnSandbox.Foreground = (Brush)FindResource("FgBrush");
        BtnSandbox.FontWeight = FontWeights.Normal;
        LoadPuzzle(_puzzleIdx);
    }

    private void LoadPuzzle(int idx)
    {
        if (idx >= PuzzleLibrary.All.Count) idx = 0;
        _puzzleIdx   = idx;
        _currentGoal = PuzzleLibrary.All[idx];

        PuzzleSuccessPanel.Visibility = Visibility.Collapsed;

        var species = PlantLibrary.All.FirstOrDefault(p => p.Name == _currentGoal.SpeciesName)
                      ?? PlantLibrary.All[0];
        _svc.SelectSpecies(species);

        TxtGoalHint.Text = $"🎯 {_currentGoal.Title}\n{_currentGoal.Description}";
    }

    private void NextPuzzleBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadPuzzle((_puzzleIdx + 1) % PuzzleLibrary.All.Count);
    }

    // ── UI 이벤트: 식물/퍼즐 선택 ────────────────────────────────────
    private void PlantList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        int idx = PlantList.SelectedIndex;
        if (idx >= 0 && idx < PlantLibrary.All.Count)
            _svc.SelectSpecies(PlantLibrary.All[idx]);
    }

    private void PuzzleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        int idx = PuzzleList.SelectedIndex;
        if (idx >= 0) LoadPuzzle(idx);
    }

    // ── UI 이벤트: 환경 슬라이더 ─────────────────────────────────────
    private void EnvSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _svc.SetEnvironment(SliderSun.Value, SliderWater.Value, SliderNutrients.Value);
    }

    // ── UI 이벤트: 성장 버튼 ─────────────────────────────────────────
    private void GrowOneBtn_Click(object sender, RoutedEventArgs e)  => _svc.GrowOne();
    private void GrowFullBtn_Click(object sender, RoutedEventArgs e) => _svc.GrowFull();
    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        PuzzleSuccessPanel.Visibility = Visibility.Collapsed;
        _svc.Reset();
    }

    // ── 캔버스 크기 변경 → 재빌드 ────────────────────────────────────
    private void PlantCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _svc.RebuildAt(PlantCanvas.ActualWidth, PlantCanvas.ActualHeight);
    }

    // ── 타이틀바 / 윈도우 제어 ───────────────────────────────────────
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();
}
