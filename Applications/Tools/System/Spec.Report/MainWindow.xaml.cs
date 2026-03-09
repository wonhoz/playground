using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;

namespace SpecReport;

public partial class MainWindow : Window
{
    private SystemReport?              _report;
    private readonly SystemInfoCollector  _collector  = new();
    private readonly JsonReportService    _jsonSvc    = new();
    private readonly HtmlReportGenerator  _htmlGen    = new();
    private          ObservableCollection<InstalledApp> _allSoftware = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    // ── 윈도우 컨트롤 ─────────────────────────────────────
    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }
    private void MinBtn_Click  (object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ── 새로고침 ─────────────────────────────────────────
    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        BtnRefresh.IsEnabled      = false;
        TxtLoadingStep.Text       = "하드웨어 정보 수집 중...";

        try
        {
            _report = await Task.Run(() => _collector.Collect());
            DisplayReport(_report);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BtnRefresh.IsEnabled      = true;
        }
    }

    // ── 화면 표시 ────────────────────────────────────────
    private void DisplayReport(SystemReport r)
    {
        // 헤더
        TxtComputer   .Text = r.ComputerName;
        TxtUser       .Text = $"{r.UserName} @ {r.ComputerName}";
        TxtCollectedAt.Text = r.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss");

        // CPU
        TxtCpuName  .Text = r.Cpu.Name;
        TxtCpuSub   .Text = $"{r.Cpu.Manufacturer}  ·  {r.Cpu.Architecture}";
        TxtCpuClock .Text = $"{r.Cpu.MaxClockGHz:F2} GHz";
        TxtCpuCores .Text = r.Cpu.PhysicalCores.ToString();
        TxtCpuThreads.Text = r.Cpu.LogicalCores.ToString();
        TxtCpuSocket.Text = string.IsNullOrEmpty(r.Cpu.Socket) ? r.Cpu.Architecture : $"{r.Cpu.Socket} / {r.Cpu.Architecture}";

        // RAM
        TxtRamTotal.Text = JsonReportService.FormatBytes(r.TotalRamBytes);
        RamSlotList.ItemsSource = r.RamSlots.Select(s => new
        {
            s.Slot,
            CapacityGb  = JsonReportService.FormatBytes(s.CapacityBytes),
            s.MemoryType,
            s.SpeedMHz,
            Manufacturer = string.IsNullOrEmpty(s.Manufacturer) ? "" : s.Manufacturer
        }).ToList();

        // GPU
        GpuList.ItemsSource = r.Gpus.Select((g, i) => new
        {
            Label       = r.Gpus.Count > 1 ? $"GPU #{i + 1}" : "GPU",
            g.Name,
            VramText    = JsonReportService.FormatBytes(g.VramBytes),
            DriverLine  = $"드라이버  {g.DriverVersion}  ({g.DriverDate})",
            ResolutionLine = g.CurrentWidth > 0
                ? $"현재 해상도  {g.CurrentWidth} × {g.CurrentHeight} @ {g.RefreshRate} Hz"
                : ""
        }).ToList();

        // 스토리지
        DriveList.ItemsSource = r.Drives.Select(d =>
        {
            double pct = d.TotalBytes > 0 ? (double)(d.TotalBytes - d.FreeBytes) / d.TotalBytes : 0;
            var barColor = pct > 0.9
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x60))
                : pct > 0.7
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x90, 0x40))
                    : new SolidColorBrush(Color.FromRgb(0x32, 0x80, 0xFF));

            // BarWidth는 최대 너비를 동적으로 계산하기 어려우므로 비율 값을 저장
            return new
            {
                d.DriveLetter,
                Label      = string.IsNullOrEmpty(d.Label) ? "로컬 디스크" : d.Label,
                d.FileSystem,
                d.MediaType,
                d.Model,
                SizeText   = JsonReportService.FormatBytes(d.TotalBytes),
                BarWidth   = pct * 400,  // 최대 400px 기준
                BarColor   = barColor,
                UsageText  = $"{JsonReportService.FormatBytes(d.TotalBytes - d.FreeBytes)} / {JsonReportService.FormatBytes(d.TotalBytes)} 사용 · 여유 {JsonReportService.FormatBytes(d.FreeBytes)} ({pct:P0})"
            };
        }).ToList();

        // OS
        TxtOsName  .Text = r.Os.Caption;
        TxtOsBuild .Text = $"{r.Os.Version} (Build {r.Os.BuildNumber})";
        TxtOsArch  .Text = r.Os.Architecture;
        TxtDotNet  .Text = r.Os.DotNetVersion;
        TxtWinUpdate.Text = r.Os.WindowsUpdateDate;
        if (r.Os.LastBoot != default)
        {
            var uptime = DateTime.Now - r.Os.LastBoot;
            TxtOsBoot.Text = $"{r.Os.LastBoot:yyyy-MM-dd HH:mm}  ({(int)uptime.TotalDays}일 {uptime.Hours}시간 가동)";
        }

        // 네트워크
        NetworkList.ItemsSource = r.NetworkAdapters.Select(a => new
        {
            Icon       = a.IsWireless ? "📶" : "🔌",
            a.Name,
            IpLine     = string.Join(", ", a.IpAddresses),
            a.MacAddress,
            DnsLine    = string.Join(", ", a.DnsServers),
            a.Speed
        }).ToList();

        // 보안
        SetSecStatus(TxtDefender,   r.Security.DefenderEnabled, "활성", "비활성");
        SetSecStatus(TxtFirewall,   r.Security.FirewallEnabled, "활성", "비활성");
        TxtBitLocker.Text      = r.Security.BitLockerStatus;
        TxtBitLocker.Foreground = r.Security.BitLockerStatus == "On"
            ? FindResource("GreenBrush")  as SolidColorBrush
            : r.Security.BitLockerStatus == "Unknown"
                ? FindResource("Fg3Brush") as SolidColorBrush
                : FindResource("OrangeBrush") as SolidColorBrush;
        SetSecStatus(TxtAutoUpdate, r.Security.AutoUpdateEnabled, "활성", "비활성");

        // 소프트웨어
        _allSoftware = new ObservableCollection<InstalledApp>(r.Software);
        TxtSwCount.Text = $"({_allSoftware.Count}개)";
        ApplySwFilter(TxtSwFilter.Text);
    }

    private void SetSecStatus(System.Windows.Controls.TextBlock tb, bool ok, string okText, string failText)
    {
        tb.Text       = ok ? okText : failText;
        tb.Foreground = ok
            ? FindResource("GreenBrush") as SolidColorBrush
            : FindResource("RedBrush")   as SolidColorBrush;
    }

    // ── 소프트웨어 검색 ────────────────────────────────────
    private void TxtSwFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplySwFilter(TxtSwFilter.Text);
    }

    private void ApplySwFilter(string keyword)
    {
        keyword = keyword.Trim();
        SwListBox.ItemsSource = string.IsNullOrEmpty(keyword)
            ? _allSoftware
            : new ObservableCollection<InstalledApp>(
                _allSoftware.Where(a =>
                    a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    a.Publisher.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    // ── HTML 리포트 ───────────────────────────────────────
    private void BtnHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) { ShowMsg("먼저 새로고침을 실행해주세요."); return; }

        var html = _htmlGen.Generate(_report);
        var path = Path.Combine(Path.GetTempPath(),
                                $"SpecReport_{_report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(path, html, System.Text.Encoding.UTF8);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    // ── JSON 저장 ─────────────────────────────────────────
    private void BtnJson_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) { ShowMsg("먼저 새로고침을 실행해주세요."); return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "리포트 저장",
            Filter     = "Spec Report (*.srep)|*.srep|JSON (*.json)|*.json",
            FileName   = $"SpecReport_{_report.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".srep"
        };
        if (dlg.ShowDialog() != true) return;

        _jsonSvc.Save(_report, dlg.FileName);
        ShowMsg($"저장 완료: {Path.GetFileName(dlg.FileName)}");
    }

    // ── 비교 ─────────────────────────────────────────────
    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) { ShowMsg("먼저 새로고침을 실행해주세요."); return; }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "비교할 이전 리포트 선택",
            Filter = "Spec Report (*.srep)|*.srep|JSON (*.json)|*.json|모든 파일 (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        var oldReport = _jsonSvc.Load(dlg.FileName);
        if (oldReport == null) { ShowMsg("파일을 불러올 수 없습니다."); return; }

        var result   = _jsonSvc.Compare(oldReport, _report);
        var html     = _htmlGen.GenerateCompare(result);
        var htmlPath = Path.Combine(Path.GetTempPath(),
                                   $"SpecCompare_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(htmlPath, html, System.Text.Encoding.UTF8);
        Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
    }

    private void ShowMsg(string msg)
        => MessageBox.Show(msg, "Spec.Report", MessageBoxButton.OK, MessageBoxImage.Information);
}
