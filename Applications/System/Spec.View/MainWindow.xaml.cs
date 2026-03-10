using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SpecView;

public partial class MainWindow : Window
{
    private readonly HardwareService  _hwSvc      = new();
    private readonly ExportService    _exportSvc  = new();
    private readonly SnapshotService  _snapSvc    = new();
    private readonly MonitorService   _monSvc     = new();
    private readonly DispatcherTimer  _monTimer   = new() { Interval = TimeSpan.FromSeconds(2) };

    private HardwareData? _data;
    private bool          _monRunning;

    public MainWindow()
    {
        InitializeComponent();
        Loaded  += OnLoaded;
        Closing += (_, _) => _monSvc.Dispose();

        _monTimer.Tick += (_, _) => RefreshMonitor();
        Tabs.SelectionChanged += Tabs_SelectionChanged;
    }

    // ── 초기화 ───────────────────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? name);

    // WindowStyle=None + AllowsTransparency=True 환경에서 WPF Icon이 HWND에 반영 안 됨
    // → WM_SETICON으로 직접 설정
    private void SetTaskbarIcon()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var hMod = GetModuleHandle(null);
        var big   = LoadImage(hMod, "#1", 1 /*IMAGE_ICON*/, 32, 32, 0);
        var small = LoadImage(hMod, "#1", 1,                 16, 16, 0);
        if (big   != IntPtr.Zero) SendMessage(hwnd, 0x0080 /*WM_SETICON*/, (IntPtr)1, big);
        if (small != IntPtr.Zero) SendMessage(hwnd, 0x0080,                (IntPtr)0, small);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetTaskbarIcon();
        _monSvc.Initialize();
        UpdateStatus("스캔 버튼을 눌러 하드웨어 정보를 수집합니다.");

        var lastTime = _snapSvc.LastScanTime();
        TxtLastScan.Text = lastTime.HasValue
            ? $"마지막 스캔: {lastTime.Value:yyyy-MM-dd HH:mm}"
            : "이전 스캔 없음";

        if (!_monSvc.IsAvailable)
            TxtMonitorStatus.Text = "⚠ 관리자 권한이 필요하거나 하드웨어 센서를 지원하지 않습니다.";
    }

    // ── 스캔 ─────────────────────────────────────────────────────────

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        ShowLoading(true, "WMI 데이터 수집 중...");
        UpdateStatus("스캔 중...");

        try
        {
            var previous = _snapSvc.Load();
            _data = await _hwSvc.ScanAsync();

            ApplyData(_data, previous);
            _snapSvc.Save(_data);

            TxtLastScan.Text = $"마지막 스캔: {_data.ScannedAt:yyyy-MM-dd HH:mm}";
            UpdateStatus($"스캔 완료 — {_data.ScannedAt:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"스캔 중 오류가 발생했습니다:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("스캔 실패");
        }
        finally
        {
            ShowLoading(false);
            ScanBtn.IsEnabled = true;
        }
    }

    // ── 데이터 바인딩 ────────────────────────────────────────────────

    private void ApplyData(HardwareData d, HardwareData? prev)
    {
        // 개요 탭
        TxtPcName.Text  = d.ComputerName;
        TxtOsName.Text  = $"{d.OsCaption}  ({d.OsVersion})";
        TxtScanTime.Text = $"스캔: {d.ScannedAt:yyyy-MM-dd HH:mm:ss}";

        // CPU 요약
        if (d.Cpu is { } cpu)
        {
            OvCpuName.Text  = cpu.Name;
            OvCpuSpec.Text  = $"{cpu.Cores} 코어 / {cpu.Threads} 스레드";
            OvCpuClock.Text = $"최대 {cpu.ClockDisplay} | {cpu.Socket} | {cpu.Architecture}";
        }

        // 메모리 요약
        OvMemTotal.Text  = d.Memory.TotalDisplay;
        OvMemType.Text   = d.Memory.Slots.FirstOrDefault(s => !s.IsEmpty)?.MemoryType ?? "";
        OvMemSlots.Text  = $"{d.Memory.SlotDisplay}"
                         + (d.Memory.MaxSpeedMHz > 0 ? $"  |  {d.Memory.MaxSpeedMHz} MHz" : "");

        // GPU 요약
        if (d.Gpus.Count > 0)
        {
            OvGpuName.Text   = d.Gpus[0].Name;
            OvGpuVram.Text   = $"VRAM {d.Gpus[0].VramDisplay}";
            OvGpuDriver.Text = $"드라이버 {d.Gpus[0].DriverVersion}  ({d.Gpus[0].DriverDate})";
        }

        // 마더보드 요약
        OvBoardName.Text = $"{d.Board.Manufacturer} {d.Board.Product}";
        OvBoardBios.Text = $"BIOS {d.Board.BiosVersion}";
        OvBoardDate.Text = d.Board.BiosDate;

        // 저장장치 요약
        OvDriveCount.Text = $"{d.Drives.Count} 개";
        var totalBytes = d.Drives.Sum(x => (decimal)x.SizeBytes);
        OvDriveTotal.Text = totalBytes >= 1_000_000_000_000m
            ? $"합계 {totalBytes / 1_000_000_000_000m:F1} TB"
            : $"합계 {totalBytes / 1_000_000_000m:F0} GB";
        var badDrives = d.Drives.Where(x => !x.SmartOk).ToList();
        if (badDrives.Count > 0)
        {
            OvDriveSmart.Text      = $"⚠ S.M.A.R.T 경고 {badDrives.Count}개";
            OvDriveSmart.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
        else
        {
            OvDriveSmart.Text      = "S.M.A.R.T 정상";
            OvDriveSmart.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        }

        // 변경 감지
        if (prev is not null)
        {
            var changes = _snapSvc.GetChanges(d, prev);
            if (changes.Count > 0)
            {
                OvChangeStatus.Text           = $"{changes.Count} 건 변경 감지";
                OvChangeStatus.Foreground      = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                OvChangeList.ItemsSource       = changes;
                TxtChangesSummary.Text         = $"⚠ {changes.Count}건 변경";
                TxtChangesSummary.Visibility   = Visibility.Visible;
                TxtChanges.Text                = $"⚠ {changes.Count}건 변경";
                TxtChanges.Visibility          = Visibility.Visible;
            }
            else
            {
                OvChangeStatus.Text     = "변경 없음";
                OvChangeStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                OvChangeList.ItemsSource  = null;
                TxtChangesSummary.Visibility = Visibility.Collapsed;
                TxtChanges.Visibility        = Visibility.Collapsed;
            }
        }
        else
        {
            OvChangeStatus.Text = "이전 스캔 없음";
            OvChangeStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        }

        // CPU 탭
        BuildCpuTab(d.Cpu);

        // 메모리 탭
        BuildMemoryTab(d.Memory);

        // GPU 탭
        BuildGpuTab(d.Gpus);

        // 마더보드 탭
        BuildBoardTab(d.Board);

        // 저장장치 탭
        ListDrives.ItemsSource = d.Drives;

        // 네트워크 탭
        ListNetworks.ItemsSource = d.Networks;
    }

    // ── 탭 빌더 ─────────────────────────────────────────────────────

    private void BuildCpuTab(CpuInfo? cpu)
    {
        GridCpu.Children.Clear();
        GridCpu.ColumnDefinitions.Clear();
        GridCpu.RowDefinitions.Clear();

        if (cpu is null)
        {
            GridCpu.Children.Add(new TextBlock { Text = "CPU 정보 없음", Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
            return;
        }

        var items = new List<(string Label, string Value)>
        {
            ("모델",        cpu.Name),
            ("제조사",      cpu.Manufacturer),
            ("소켓",        cpu.Socket),
            ("아키텍처",    cpu.Architecture),
            ("코어 / 스레드", $"{cpu.Cores}C / {cpu.Threads}T"),
            ("최대 클럭",   cpu.ClockDisplay),
            ("L2 캐시",     string.IsNullOrEmpty(cpu.L2Cache) ? "-" : cpu.L2Cache),
            ("L3 캐시",     string.IsNullOrEmpty(cpu.L3Cache) ? "-" : cpu.L3Cache),
            ("설명",        cpu.Description)
        };

        BuildInfoGrid(GridCpu, items);
    }

    private void BuildMemoryTab(MemoryInfo mem)
    {
        MemSummaryPanel.Children.Clear();

        var items = new List<(string, string)>
        {
            ("총 용량",    mem.TotalDisplay),
            ("슬롯",       mem.SlotDisplay),
            ("최대 속도",  mem.MaxSpeedMHz > 0 ? $"{mem.MaxSpeedMHz} MHz" : "-")
        };

        var grid = new Grid();
        BuildInfoGrid(grid, items);
        MemSummaryPanel.Children.Add(grid);

        ListMemSlots.ItemsSource = mem.Slots;
    }

    private void BuildGpuTab(List<GpuInfo> gpus)
    {
        // 기존 GPU 카드들 제거 (헤더 TextBlock 이후)
        var header = PanelGpu.Children[0];
        PanelGpu.Children.Clear();
        PanelGpu.Children.Add(header);

        if (gpus.Count == 0)
        {
            PanelGpu.Children.Add(new TextBlock
            {
                Text       = "GPU 정보 없음",
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                Margin     = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var gpu in gpus)
        {
            var cardBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(16),
                Margin          = new Thickness(0, 0, 0, 12)
            };

            var inner = new StackPanel();
            inner.Children.Add(new TextBlock
            {
                Text       = gpu.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0xE0)),
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, 0, 0, 12)
            });

            var grid = new Grid();
            var items = new List<(string, string)>
            {
                ("VRAM",        gpu.VramDisplay),
                ("드라이버",    gpu.DriverVersion),
                ("드라이버 날짜", gpu.DriverDate),
                ("현재 해상도", gpu.VideoModeDescription),
                ("주사율",      gpu.CurrentRefreshRate > 0 ? $"{gpu.CurrentRefreshRate} Hz" : "-"),
                ("제조사",      gpu.AdapterCompatibility)
            };
            BuildInfoGrid(grid, items);
            inner.Children.Add(grid);
            cardBorder.Child = inner;
            PanelGpu.Children.Add(cardBorder);
        }
    }

    private void BuildBoardTab(MotherboardInfo board)
    {
        GridBoard.Children.Clear();
        GridBoard.ColumnDefinitions.Clear();
        GridBoard.RowDefinitions.Clear();

        var items = new List<(string, string)>
        {
            ("제조사",          board.Manufacturer),
            ("제품명",          board.Product),
            ("시리얼 번호",     string.IsNullOrEmpty(board.SerialNumber) ? "-" : board.SerialNumber),
            ("BIOS 제조사",     board.BiosMaker),
            ("BIOS 버전",       board.BiosVersion),
            ("BIOS 날짜",       board.BiosDate)
        };

        BuildInfoGrid(GridBoard, items);
    }

    // ── 공통 그리드 빌더 ─────────────────────────────────────────────

    private static void BuildInfoGrid(Grid grid, List<(string Label, string Value)> items)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < items.Count; i++)
        {
            int dataRow = i * 2;  // 데이터 행: 0, 2, 4, ...
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var (label, value) = items[i];

            var labelTb = new TextBlock
            {
                Text       = label,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                FontSize   = 11,
                Padding    = new Thickness(0, 6, 8, 6),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(labelTb, dataRow); Grid.SetColumn(labelTb, 0);

            var valueTb = new TextBlock
            {
                Text        = string.IsNullOrEmpty(value) ? "-" : value,
                Foreground  = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                FontSize    = 13,
                Padding     = new Thickness(0, 6, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(valueTb, dataRow); Grid.SetColumn(valueTb, 1);

            grid.Children.Add(labelTb);
            grid.Children.Add(valueTb);

            if (i < items.Count - 1)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var sep = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x35)),
                    Height     = 1
                };
                Grid.SetRow(sep, dataRow + 1);  // 구분선 행: 1, 3, 5, ...
                Grid.SetColumn(sep, 0);
                Grid.SetColumnSpan(sep, 2);
                grid.Children.Add(sep);
            }
        }
    }

    // ── 실시간 모니터 ────────────────────────────────────────────────

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (Tabs.SelectedItem == TabMonitor && !_monRunning)
            StartMonitor();
        else if (Tabs.SelectedItem != TabMonitor && _monRunning)
            StopMonitor();
    }

    private void MonitorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_monRunning) StopMonitor();
        else             StartMonitor();
    }

    private void StartMonitor()
    {
        if (!_monSvc.IsAvailable) return;
        _monRunning             = true;
        _monTimer.Start();
        MonitorToggleBtn.Content = "⏸ 일시정지";
        TxtMonitorDot.Text       = "● 모니터링";
        TxtMonitorDot.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        RefreshMonitor();
    }

    private void StopMonitor()
    {
        _monRunning             = false;
        _monTimer.Stop();
        MonitorToggleBtn.Content = "▶ 시작";
        TxtMonitorDot.Text       = "";
    }

    private void RefreshMonitor()
    {
        var readings = _monSvc.GetReadings();
        ListSensors.ItemsSource = readings.OrderBy(r => r.HardwareType)
                                          .ThenBy(r => r.SensorType)
                                          .ThenBy(r => r.SensorName)
                                          .ToList();
        TxtMonitorStatus.Text = $"센서 {readings.Count}개  |  {DateTime.Now:HH:mm:ss}";
    }

    // ── 내보내기 ─────────────────────────────────────────────────────

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureData()) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Markdown 파일 (*.md)|*.md",
            FileName = $"spec_{_data!.ComputerName}_{_data.ScannedAt:yyyyMMdd}"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, _exportSvc.ToMarkdown(_data!), Encoding.UTF8);
        UpdateStatus($"Markdown 저장: {dlg.FileName}");
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureData()) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "HTML 파일 (*.html)|*.html",
            FileName = $"spec_{_data!.ComputerName}_{_data.ScannedAt:yyyyMMdd}"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, _exportSvc.ToHtml(_data!), Encoding.UTF8);
        UpdateStatus($"HTML 저장: {dlg.FileName}");
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureData()) return;
        Clipboard.SetText(_exportSvc.ToText(_data!));
        UpdateStatus("텍스트를 클립보드에 복사했습니다.");
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureData()) return;

        // FlowDocument로 인쇄 다이얼로그
        var doc = BuildFlowDocument(_data!);
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.PageSize = new System.Windows.Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
        dlg.PrintDocument(paginator, $"Spec.View — {_data!.ComputerName}");
        UpdateStatus("인쇄 완료.");
    }

    private static FlowDocument BuildFlowDocument(HardwareData d)
    {
        var doc  = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 12 };
        var head = new Paragraph(new Run($"시스템 스펙 — {d.ComputerName}"))
        {
            FontSize  = 18, FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black
        };
        doc.Blocks.Add(head);
        doc.Blocks.Add(new Paragraph(new Run($"OS: {d.OsCaption}  |  스캔: {d.ScannedAt:yyyy-MM-dd HH:mm}"))
        {
            FontSize = 11, Foreground = Brushes.DimGray
        });

        void AddSection(string title, IEnumerable<(string, string)> rows)
        {
            doc.Blocks.Add(new Paragraph(new Run(title))
                { FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(160) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var rg = new TableRowGroup();
            foreach (var (l, v) in rows)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(l))
                    { FontSize = 11, Foreground = Brushes.Gray }));
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))));
                rg.Rows.Add(row);
            }
            table.RowGroups.Add(rg);
            doc.Blocks.Add(table);
        }

        if (d.Cpu is { } cpu)
            AddSection("CPU", [
                ("모델",   cpu.Name), ("소켓", cpu.Socket),
                ("코어/스레드", $"{cpu.Cores}C/{cpu.Threads}T"), ("클럭", cpu.ClockDisplay)
            ]);

        AddSection("메모리", [
            ("합계", d.Memory.TotalDisplay), ("슬롯", d.Memory.SlotDisplay),
            ("속도", d.Memory.MaxSpeedMHz > 0 ? $"{d.Memory.MaxSpeedMHz} MHz" : "-")
        ]);

        foreach (var gpu in d.Gpus)
            AddSection($"GPU — {gpu.Name}", [
                ("VRAM", gpu.VramDisplay), ("드라이버", gpu.DriverVersion),
                ("날짜", gpu.DriverDate)
            ]);

        var board = d.Board;
        AddSection("마더보드", [
            ("제조사", board.Manufacturer), ("제품", board.Product),
            ("BIOS", board.BiosVersion), ("날짜", board.BiosDate)
        ]);

        foreach (var drv in d.Drives)
            AddSection($"저장장치 — {drv.Model}", [
                ("용량", drv.SizeDisplay), ("인터페이스", drv.InterfaceType),
                ("S.M.A.R.T", drv.SmartStatus)
            ]);

        return doc;
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────────────

    private bool EnsureData()
    {
        if (_data is not null) return true;
        MessageBox.Show("먼저 스캔을 실행해주세요.", "Spec.View",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void ShowLoading(bool show, string detail = "")
    {
        LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TxtLoadingDetail.Text     = detail;
    }

    private void UpdateStatus(string msg) => TxtStatus.Text = msg;

    // ── 윈도우 컨트롤 ────────────────────────────────────────────────

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();
}
