using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>
/// 🏆 급등락 전광판 — 한국·미국 시장 <b>전체</b>에서 급등/급락/거래량 급증 상위 종목을 주기 갱신으로 리스트업.
/// 한국은 KIS 순위 API(등락률·거래증가율 · 실시간), 미국은 Alpaca 스크리너(실시간 · Yahoo는 정규장 지연)
/// 우선·없으면 Yahoo 폴백. 직전 갱신에 없던 <b>신규 진입 종목은 N 배지 + 하이라이트</b>로 표시한다.
/// 행 더블클릭 시 네이버 증권/Yahoo 시세 페이지를 연다.
/// </summary>
public partial class LeaderboardWindow : Window
{
    private readonly PriceSourceRegistry _registry;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _timer;
    private readonly Brush _up, _down, _muted;
    private readonly Brush _newBg = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xD5, 0x4F));
    private bool _busy;
    // 패널 키 → 직전 갱신의 종목 집합(신규 진입 판정 · 첫 로드는 배지 없음).
    private readonly Dictionary<string, HashSet<string>> _prevSymbols = new();

    private readonly ObservableCollection<MoverVm> _krGain = new(), _krLose = new(), _krVol = new();
    private readonly ObservableCollection<MoverVm> _usGain = new(), _usLose = new(), _usVol = new();

    public LeaderboardWindow(PriceSourceRegistry registry, AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _registry = registry;
        _config = config;
        _up = (Brush)Application.Current.Resources["BullBrush"];      // 한국식: 상승 빨강
        _down = (Brush)Application.Current.Resources["BearBrush"];    // 하락 파랑
        _muted = (Brush)Application.Current.Resources["FgMuted"];

        KrGainList.ItemsSource = _krGain; KrLoseList.ItemsSource = _krLose; KrVolList.ItemsSource = _krVol;
        UsGainList.ItemsSource = _usGain; UsLoseList.ItemsSource = _usLose; UsVolList.ItemsSource = _usVol;
        UsHeaderText.Text = _config.HasAlpacaKeys ? "🇺🇸 미국 (Alpaca 실시간)" : "🇺🇸 미국 (Yahoo · 지연 가능)";

        // 갱신 주기 콤보 복원(가장 가까운 항목 선택).
        int sec = Math.Max(10, _config.LeaderboardRefreshSeconds);
        int idx = sec <= 15 ? 0 : sec <= 30 ? 1 : sec <= 60 ? 2 : 3;
        IntervalCombo.SelectedIndex = idx;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SelectedIntervalSeconds()) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private int SelectedIntervalSeconds()
        => IntervalCombo.SelectedItem is ComboBoxItem it && int.TryParse(it.Tag as string, out var s) ? s : 30;

    private void Interval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;   // InitializeComponent 중 발동 방어
        int sec = SelectedIntervalSeconds();
        _timer.Interval = TimeSpan.FromSeconds(sec);
        _config.LeaderboardRefreshSeconds = sec;
        _config.Save();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        RefreshBtn.IsEnabled = false;
        try
        {
            // 한국·미국 6개 순위를 병렬 조회 — 실패는 패널별 상태 문구로만(다른 패널은 계속).
            var krG = FetchSafeAsync(() => _registry.KrMoversAsync(MoverKind.Gainers));
            var krL = FetchSafeAsync(() => _registry.KrMoversAsync(MoverKind.Losers));
            var krV = FetchSafeAsync(() => _registry.KrMoversAsync(MoverKind.VolumeSurge));
            var usG = FetchSafeAsync(() => _registry.UsMoversAsync(MoverKind.Gainers));
            var usL = FetchSafeAsync(() => _registry.UsMoversAsync(MoverKind.Losers));
            var usV = FetchSafeAsync(() => _registry.UsMoversAsync(MoverKind.VolumeSurge));
            await Task.WhenAll(krG, krL, krV, usG, usL, usV);

            // 패널별로 모두 반영한 뒤(?? 단락 평가로 건너뛰지 않도록) 첫 에러만 상태에 표시.
            string? kr1 = Apply(_krGain, "krG", krG.Result, kr: true);
            string? kr2 = Apply(_krLose, "krL", krL.Result, kr: true);
            string? kr3 = Apply(_krVol, "krV", krV.Result, kr: true);
            string? us1 = Apply(_usGain, "usG", usG.Result, kr: false);
            string? us2 = Apply(_usLose, "usL", usL.Result, kr: false);
            string? us3 = Apply(_usVol, "usV", usV.Result, kr: false);
            string? krErr = kr1 ?? kr2 ?? kr3;
            string? usErr = us1 ?? us2 ?? us3;

            KrStatusText.Text = krErr ?? $"{KrSessionLabel()} · 갱신 {DateTime.Now:HH:mm:ss}";
            UsStatusText.Text = usErr ?? $"{UsSessionLabel()} · 갱신 {DateTime.Now:HH:mm:ss}";
            StatusText.Text = $"자동 갱신 {SelectedIntervalSeconds()}초 · 더블클릭=시세 페이지";
        }
        finally
        {
            _busy = false;
            RefreshBtn.IsEnabled = true;
        }
    }

    private static async Task<(List<MoverRow>? Rows, string? Error)> FetchSafeAsync(Func<Task<List<MoverRow>>> fetch)
    {
        try { return (await fetch(), null); }
        catch (Exception ex) { return (null, ex.Message); }
    }

    /// <summary>패널 1개 반영: 성공이면 행 갱신(신규 진입 N 배지) 후 null, 실패면 에러 문구(짧게) 반환.</summary>
    private string? Apply(ObservableCollection<MoverVm> list, string key,
        (List<MoverRow>? Rows, string? Error) result, bool kr)
    {
        if (result.Rows is null)
            return result.Error is { } err ? (err.Length > 60 ? err[..60] : err) : "조회 실패";

        bool first = !_prevSymbols.TryGetValue(key, out var prev);
        var now = new HashSet<string>(result.Rows.Select(r => r.Symbol), StringComparer.OrdinalIgnoreCase);
        list.Clear();
        foreach (var r in result.Rows)
        {
            bool isNew = !first && prev != null && !prev.Contains(r.Symbol);
            list.Add(ToVm(r, kr, isNew));
        }
        _prevSymbols[key] = now;
        return null;
    }

    private MoverVm ToVm(MoverRow r, bool kr, bool isNew) => new()
    {
        Symbol = r.Symbol,
        IsKr = kr,
        RankText = r.Rank.ToString(),
        Display = r.Display,
        SymbolSuffix = kr ? r.Symbol : "",   // 미국은 티커가 곧 이름
        PriceText = r.Price <= 0 ? "-" : kr ? r.Price.ToString("N0") : r.Price.ToString("N2"),
        ChgText = $"{r.ChangeRate:+0.0;-0.0;0.0}%",
        ChgBrush = r.ChangeRate > 0 ? _up : r.ChangeRate < 0 ? _down : _muted,
        VolText = r.Extra.Length > 0 ? r.Extra : r.Volume > 0 ? FormatVolume(r.Volume, kr) : "",
        NewBadge = isNew ? "N" : "",
        RowBg = isNew ? _newBg : Brushes.Transparent,
    };

    /// <summary>거래량 축약 표기 — KR: 1.2억/3456만 · US: 12.3M/456K.</summary>
    private static string FormatVolume(long vol, bool kr) => kr
        ? vol >= 100_000_000 ? $"{vol / 100_000_000.0:0.0}억주" : vol >= 10_000 ? $"{vol / 10_000.0:0}만주" : $"{vol:N0}주"
        : vol >= 1_000_000 ? $"{vol / 1_000_000.0:0.0}M주" : vol >= 1_000 ? $"{vol / 1_000.0:0}K주" : $"{vol:N0}주";

    private static string KrSessionLabel()
    {
        var now = DateTime.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return "휴장";
        var t = now.TimeOfDay;
        if (t >= new TimeSpan(9, 0, 0) && t < new TimeSpan(15, 30, 0)) return "정규장";
        if (t >= new TimeSpan(8, 0, 0) && t < new TimeSpan(9, 0, 0)) return "장전";
        if (t >= new TimeSpan(15, 30, 0) && t < new TimeSpan(20, 0, 0)) return "장후(NXT)";
        return "장외(전일 기준)";
    }

    private static string UsSessionLabel() => UsMarket.CurrentSession() switch
    {
        UsSession.Regular => "정규장",
        UsSession.Pre => "프리마켓",
        UsSession.After => "애프터마켓",
        _ => "휴장(전일 기준)",
    };

    /// <summary>행 더블클릭 → 시세 페이지(KR: 네이버 증권 · US: Yahoo Finance).</summary>
    private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { Tag: MoverVm vm }) return;
        string url = vm.IsKr
            ? $"https://finance.naver.com/item/main.naver?code={vm.Symbol}"
            : $"https://finance.yahoo.com/quote/{Uri.EscapeDataString(vm.Symbol)}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* 브라우저 열기 실패는 무시 */ }
    }
}

/// <summary>전광판 행 1건(바인딩용).</summary>
public sealed class MoverVm
{
    public string Symbol { get; set; } = "";
    public bool IsKr { get; set; }
    public string RankText { get; set; } = "";
    public string Display { get; set; } = "";
    public string SymbolSuffix { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string ChgText { get; set; } = "";
    public Brush ChgBrush { get; set; } = Brushes.Gray;
    public string VolText { get; set; } = "";
    public string NewBadge { get; set; } = "";
    public Brush RowBg { get; set; } = Brushes.Transparent;
}
