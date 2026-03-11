namespace MemLens;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ── 상태 ──────────────────────────────────────────────────────────────
    private readonly ObservableCollection<ProcessInfo> _allProcesses  = [];
    private readonly ObservableCollection<ProcessInfo> _filteredProcs = [];

    private readonly Dictionary<int, List<MemorySnapshot>> _timelines = [];
    private readonly MemoryLeakDetector _leakDetector = new();

    private readonly DispatcherTimer _refreshTimer = new();
    private bool _initialized;
    private ProcessInfo? _selectedInfo;

    private const int TimelineMaxSamples = 120; // 30분 × 15초 간격

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        LstProcesses.ItemsSource = _filteredProcs;

        _refreshTimer.Tick += (_, _) => RefreshProcesses();
        SetRefreshInterval(3);

        Loaded += (_, _) =>
        {
            _initialized = true;
            RefreshProcesses();
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F5) { RefreshProcesses(); e.Handled = true; }
        };
    }

    // ── 새로 고침 ─────────────────────────────────────────────────────────

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshProcesses();

    private void RefreshProcesses()
    {
        var selected = _selectedInfo;

        Task.Run(() =>
        {
            var procs = ProcessMemoryService.GetAll();

            Dispatcher.InvokeAsync(() =>
            {
                // 트렌드 계산
                foreach (var p in procs)
                    p.Trend = _leakDetector.Record(p.Pid, p.PrivateBytes);

                _allProcesses.Clear();
                foreach (var p in procs) _allProcesses.Add(p);

                ApplySearch();

                // 타임라인 업데이트
                var now = DateTime.Now;
                foreach (var p in procs)
                {
                    if (!_timelines.TryGetValue(p.Pid, out var hist))
                    {
                        hist = [];
                        _timelines[p.Pid] = hist;
                    }
                    hist.Add(new MemorySnapshot { Time = now, PrivateBytes = p.PrivateBytes, WorkingSet = p.WorkingSet });
                    while (hist.Count > TimelineMaxSamples) hist.RemoveAt(0);
                }

                // 선택 프로세스 복원 & 업데이트
                if (selected != null)
                {
                    var refreshed = _filteredProcs.FirstOrDefault(p => p.Pid == selected.Pid);
                    if (refreshed != null)
                    {
                        LstProcesses.SelectedItem = refreshed;
                        UpdateDetailPanel(refreshed);
                    }
                }

                TxtRefreshTime.Text = $"마지막 갱신: {now:HH:mm:ss}";
                TxtStatus.Text = $"총 {procs.Count}개 프로세스";
            });
        });
    }

    private void ApplySearch()
    {
        var q = TxtSearch.Text.Trim().ToLowerInvariant();
        _filteredProcs.Clear();
        foreach (var p in _allProcesses)
        {
            if (string.IsNullOrEmpty(q) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(q))
                _filteredProcs.Add(p);
        }
    }

    private void TxtSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        ApplySearch();
    }

    private void CbRefreshInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (CbRefreshInterval.SelectedItem is ComboBoxItem item &&
            item.Tag is string s && int.TryParse(s, out var sec))
            SetRefreshInterval(sec);
    }

    private void SetRefreshInterval(int seconds)
    {
        _refreshTimer.Stop();
        if (seconds > 0)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
            _refreshTimer.Start();
        }
    }

    // ── 프로세스 선택 ─────────────────────────────────────────────────────

    private void LstProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstProcesses.SelectedItem is not ProcessInfo info) return;
        _selectedInfo = info;
        UpdateDetailPanel(info);
    }

    private void UpdateDetailPanel(ProcessInfo info)
    {
        TxtDetailName.Text = info.Name;
        TxtDetailPid.Text  = $"PID: {info.Pid}  |  .NET: {(info.IsDotNet ? "예" : "아니오")}";

        TxtPrivate.Text    = info.PrivateDisplay;
        TxtWS.Text         = info.WorkingSetDisplay;
        TxtVirtual.Text    = FormatBytes(info.VirtualBytes);
        TxtPaged.Text      = FormatBytes(info.PagedPool);
        TxtNonPaged.Text   = FormatBytes(info.NonPagedPool);
        TxtPageFaults.Text = $"{info.PageFaults:N0}";
        TxtTrend.Text      = info.Trend switch
        {
            MemoryTrend.Rising  => "↑ 증가 중",
            MemoryTrend.Falling => "↓ 감소 중",
            _                   => "─ 안정",
        };

        // .NET GC 힙
        BdrGcHeap.Visibility = info.IsDotNet ? Visibility.Visible : Visibility.Collapsed;
        if (info.IsDotNet)
        {
            TxtGen0.Text    = FormatBytes(info.GcGen0Size);
            TxtGen1.Text    = FormatBytes(info.GcGen1Size);
            TxtGen2.Text    = FormatBytes(info.GcGen2Size);
            TxtLoh.Text     = FormatBytes(info.GcLohSize);
            TxtGcTotal.Text = FormatBytes(info.GcTotalHeap);
            UpdateGcBar(info);
        }

        // 타임라인
        DrawTimeline(info.Pid);
    }

    private void UpdateGcBar(ProcessInfo info)
    {
        var total = (double)info.GcTotalHeap;
        if (total <= 0) return;

        // GridSplitter 방식 대신 Width 비율 직접 설정
        ColGen0.Width = new GridLength(info.GcGen0Size / total, GridUnitType.Star);
        ColGen1.Width = new GridLength(info.GcGen1Size / total, GridUnitType.Star);
        ColGen2.Width = new GridLength(info.GcGen2Size / total, GridUnitType.Star);
        ColLoh.Width  = new GridLength(info.GcLohSize  / total, GridUnitType.Star);
    }

    // ── 타임라인 그래프 ───────────────────────────────────────────────────

    private void DrawTimeline(int pid)
    {
        TimelineCanvas.Children.Clear();

        if (!_timelines.TryGetValue(pid, out var hist) || hist.Count < 2) return;

        double w = TimelineCanvas.ActualWidth;
        double h = TimelineCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            TimelineCanvas.SizeChanged += OnTimelineSizeChanged;
            return;
        }

        var maxPv = hist.Max(s => s.PrivateBytes);
        var maxWs = hist.Max(s => s.WorkingSet);
        double maxVal = Math.Max(maxPv, maxWs);
        if (maxVal <= 0) return;

        double xStep = w / (TimelineMaxSamples - 1.0);

        DrawLine(TimelineCanvas, hist, s => s.PrivateBytes, maxVal, xStep, h, Color.FromRgb(0x6E, 0xFF, 0x6E));
        DrawLine(TimelineCanvas, hist, s => s.WorkingSet,   maxVal, xStep, h, Color.FromRgb(0x6E, 0xA8, 0xFE));
    }

    private static void DrawLine(
        Canvas canvas, List<MemorySnapshot> hist,
        Func<MemorySnapshot, long> getValue,
        double maxVal, double xStep, double h, Color color)
    {
        var pts = new System.Windows.Media.PointCollection();
        int offsetIdx = TimelineMaxSamples - hist.Count;

        for (int i = 0; i < hist.Count; i++)
        {
            double x = (offsetIdx + i) * xStep;
            double y = h - (getValue(hist[i]) / maxVal * (h - 4)) - 2;
            pts.Add(new System.Windows.Point(x, y));
        }

        var poly = new Polyline
        {
            Points          = pts,
            Stroke          = new SolidColorBrush(color),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        };
        canvas.Children.Add(poly);
    }

    private void OnTimelineSizeChanged(object sender, SizeChangedEventArgs e)
    {
        TimelineCanvas.SizeChanged -= OnTimelineSizeChanged;
        if (_selectedInfo != null) DrawTimeline(_selectedInfo.Pid);
    }

    // ── WorkingSet 트림 ───────────────────────────────────────────────────

    private void BtnTrimWs_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedInfo == null) return;
        var ok = ProcessMemoryService.TrimWorkingSet(_selectedInfo.Pid);
        TxtStatus.Text = ok
            ? $"{_selectedInfo.Name} ({_selectedInfo.Pid}): WorkingSet 정리 완료"
            : "WorkingSet 정리 실패 (권한 부족 또는 프로세스 없음)";
        RefreshProcesses();
    }

    // ── 리포트 내보내기 ───────────────────────────────────────────────────

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "리포트 저장",
            Filter     = "Markdown|*.md|텍스트|*.txt",
            FileName   = $"MemLens_{DateTime.Now:yyyyMMdd_HHmmss}.md",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Mem.Lens 스냅샷 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("| 순위 | 프로세스 | PID | Private | Working Set | 추세 |");
        sb.AppendLine("|------|----------|-----|---------|-------------|------|");
        int rank = 1;
        foreach (var p in _allProcesses.Take(50))
            sb.AppendLine($"| {rank++} | {p.Name} | {p.Pid} | {p.PrivateDisplay} | {p.WorkingSetDisplay} | {p.TrendIndicator} |");

        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
        TxtStatus.Text = $"리포트 저장: {dlg.FileName}";
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:F1} GB",
        >= 1024L * 1024        => $"{bytes / 1024.0 / 1024:F1} MB",
        >= 1024L               => $"{bytes / 1024.0:F1} KB",
        _                      => $"{bytes} B",
    };
}
