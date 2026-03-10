namespace NetScan.Views;

public partial class ScanView : UserControl
{
    // ── 서비스 ───────────────────────────────────────────────────────
    private readonly ScanService                     _scanSvc  = new();
    private readonly ObservableCollection<NetworkDevice> _devices = [];
    private PingMonitor?                             _monitor;
    private NetworkDevice?                           _selected;

    // ── 생성자 ───────────────────────────────────────────────────────
    public ScanView()
    {
        InitializeComponent();
        LvDevices.ItemsSource = _devices;
        LoadAdapters();
        WireEvents();
        Loaded += (_, _) =>
        {
            _monitor = new PingMonitor(_devices);
            // IsLoaded 가드로 건너뛴 초기 어댑터 선택 상태 적용
            if (CbAdapter.SelectedItem is NetworkService.AdapterInfo info)
            {
                TxtSubnet.Text       = info.Subnet;
                TxtRange.Text        = info.Subnet;
                TxtSubnet.Visibility = Visibility.Visible;
                TxtRange.Visibility  = Visibility.Collapsed;
                UpdateSummary();
            }
        };
    }

    private void LoadAdapters()
    {
        var adapters = NetworkService.GetActiveAdapters();
        CbAdapter.ItemsSource   = adapters;
        CbAdapter.DisplayMemberPath = "Name";
        if (adapters.Count > 0)
            CbAdapter.SelectedIndex = 0;
    }

    private void WireEvents()
    {
        _scanSvc.DeviceFound      += OnDeviceFound;
        _scanSvc.ProgressChanged  += OnProgressChanged;
        _scanSvc.ScanCompleted    += OnScanCompleted;
    }

    // ── 어댑터 선택 ──────────────────────────────────────────────────
    private void CbAdapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CbAdapter.SelectedItem is not NetworkService.AdapterInfo info) return;
        TxtSubnet.Text     = info.Subnet;
        TxtRange.Text      = info.Subnet;
        TxtSubnet.Visibility = Visibility.Visible;
        TxtRange.Visibility  = Visibility.Collapsed;
        UpdateSummary();
    }

    // ── 스캔 버튼 ────────────────────────────────────────────────────
    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        var cidr = GetCurrentCidr();
        if (string.IsNullOrEmpty(cidr)) return;

        _devices.Clear();
        _monitor?.Stop();

        BtnScan.IsEnabled = false;
        BtnStop.IsEnabled = true;
        PbScan.Value      = 0;
        MainWin?.SetStatus($"스캔 중: {cidr}");
        MainWin?.ShowProgress(true, false, 0, 100);

        await _scanSvc.StartScanAsync(cidr);
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e) => _scanSvc.StopScan();

    private string GetCurrentCidr()
    {
        if (TxtRange.Visibility == Visibility.Visible)
            return TxtRange.Text.Trim();
        return CbAdapter.SelectedItem is NetworkService.AdapterInfo info ? info.Subnet : "";
    }

    // ── 스캔 이벤트 핸들러 ──────────────────────────────────────────
    private void OnDeviceFound(NetworkDevice dev)
    {
        Dispatcher.Invoke(() =>
        {
            _devices.Add(dev);
            UpdateSummary();
        });
    }

    private void OnProgressChanged(int done, int total)
    {
        Dispatcher.Invoke(() =>
        {
            double pct = total > 0 ? done * 100.0 / total : 0;
            PbScan.Value = pct;
            MainWin?.ShowProgress(true, false, pct, 100);
        });
    }

    private void OnScanCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            BtnScan.IsEnabled = true;
            BtnStop.IsEnabled = false;
            PbScan.Value      = 100;
            int cnt = _devices.Count;
            MainWin?.SetStatus($"스캔 완료 — {cnt}개 기기 발견");
            MainWin?.ShowProgress(false);

            _monitor?.Start();
            UpdateSummary();
        });
    }

    // ── 기기 선택 ────────────────────────────────────────────────────
    private void LvDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = LvDevices.SelectedItem as NetworkDevice;
        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if (_selected is null)
        {
            TxtNoSelection.Visibility    = Visibility.Visible;
            PanelDeviceDetail.Visibility = Visibility.Collapsed;
            return;
        }

        TxtNoSelection.Visibility    = Visibility.Collapsed;
        PanelDeviceDetail.Visibility = Visibility.Visible;

        var d = _selected;
        TxtDetailIcon.Text     = d.DeviceType;
        TxtDetailName.Text     = d.DisplayName;
        TxtDetailStatus.Text   = d.IsOnline ? "● 온라인" : "● 오프라인";
        TxtDetailStatus.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(d.StatusColor));
        TxtDetailPing.Text     = d.PingText;
        TxtDetailIp.Text       = d.IpAddress;
        TxtDetailMac.Text      = d.MacDisplay;
        TxtDetailVendor.Text   = d.Vendor;
        TxtDetailLastSeen.Text = d.LastSeen == default ? "—"
                               : d.LastSeen.ToString("HH:mm:ss");
        TxtAlias.Text          = d.Alias;

        // 포트 목록 갱신
        RefreshPortDisplay();

        // 핑 그래프 갱신
        DrawPingGraph();
    }

    // ── 별칭 저장 ────────────────────────────────────────────────────
    private void BtnSaveAlias_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _selected.Alias = TxtAlias.Text.Trim();
        RefreshDetailPanel();
    }

    // ── 포트 스캔 ────────────────────────────────────────────────────
    private CancellationTokenSource? _portCts;

    private async void BtnPortScan_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        _portCts?.Cancel();
        _portCts = new CancellationTokenSource();
        BtnPortScan.IsEnabled   = false;
        TxtPortScanStatus.Text  = "스캔 중…";
        IcPorts.ItemsSource     = null;

        try
        {
            var ip    = _selected.IpAddress;
            var ports = await PortScanService.ScanAsync(ip, ct: _portCts.Token);
            _selected.OpenPorts = ports;
            RefreshPortDisplay();
            TxtPortScanStatus.Text = ports.Count > 0
                ? $"{ports.Count}개 포트 열림"
                : "열린 포트 없음";
        }
        catch (OperationCanceledException) { TxtPortScanStatus.Text = "취소됨"; }
        finally { BtnPortScan.IsEnabled = true; }
    }

    private void RefreshPortDisplay()
    {
        if (_selected is null) return;
        var labels = _selected.OpenPorts
                              .Select(p => $"{p}/{PortScanService.GetPortName(p)}")
                              .ToList();
        IcPorts.ItemsSource = labels;
        if (_selected.OpenPorts.Count == 0 && TxtPortScanStatus.Text == "")
            TxtPortScanStatus.Text = "";
    }

    // ── 핑 그래프 ────────────────────────────────────────────────────
    private void DrawPingGraph()
    {
        CvPingGraph.Children.Clear();
        if (_selected is null) return;

        var history = _selected.PingHistory.ToArray();
        if (history.Length < 2) return;

        double w = CvPingGraph.ActualWidth;
        double h = CvPingGraph.ActualHeight;
        if (w < 10 || h < 10) return;

        // 유효한 샘플(>= 0)만 추출
        var valid = history.Where(v => v >= 0).ToArray();
        if (valid.Length == 0) return;

        long maxMs = Math.Max(valid.Max(), 1);
        long minMs = valid.Min();
        long avgMs = (long)valid.Average();

        // 그리드라인 (50% 위치)
        var grid = new Line
        {
            X1 = 0, X2 = w,
            Y1 = h * 0.5, Y2 = h * 0.5,
            Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            StrokeDashArray = [3, 3]
        };
        CvPingGraph.Children.Add(grid);

        // 오프라인(-1) 포함 전체 샘플을 좌→우 배치
        int  n      = history.Length;
        double step = w / Math.Max(n - 1, 1);
        var pts     = new PointCollection();

        for (int i = 0; i < n; i++)
        {
            double x  = i * step;
            double yy;
            if (history[i] < 0)
                yy = h; // 오프라인 → 바닥
            else
                yy = h - (history[i] / (double)maxMs) * (h - 4) - 2;
            pts.Add(new Point(x, yy));
        }

        // 채우기 영역
        var fill = new Polygon
        {
            Fill      = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
            Stroke    = Brushes.Transparent,
            StrokeThickness = 0
        };
        fill.Points.Add(new Point(0, h));
        foreach (var p in pts) fill.Points.Add(p);
        fill.Points.Add(new Point((n - 1) * step, h));
        CvPingGraph.Children.Add(fill);

        // 선
        var line = new Polyline
        {
            Stroke          = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
            StrokeThickness = 1.5,
            Points          = pts
        };
        CvPingGraph.Children.Add(line);

        // 통계 텍스트
        TxtGraphMin.Text = $"최소 {minMs}ms";
        TxtGraphAvg.Text = $"평균 {avgMs}ms";
        TxtGraphMax.Text = $"최대 {maxMs}ms";
    }

    // ── 요약 ────────────────────────────────────────────────────────
    private void UpdateSummary()
    {
        int total  = _devices.Count;
        int online = _devices.Count(d => d.IsOnline);
        TxtSummary.Text = total > 0 ? $"{online}/{total}개 온라인" : "";
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────
    private MainWindow? MainWin => Window.GetWindow(this) as MainWindow;
}
