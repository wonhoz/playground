using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BootMap;

// ── 데이터 레코드 ──────────────────────────────────────────────────────

record BootSession(
    DateTime StartTime,
    long TotalMs,
    long MainPathMs,
    long FirmwareMs,
    long KernelInitMs,
    long DriverInitMs,
    long SmssInitMs,
    long CriticalServicesMs,
    long ExplorerInitMs,
    long PostBootMs,
    int NumStartupApps,
    bool IsDegradation,
    long DegradationDelta
);

record DelayedService(string ServiceName, string DisplayName, long DurationMs);

// ── ViewModel ──────────────────────────────────────────────────────────

class SessionVm : INotifyPropertyChanged
{
    public BootSession Session { get; }

    public string StartTimeStr => Session.StartTime.ToString("MM/dd HH:mm:ss");
    public string TotalStr
    {
        get
        {
            if (Session.TotalMs >= 60000)
                return $"{Session.TotalMs / 1000.0:F1}s  ({Session.TotalMs:N0} ms)";
            return $"{Session.TotalMs:N0} ms";
        }
    }

    private BitmapSource? _miniBarImage;
    public BitmapSource? MiniBarImage
    {
        get => _miniBarImage;
        set { _miniBarImage = value; OnPropertyChanged(); }
    }

    public SessionVm(BootSession session) => Session = session;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

class SegmentVm : INotifyPropertyChanged
{
    public string Label { get; set; } = "";
    public long DurationMs { get; set; }
    public Color BarColor { get; set; }
    public double WidthRatio { get; set; }
    public string DurationStr => DurationMs >= 1000
        ? $"{DurationMs / 1000.0:F2} s"
        : $"{DurationMs:N0} ms";
    public string TooltipText => $"{Label}: {DurationStr}";

    private double _barWidth;
    public double BarWidth
    {
        get => _barWidth;
        set { _barWidth = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

class ServiceVm
{
    public string ServiceName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long DurationMs { get; set; }
    public string DurationStr => DurationMs >= 1000
        ? $"{DurationMs / 1000.0:F2} s"
        : $"{DurationMs:N0} ms";
}

// ── 단계 정의 ──────────────────────────────────────────────────────────

static class BootPhase
{
    public static readonly (string Label, Color Color)[] Phases =
    [
        ("BIOS / Firmware",  Color.FromRgb(0x7B, 0x1F, 0xA2)),
        ("OS Loader",        Color.FromRgb(0x15, 0x65, 0xC0)),
        ("Kernel Init",      Color.FromRgb(0x02, 0x77, 0xBD)),
        ("Driver Init",      Color.FromRgb(0x00, 0x83, 0x8F)),
        ("Session Mgr",      Color.FromRgb(0x2E, 0x7D, 0x32)),
        ("Services",         Color.FromRgb(0x55, 0x8B, 0x2F)),
        ("Explorer",         Color.FromRgb(0xF5, 0x7F, 0x17)),
        ("Post-Boot",        Color.FromRgb(0xE6, 0x51, 0x00)),
    ];
}

// ── 메인 윈도우 ────────────────────────────────────────────────────────

public partial class MainWindow : Window
{
    private const string LogName = "Microsoft-Windows-Diagnostics-Performance/Operational";

    private readonly ObservableCollection<SessionVm> _sessions = [];
    private readonly ObservableCollection<SegmentVm> _segments = [];
    private readonly ObservableCollection<ServiceVm> _services = [];

    private List<BootSession> _rawSessions = [];
    private Dictionary<DateTime, List<DelayedService>> _svcMap = [];

    private double _timelineWidth = 400;

    public MainWindow()
    {
        InitializeComponent();
        LbSessions.ItemsSource = _sessions;
        IcTimeline.ItemsSource = _segments;
        LvServices.ItemsSource = _services;

        Loaded += (_, _) => LoadData();
    }

    // ── 데이터 로드 ────────────────────────────────────────────────────

    private async void LoadData()
    {
        TbStatus.Text = "ETW 로그 읽는 중...";
        BtnRefresh.IsEnabled = false;

        try
        {
            var (sessions, svcMap) = await Task.Run(ParseEventLog);
            _rawSessions = sessions;
            _svcMap = svcMap;

            _sessions.Clear();
            foreach (var s in sessions)
            {
                var vm = new SessionVm(s);
                vm.MiniBarImage = BuildMiniBar(s, 260, 4);
                _sessions.Add(vm);
            }

            // 비교 콤보 초기화
            CbCompare.ItemsSource = null;
            var compareItems = new List<object> { "(없음)" };
            compareItems.AddRange(sessions.Select(s => (object)s.StartTime.ToString("MM/dd HH:mm:ss")));
            CbCompare.ItemsSource = compareItems;
            CbCompare.SelectedIndex = 0;

            if (_sessions.Count > 0)
                LbSessions.SelectedIndex = 0;

            TbStatus.Text = $"부팅 기록 {sessions.Count}건 로드 완료  |  {DateTime.Now:HH:mm:ss}";
        }
        catch (UnauthorizedAccessException)
        {
            TbStatus.Text = "관리자 권한 필요 — 이 앱은 관리자로 실행해야 합니다.";
            MessageBox.Show(
                "ETW 로그를 읽으려면 관리자 권한이 필요합니다.\n앱을 관리자로 실행해 주세요.",
                "권한 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            TbStatus.Text = $"오류: {ex.Message}";
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private static (List<BootSession> sessions, Dictionary<DateTime, List<DelayedService>> svcMap)
        ParseEventLog()
    {
        var sessions = new List<BootSession>();
        var svcMap = new Dictionary<DateTime, List<DelayedService>>();

        // ID 100 — 부팅 단계별 시간
        var query100 = new EventLogQuery(LogName, PathType.LogName,
            "*[System[EventID=100]]");
        using (var reader = new EventLogReader(query100))
        {
            EventRecord? rec;
            while ((rec = reader.ReadEvent()) != null)
            {
                using (rec)
                {
                    try
                    {
                        var props = rec.Properties;
                        if (props.Count < 32) continue;

                        long Get(int i) => Convert.ToInt64(props[i].Value);

                        var session = new BootSession(
                            StartTime:          rec.TimeCreated ?? DateTime.MinValue,
                            TotalMs:            Get(0),
                            MainPathMs:         Get(1),
                            FirmwareMs:         TryGet(props, 31),
                            KernelInitMs:       TryGet(props, 7),
                            DriverInitMs:       TryGet(props, 8),
                            SmssInitMs:         TryGet(props, 13),
                            CriticalServicesMs: TryGet(props, 14),
                            ExplorerInitMs:     TryGet(props, 17),
                            PostBootMs:         TryGet(props, 19),
                            NumStartupApps:     (int)TryGet(props, 18),
                            IsDegradation:      TryGetBool(props, 25),
                            DegradationDelta:   TryGet(props, 29)
                        );
                        sessions.Add(session);
                    }
                    catch { /* 파싱 실패 레코드 건너뜀 */ }
                }
            }
        }

        sessions.Sort((a, b) => b.StartTime.CompareTo(a.StartTime));

        // ID 101 — 병목 서비스
        var query101 = new EventLogQuery(LogName, PathType.LogName,
            "*[System[EventID=101]]");
        using (var reader = new EventLogReader(query101))
        {
            EventRecord? rec;
            while ((rec = reader.ReadEvent()) != null)
            {
                using (rec)
                {
                    try
                    {
                        var props = rec.Properties;
                        var svcName = props.Count > 0 ? props[0].Value?.ToString() ?? "" : "";
                        var displayName = props.Count > 1 ? props[1].Value?.ToString() ?? "" : "";
                        var durationMs = props.Count > 2 ? Convert.ToInt64(props[2].Value) : 0;
                        var ts = rec.TimeCreated ?? DateTime.MinValue;

                        // 가장 가까운 세션에 연결
                        var match = sessions.MinBy(s => Math.Abs((s.StartTime - ts).TotalSeconds));
                        if (match != null)
                        {
                            if (!svcMap.TryGetValue(match.StartTime, out var list))
                                svcMap[match.StartTime] = list = [];
                            list.Add(new DelayedService(svcName, displayName, durationMs));
                        }
                    }
                    catch { }
                }
            }
        }

        return (sessions, svcMap);
    }

    private static long TryGet(IList<EventProperty> props, int i)
    {
        if (i >= props.Count || props[i].Value == null) return 0;
        try { return Convert.ToInt64(props[i].Value); }
        catch { return 0; }
    }

    private static bool TryGetBool(IList<EventProperty> props, int i)
    {
        if (i >= props.Count || props[i].Value == null) return false;
        try { return Convert.ToBoolean(props[i].Value); }
        catch { return false; }
    }

    // ── 선택 변경 ──────────────────────────────────────────────────────

    private void LbSessions_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (LbSessions.SelectedItem is not SessionVm vm) return;

        ShowSession(vm.Session);
        UpdateCompare();
    }

    private void ShowSession(BootSession s)
    {
        // 헤더 시간
        TbTotalTime.Text = s.TotalMs >= 60000
            ? $"총 {s.TotalMs / 1000.0:F1}초 ({s.TotalMs:N0} ms)"
            : $"총 {s.TotalMs:N0} ms";

        // 타임라인 세그먼트 빌드
        var segs = BuildSegments(s);
        _segments.Clear();
        foreach (var seg in segs) _segments.Add(seg);

        RecalcBarWidths();

        // 병목 서비스
        _services.Clear();
        var svcs = GetServicesForSession(s);
        foreach (var svc in svcs.OrderByDescending(x => x.DurationMs))
            _services.Add(new ServiceVm
            {
                ServiceName = svc.ServiceName,
                DisplayName = svc.DisplayName,
                DurationMs  = svc.DurationMs,
            });

        // 상태바 업데이트
        var degradeInfo = s.IsDegradation
            ? $"  ⚠ 성능 저하 감지 (+{s.DegradationDelta:N0}ms)"
            : "";
        TbStatus.Text = $"{s.StartTime:yyyy-MM-dd HH:mm:ss}  |  시작 앱 {s.NumStartupApps}개{degradeInfo}";
    }

    private static List<SegmentVm> BuildSegments(BootSession s)
    {
        // OS Loader = MainPath - 나머지 단계 합산
        var knownSum = s.KernelInitMs + s.DriverInitMs + s.SmssInitMs
                     + s.CriticalServicesMs + s.ExplorerInitMs + s.PostBootMs;
        var osLoaderMs = Math.Max(0, s.MainPathMs - knownSum);

        var items = new[]
        {
            (s.FirmwareMs,         0),  // BIOS / Firmware
            (osLoaderMs,           1),  // OS Loader
            (s.KernelInitMs,       2),  // Kernel Init
            (s.DriverInitMs,       3),  // Driver Init
            (s.SmssInitMs,         4),  // Session Mgr
            (s.CriticalServicesMs, 5),  // Services
            (s.ExplorerInitMs,     6),  // Explorer
            (s.PostBootMs,         7),  // Post-Boot
        };

        var total = (double)Math.Max(1, s.TotalMs);
        var result = new List<SegmentVm>();
        foreach (var (ms, idx) in items)
        {
            if (ms <= 0) continue;
            var (label, color) = BootPhase.Phases[idx];
            result.Add(new SegmentVm
            {
                Label      = label,
                DurationMs = ms,
                BarColor   = color,
                WidthRatio = ms / total,
                BarWidth   = 100, // 초기값, RecalcBarWidths에서 갱신
            });
        }
        return result;
    }

    // ── 타임라인 너비 계산 ─────────────────────────────────────────────

    private void IcTimeline_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _timelineWidth = e.NewSize.Width;
        RecalcBarWidths();
    }

    private void RecalcBarWidths()
    {
        var available = Math.Max(10, _timelineWidth - 230);
        foreach (var seg in _segments)
            seg.BarWidth = seg.WidthRatio * available;
    }

    // ── 비교 기능 ──────────────────────────────────────────────────────

    private void CbCompare_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCompare();
    }

    private void UpdateCompare()
    {
        if (LbSessions.SelectedItem is not SessionVm current)
        {
            TbDelta.Text = "";
            return;
        }

        var sel = CbCompare.SelectedIndex;
        if (sel <= 0 || _rawSessions.Count == 0)
        {
            TbDelta.Text = "";
            return;
        }

        // 선택된 비교 대상 인덱스 (콤보 인덱스 1 = 세션 0)
        var compareIdx = sel - 1;
        if (compareIdx >= _rawSessions.Count) return;

        var compareSession = _rawSessions[compareIdx];
        if (compareSession.StartTime == current.Session.StartTime)
        {
            TbDelta.Text = "";
            return;
        }

        var delta = current.Session.TotalMs - compareSession.TotalMs;
        var sign = delta >= 0 ? "+" : "";
        var color = delta > 0
            ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
            : new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));

        TbDelta.Text = $"비교: {sign}{delta:N0} ms";
        TbDelta.Foreground = color;
    }

    // ── 병목 서비스 조회 ───────────────────────────────────────────────

    private List<DelayedService> GetServicesForSession(BootSession s)
    {
        return _svcMap.TryGetValue(s.StartTime, out var list) ? list : [];
    }

    // ── 미니 컬러바 렌더링 ─────────────────────────────────────────────

    private static BitmapSource BuildMiniBar(BootSession s, int width, int height)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double total = Math.Max(1, s.TotalMs);
            double x = 0;

            var knownSum = s.KernelInitMs + s.DriverInitMs + s.SmssInitMs
                         + s.CriticalServicesMs + s.ExplorerInitMs + s.PostBootMs;
            var osLoaderMs = Math.Max(0L, s.MainPathMs - knownSum);

            var phases = new[]
            {
                (s.FirmwareMs,         0),
                (osLoaderMs,           1),
                (s.KernelInitMs,       2),
                (s.DriverInitMs,       3),
                (s.SmssInitMs,         4),
                (s.CriticalServicesMs, 5),
                (s.ExplorerInitMs,     6),
                (s.PostBootMs,         7),
            };

            foreach (var (ms, idx) in phases)
            {
                if (ms <= 0) continue;
                var w = ms / total * width;
                var color = BootPhase.Phases[idx].Color;
                dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(x, 0, w, height));
                x += w;
            }
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    // ── 버튼 핸들러 ────────────────────────────────────────────────────

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadData();

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Boot.Map — Windows 부팅 타임라인 분석기\n\n" +
            "데이터 소스:\n" +
            "  • Event ID 100: 부팅 단계별 시간 (관리자 권한 필요)\n" +
            "  • Event ID 101: 부팅 지연 서비스\n\n" +
            "색상 범례:\n" +
            "  ■ 보라: BIOS/Firmware\n" +
            "  ■ 남색: OS Loader\n" +
            "  ■ 하늘: Kernel Init\n" +
            "  ■ 청록: Driver Init\n" +
            "  ■ 녹색: Session Mgr / Services\n" +
            "  ■ 주황: Explorer / Post-Boot\n\n" +
            "비교: 왼쪽 목록에서 부팅을 선택 후\n" +
            "비교 콤보박스에서 다른 부팅을 선택하면\n" +
            "총 시간 차이(±ms)를 표시합니다.",
            "도움말", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
