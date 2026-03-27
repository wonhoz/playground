using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LeafGrow;

public partial class MainWindow : Window
{
    // ── 서비스 ───────────────────────────────────────────────────────
    private readonly GrowthService _svc = new();

    // ── 퍼즐 모드 ────────────────────────────────────────────────────
    private bool        _puzzleMode = false;
    private int         _puzzleIdx  = 0;
    private PuzzleGoal? _currentGoal;

    // ── 비주얼 풀 ────────────────────────────────────────────────────
    private readonly List<Line>    _linePool   = [];
    private readonly List<Ellipse> _flowerPool = [];

    // ── 브러시 캐시 ──────────────────────────────────────────────────
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = [];

    // ── 자동 성장 타이머 ─────────────────────────────────────────────
    private DispatcherTimer? _autoTimer;

    // ── 생성자 ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        PlantList.ItemsSource = PlantLibrary.All
            .Select(p => $"{p.KorName}  ({p.Name})")
            .ToList();
        PlantList.SelectedIndex = 0;

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
            if (_puzzleMode) { CheckPuzzleGoal(); UpdatePuzzleHints(); }
        });
    }

    // ── HUD 업데이트 ──────────────────────────────────────────────────
    private void UpdateHud()
    {
        var s = _svc.State;
        TxtIteration.Text   = $"{s.Iteration} / {s.Species.MaxIter}";
        TxtSpeciesInfo.Text = $" — {s.Species.KorName}";
        TxtDesc.Text        = s.Species.Description;

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

        // 성장 완료 시 버튼 비활성화
        bool grown = s.IsFullyGrown;
        GrowOneBtn.IsEnabled  = !grown;
        GrowFullBtn.IsEnabled = !grown;
        StepBackBtn.IsEnabled = _svc.CanStepBack;

        // 자동 성장 중 완료 시 인터벌 업데이트
        if (_autoTimer?.IsEnabled == true) UpdateAutoInterval();
    }

    // ── 식물 렌더링 ───────────────────────────────────────────────────
    private void RenderPlant()
    {
        if (PlantCanvas.ActualWidth < 1) return;

        var segs          = _svc.State.Segments;
        int neededLines   = segs.Count(s => s.Type != SegType.Flower);
        int neededFlowers = segs.Count(s => s.Type == SegType.Flower);

        // 라인 풀 확장
        while (_linePool.Count < neededLines)
        {
            var line = new Line { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            PlantCanvas.Children.Add(line);
            _linePool.Add(line);
        }

        // 꽃 풀 확장
        while (_flowerPool.Count < neededFlowers)
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
                el.Width   = seg.Thickness * 2;
                el.Height  = seg.Thickness * 2;
                el.Fill    = GetBrush(seg.Color);
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

        // 풀 축소: 사용량 + 여유분 초과 시 제거로 메모리 확보
        int trimLine = neededLines + 30;
        while (_linePool.Count > trimLine)
        {
            PlantCanvas.Children.Remove(_linePool[^1]);
            _linePool.RemoveAt(_linePool.Count - 1);
        }
        int trimFlower = neededFlowers + 20;
        while (_flowerPool.Count > trimFlower)
        {
            PlantCanvas.Children.Remove(_flowerPool[^1]);
            _flowerPool.RemoveAt(_flowerPool.Count - 1);
        }
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

        bool iterOk   = s.Iteration >= _currentGoal.TargetIter;
        bool sunOk    = s.Sun   >= _currentGoal.MinSun   && s.Sun   <= _currentGoal.MaxSun;
        bool waterOk  = s.Water >= _currentGoal.MinWater && s.Water <= _currentGoal.MaxWater;
        bool flowerOk = !_currentGoal.NeedFlower || s.Segments.Any(seg => seg.Type == SegType.Flower);

        if (iterOk && sunOk && waterOk && flowerOk)
        {
            TxtSuccessMsg.Text = $"\"{_currentGoal.Title}\" 목표 달성!";
            PuzzleSuccessPanel.Visibility = Visibility.Visible;
            StopAutoGrow();
        }
    }

    // ── 퍼즐 힌트 텍스트 업데이트 ────────────────────────────────────
    private void UpdatePuzzleHints()
    {
        if (_currentGoal == null || !_puzzleMode) return;
        var s = _svc.State;

        bool sunOk   = s.Sun   >= _currentGoal.MinSun   && s.Sun   <= _currentGoal.MaxSun;
        bool waterOk = s.Water >= _currentGoal.MinWater && s.Water <= _currentGoal.MaxWater;
        bool iterOk  = s.Iteration >= _currentGoal.TargetIter;

        var okBrush  = (Brush)FindResource("AccentBrush");
        var badBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x60, 0x40));

        PuzzleSunHint.Text       = $"목표: {_currentGoal.MinSun   * 100:F0}%~{_currentGoal.MaxSun   * 100:F0}% {(sunOk   ? "✅" : "❌")}";
        PuzzleSunHint.Foreground = sunOk   ? okBrush : badBrush;

        PuzzleWaterHint.Text       = $"목표: {_currentGoal.MinWater * 100:F0}%~{_currentGoal.MaxWater * 100:F0}% {(waterOk ? "✅" : "❌")}";
        PuzzleWaterHint.Foreground = waterOk ? okBrush : badBrush;

        // 영양분 슬라이더엔 단계 조건 표시
        PuzzleNutHint.Text       = $"목표 단계: {_currentGoal.TargetIter}단계 이상 {(iterOk ? "✅" : "❌")}";
        PuzzleNutHint.Foreground = iterOk ? okBrush : badBrush;

        // 범위 바 색상 (조건 충족: 녹색 / 미충족: 주황)
        PuzzleSunRangeBar.Background  = sunOk
            ? new SolidColorBrush(Color.FromArgb(0x90, 0x2E, 0xE0, 0x60))
            : new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x60, 0x40));
        PuzzleWaterRangeBar.Background = waterOk
            ? new SolidColorBrush(Color.FromArgb(0x90, 0x2E, 0xE0, 0x60))
            : new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x60, 0x40));
    }

    // ── 퍼즐 범위 막대 그리드 설정 ────────────────────────────────────
    private void SetPuzzleRangeGrids(PuzzleGoal goal)
    {
        PuzzleSunColLeft.Width  = new GridLength(goal.MinSun, GridUnitType.Star);
        PuzzleSunColRange.Width = new GridLength(Math.Max(goal.MaxSun - goal.MinSun, 0.01), GridUnitType.Star);
        PuzzleSunColRight.Width = new GridLength(Math.Max(1.0 - goal.MaxSun, 0), GridUnitType.Star);

        PuzzleWaterColLeft.Width  = new GridLength(goal.MinWater, GridUnitType.Star);
        PuzzleWaterColRange.Width = new GridLength(Math.Max(goal.MaxWater - goal.MinWater, 0.01), GridUnitType.Star);
        PuzzleWaterColRight.Width = new GridLength(Math.Max(1.0 - goal.MaxWater, 0), GridUnitType.Star);

        // 영양분은 조건 없으므로 전 범위를 반투명 초록으로
        PuzzleNutColLeft.Width  = new GridLength(0, GridUnitType.Star);
        PuzzleNutColRange.Width = new GridLength(1, GridUnitType.Star);
        PuzzleNutColRight.Width = new GridLength(0, GridUnitType.Star);
        PuzzleNutRangeBar.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x2E, 0xE0, 0x60));
    }

    // ── 퍼즐 힌트 UI 표시/숨김 ───────────────────────────────────────
    private void SetPuzzleHintsVisible(bool visible)
    {
        var vis = visible ? Visibility.Visible : Visibility.Collapsed;
        PuzzleSunRangeGrid.Visibility   = vis;
        PuzzleSunHint.Visibility        = vis;
        PuzzleWaterRangeGrid.Visibility = vis;
        PuzzleWaterHint.Visibility      = vis;
        PuzzleNutRangeGrid.Visibility   = vis;
        PuzzleNutHint.Visibility        = vis;
    }

    // ── UI 이벤트: 모드 전환 ─────────────────────────────────────────
    private void BtnSandbox_Click(object sender, RoutedEventArgs e)
    {
        _puzzleMode = false;
        PanelSandbox.Visibility       = Visibility.Visible;
        PanelPuzzle.Visibility        = Visibility.Collapsed;
        PuzzleGoalBadge.Visibility    = Visibility.Collapsed;
        PuzzleSuccessPanel.Visibility = Visibility.Collapsed;
        SetPuzzleHintsVisible(false);

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
        PanelSandbox.Visibility    = Visibility.Collapsed;
        PanelPuzzle.Visibility     = Visibility.Visible;
        PuzzleGoalBadge.Visibility = Visibility.Visible;
        SetPuzzleHintsVisible(true);

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

        // 퍼즐 시작 시 슬라이더 중간값으로 리셋
        SliderSun.Value       = 0.5;
        SliderWater.Value     = 0.5;
        SliderNutrients.Value = 0.5;

        var species = PlantLibrary.All.FirstOrDefault(p => p.Name == _currentGoal.SpeciesName)
                      ?? PlantLibrary.All[0];
        _svc.SelectSpecies(species);

        TxtGoalHint.Text = $"🎯 {_currentGoal.Title}\n{_currentGoal.Description}";
        SetPuzzleRangeGrids(_currentGoal);
        UpdatePuzzleHints();
    }

    private void NextPuzzleBtn_Click(object sender, RoutedEventArgs e)
        => LoadPuzzle((_puzzleIdx + 1) % PuzzleLibrary.All.Count);

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
        if (_puzzleMode) UpdatePuzzleHints();
    }

    // ── UI 이벤트: 성장 버튼 ─────────────────────────────────────────
    private void GrowOneBtn_Click(object sender, RoutedEventArgs e)  => _svc.GrowOne();
    private void GrowFullBtn_Click(object sender, RoutedEventArgs e) => _svc.GrowFull();
    private void StepBackBtn_Click(object sender, RoutedEventArgs e) => _svc.StepBack();

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        PuzzleSuccessPanel.Visibility = Visibility.Collapsed;
        StopAutoGrow();
        _svc.Reset();
    }

    // ── 자동 성장 ─────────────────────────────────────────────────────
    private void AutoGrowBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_autoTimer?.IsEnabled == true) StopAutoGrow();
        else                               StartAutoGrow();
    }

    private void StartAutoGrow()
    {
        _autoTimer       = new DispatcherTimer();
        _autoTimer.Tick += (_, _) =>
        {
            if (_svc.State.IsFullyGrown) StopAutoGrow();
            else                         _svc.GrowOne();
        };
        UpdateAutoInterval();
        _autoTimer.Start();

        AutoGrowBtn.Content    = "⏸ 자동 성장 중";
        AutoGrowBtn.Background = (Brush)FindResource("AccentBrush");
        AutoGrowBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0x0F, 0x06));
        AutoGrowBtn.FontWeight = FontWeights.SemiBold;
    }

    private void StopAutoGrow()
    {
        _autoTimer?.Stop();
        if (!IsLoaded) return;
        AutoGrowBtn.Content    = "▶ 자동 성장";
        AutoGrowBtn.Background = (Brush)FindResource("PanelBrush");
        AutoGrowBtn.Foreground = (Brush)FindResource("FgBrush");
        AutoGrowBtn.FontWeight = FontWeights.Normal;
    }

    private void UpdateAutoInterval()
    {
        if (_autoTimer == null) return;
        double rate     = _svc.State.GrowthRate;
        double seconds  = 3.0 - rate * 2.5; // 고성장률 → 0.5s, 저성장률 → 3.0s
        _autoTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.5, seconds));
    }

    // ── 환경 프리셋 ───────────────────────────────────────────────────
    private void PresetSpring_Click(object sender, RoutedEventArgs e)  => ApplyPreset(0.6, 0.7, 0.6);
    private void PresetSummer_Click(object sender, RoutedEventArgs e)  => ApplyPreset(0.9, 0.4, 0.7);
    private void PresetAutumn_Click(object sender, RoutedEventArgs e)  => ApplyPreset(0.3, 0.3, 0.4);
    private void PresetWinter_Click(object sender, RoutedEventArgs e)  => ApplyPreset(0.1, 0.1, 0.2);

    private void ApplyPreset(double sun, double water, double nut)
    {
        SliderSun.Value       = sun;
        SliderWater.Value     = water;
        SliderNutrients.Value = nut;
    }

    // ── 이미지 저장 ───────────────────────────────────────────────────
    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (PlantCanvas.ActualWidth < 1) return;

        var dlg = new SaveFileDialog
        {
            Filter   = "PNG 이미지 (*.png)|*.png",
            FileName = $"{_svc.State.Species.KorName}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
        };
        if (dlg.ShowDialog() != true) return;

        var rtb = new RenderTargetBitmap(
            (int)PlantCanvas.ActualWidth,
            (int)PlantCanvas.ActualHeight,
            96, 96, PixelFormats.Pbgra32);
        rtb.Render(PlantCanvas);

        using var stream = File.Create(dlg.FileName);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        encoder.Save(stream);
    }

    // ── 도움말 ────────────────────────────────────────────────────────
    private void HelpBtn_Click(object sender, RoutedEventArgs e)
        => HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;

    private void HelpCloseBtn_Click(object sender, RoutedEventArgs e)
        => HelpPanel.Visibility = Visibility.Collapsed;

    // 배경 클릭 → 닫기
    private void HelpPanel_MouseDown(object sender, MouseButtonEventArgs e)
        => HelpPanel.Visibility = Visibility.Collapsed;

    // 내부 패널 클릭은 이벤트 버블링 차단 (배경 닫힘 방지)
    private void HelpInner_MouseDown(object sender, MouseButtonEventArgs e)
        => e.Handled = true;

    // ── 단축키 ────────────────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 도움말 열린 상태: Esc / F1만 처리
        if (HelpPanel.Visibility == Visibility.Visible)
        {
            if (e.Key is Key.Escape or Key.F1)
                HelpPanel.Visibility = Visibility.Collapsed;
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                if (!_svc.State.IsFullyGrown) _svc.GrowOne();
                e.Handled = true;
                break;
            case Key.F:
                _svc.GrowFull();
                break;
            case Key.Z:
                if (_svc.CanStepBack) _svc.StepBack();
                break;
            case Key.R:
                ResetBtn_Click(this, new RoutedEventArgs());
                break;
            case Key.A:
                AutoGrowBtn_Click(this, new RoutedEventArgs());
                break;
            case Key.S:
                SaveBtn_Click(this, new RoutedEventArgs());
                break;
            case Key.F1:
                HelpBtn_Click(this, new RoutedEventArgs());
                break;
        }
    }

    // ── 캔버스 크기 변경 → 재빌드 ────────────────────────────────────
    private void PlantCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        => _svc.RebuildAt(PlantCanvas.ActualWidth, PlantCanvas.ActualHeight);

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
