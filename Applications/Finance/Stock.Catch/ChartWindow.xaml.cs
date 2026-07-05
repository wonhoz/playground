using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using Stock.Catch.Indicators;
using Stock.Catch.Models;
using Stock.Catch.Services;
using Stock.Catch.Views.Controls;

namespace Stock.Catch;

public partial class ChartWindow : Window
{
    private readonly PriceSourceRegistry _registry;
    private readonly AppConfig _config;
    private readonly string _code;
    private readonly string _name;
    private readonly DispatcherTimer _timer = new();
    private IndicatorSet? _set;
    private bool _ready;
    private bool _loading;

    // ── 분봉 CSV 모드 ──
    private bool _csvMode;
    private string _csvTitle = "";
    private List<MinuteSignal> _csvSignals = new();   // 불러온 CSV(+반대 짝 교차)로 계산한 1분 TF 시그널

    private sealed record IntervalItem(BarInterval Iv)
    {
        public override string ToString() => ChartDataService.Label(Iv);
    }
    private sealed record SourceItem(ChartSourceKind Kind, string Name)
    {
        public override string ToString() => Name;
    }
    private sealed record PeriodItem(int Seconds, string Label)
    {
        public override string ToString() => Label;
    }

    public ChartWindow(string code, string name, PriceSourceRegistry registry, AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _registry = registry;
        _config = config;
        _code = code;
        _name = name;

        Title = $"{(string.IsNullOrEmpty(name) ? code : name)} ({code}) — 차트";
        TitleText.Text = string.IsNullOrEmpty(name) ? code : $"{name} ({code})";

        IntervalCombo.ItemsSource = new[]
        {
            new IntervalItem(BarInterval.Min1), new IntervalItem(BarInterval.Min5),
            new IntervalItem(BarInterval.Min15), new IntervalItem(BarInterval.Min30),
            new IntervalItem(BarInterval.Min60), new IntervalItem(BarInterval.Day),
            new IntervalItem(BarInterval.Week), new IntervalItem(BarInterval.Month),
        };
        SourceCombo.ItemsSource = new[]
        {
            new SourceItem(ChartSourceKind.Yahoo, "Yahoo (분/일/주/월)"),
            new SourceItem(ChartSourceKind.Kis, "KIS (당일분봉·일/주/월)"),
        };
        PeriodCombo.ItemsSource = new[]
        {
            new PeriodItem(5, "5초"), new PeriodItem(10, "10초"),
            new PeriodItem(30, "30초"), new PeriodItem(60, "1분"),
        };
        SignalFilterCombo.ItemsSource = new[]
        {
            "전체 시그널", "🚀🔥✅ 진입 후보", "📈↗ 반등 계열", "📉🔻🔁 고점·전환", "⚠ 약한 확인",
        };
        SignalFilterCombo.SelectedIndex = 0;

        // ── 저장된 차트 설정 복원(_ready=false 동안이라 변경 이벤트는 무시됨) ──
        IntervalCombo.SelectedItem = IntervalCombo.Items.Cast<IntervalItem>()
            .FirstOrDefault(x => x.Iv == config.ChartInterval) ?? IntervalCombo.Items[5];
        SourceCombo.SelectedItem = SourceCombo.Items.Cast<SourceItem>()
            .FirstOrDefault(x => x.Kind == config.ChartSource) ?? SourceCombo.Items[0];
        PeriodCombo.SelectedItem = PeriodCombo.Items.Cast<PeriodItem>()
            .FirstOrDefault(x => x.Seconds == config.ChartPeriodSec) ?? PeriodCombo.Items[1];
        BollCheck.IsChecked = config.ChartBollinger;
        MaCheck.IsChecked = config.ChartMa;
        RsiCheck.IsChecked = config.ChartRsi;
        VolCheck.IsChecked = config.ChartVolume;
        AutoCheck.IsChecked = config.ChartAutoRefresh;

        _timer.Tick += async (_, _) => await LoadAsync();

        _ready = true;
        Loaded += async (_, _) =>
        {
            await LoadAsync();
            if (AutoCheck.IsChecked == true) { ApplyTimerInterval(); _timer.Start(); }
        };
        Closed += (_, _) => { _timer.Stop(); SaveState(); };
    }

    private void SaveState()
    {
        _config.ChartInterval = SelectedInterval();
        _config.ChartSource = SelectedSource();
        _config.ChartBollinger = BollCheck.IsChecked == true;
        _config.ChartMa = MaCheck.IsChecked == true;
        _config.ChartRsi = RsiCheck.IsChecked == true;
        _config.ChartVolume = VolCheck.IsChecked == true;
        _config.ChartAutoRefresh = AutoCheck.IsChecked == true;
        _config.ChartPeriodSec = PeriodCombo.SelectedItem is PeriodItem p ? p.Seconds : 10;
        _config.Save();
    }

    private BarInterval SelectedInterval() => ((IntervalItem)IntervalCombo.SelectedItem).Iv;
    private ChartSourceKind SelectedSource() => ((SourceItem)SourceCombo.SelectedItem).Kind;

    private ChartOptions CurrentOptions()
    {
        // CSV 모드는 분봉 고정(시:분). 온라인은 봉종류에 맞춘 포맷.
        string fmt = _csvMode ? "MM-dd HH:mm"
            : ChartDataService.IsIntraday(SelectedInterval()) ? "MM-dd HH:mm"
            : SelectedInterval() == BarInterval.Month ? "yy-MM" : "yy-MM-dd";
        return new ChartOptions(
            BollCheck.IsChecked == true, MaCheck.IsChecked == true,
            RsiCheck.IsChecked == true, VolCheck.IsChecked == true, fmt);
    }

    // ────────────────────────────── 분봉 CSV 불러오기 ──────────────────────────────
    private async void LoadCsv_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog
        {
            Title = "분봉 CSV 선택 (date,time,open,close,low,high,volume)",
            Filter = "CSV (쉼표 구분)|*.csv|모든 파일|*.*",
            InitialDirectory = Directory.Exists(_config.LastChartCsvDir) ? _config.LastChartCsvDir
                : Directory.Exists(_config.LastSignalDir) ? _config.LastSignalDir
                : Directory.Exists(_config.LastExportDir) ? _config.LastExportDir : null
        };
        if (open.ShowDialog(this) != true) return;

        // 마지막 불러온 폴더 기억 — 다음에 같은 위치에서 열리도록.
        _config.LastChartCsvDir = Path.GetDirectoryName(open.FileName) ?? "";
        _config.Save();

        try
        {
            var bars = MinuteCsvIo.Parse(open.FileName);
            if (bars.Count < 26) throw new InvalidDataException($"분봉 부족({bars.Count}봉, 최소 26봉)");
            string stem = Path.GetFileNameWithoutExtension(open.FileName);
            var meta = MinuteCsvIo.ParseStem(open.FileName);
            string code = meta?.Code ?? _code;
            string name = meta is { } m && _config.Watchlist.FirstOrDefault(w =>
                string.Equals(w.Symbol, m.Code, StringComparison.OrdinalIgnoreCase))?.Name is { Length: > 0 } n ? n
                : meta?.Name ?? _name;

            // 반대 짝(레버리지↔인버스) CSV로 🔁 전환 확인까지 표시할지.
            var cross = new List<MinuteSignal>();
            var pairAsk = MessageBox.Show(
                "반대 짝(레버리지↔인버스) 분봉도 함께 불러와 🔁 전환 확인(교차)까지 표시할까요?\n같은 날짜의 반대 종목 CSV를 이어서 선택합니다.",
                "🔁 전환 확인 포함", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (pairAsk == MessageBoxResult.Yes)
            {
                var openP = new OpenFileDialog
                {
                    Title = "반대 짝 분봉 CSV 선택 (같은 날짜)",
                    Filter = "CSV (쉼표 구분)|*.csv|모든 파일|*.*",
                    InitialDirectory = Path.GetDirectoryName(open.FileName)
                };
                if (openP.ShowDialog(this) == true)
                {
                    var barsB = MinuteCsvIo.Parse(openP.FileName);
                    var mb = MinuteCsvIo.ParseStem(openP.FileName);
                    string codeB = mb?.Code ?? "PAIR";
                    string nameB = mb?.Name ?? "반대 짝";
                    var eng = new MinuteSignalEngine(_config, _registry, new SlackNotifier(_config));
                    var sigA = eng.Backtest(bars, ResolveItem(code, name));
                    var sigB = eng.Backtest(barsB, ResolveItem(codeB, nameB));
                    // 주 종목(code) 명의 교차만 이 차트에 표시.
                    cross = MinuteSignalEngine.DetectCrossTurns(sigA, bars, code, name, sigB, barsB, codeB, nameB)
                        .Where(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            // 시그널 계산(1분 TF) + 교차 병합.
            var engM = new MinuteSignalEngine(_config, _registry, new SlackNotifier(_config));
            _csvSignals = engM.Backtest(bars, ResolveItem(code, name))
                .Where(s => s.Timeframe == 1).Concat(cross)
                .OrderBy(s => s.Time).ToList();

            _csvMode = true;
            _csvTitle = $"{name} ({code}) · {bars[0].Date:MM-dd} · CSV {bars.Count}봉";
            SetOnlineControlsEnabled(false);
            _timer.Stop(); AutoCheck.IsChecked = false;
            _set = new IndicatorSet(bars);
            Chart.Show(_set, CurrentOptions());
            ApplySignals();
            TitleText.Text = _csvTitle;
            CsvInfoText.Text = $"📂 {stem}" + (cross.Count > 0 ? $" · 🔁 {cross.Count}" : "");
            StatusText.Text = $"CSV 불러옴 · {bars.Count}봉 · 시그널 {_csvSignals.Count}건" +
                "  (온라인 조회로 돌아가려면 새로고침)";
        }
        catch (Exception ex)
        {
            MessageBox.Show("불러오기 실패: " + ex.Message, "분봉 CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Models.WatchItem ResolveItem(string code, string name)
        => _config.Watchlist.FirstOrDefault(w => string.Equals(w.Symbol, code, StringComparison.OrdinalIgnoreCase))
           ?? new Models.WatchItem { Symbol = code, Name = name };

    /// <summary>온라인 조회용 컨트롤(봉·소스·자동갱신)을 CSV 모드에서 비활성/복원.</summary>
    private void SetOnlineControlsEnabled(bool on)
    {
        IntervalCombo.IsEnabled = on;
        SourceCombo.IsEnabled = on;
        AutoCheck.IsEnabled = on;
        PeriodCombo.IsEnabled = on;
    }

    private void Signal_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready && _csvMode) ApplySignals();
    }

    private void Signal_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && _csvMode) ApplySignals();
    }

    /// <summary>시그널 체크·필터에 따라 차트 마커 갱신.</summary>
    private void ApplySignals()
    {
        if (SignalCheck.IsChecked != true) { Chart.SetSignals(Array.Empty<ChartSignal>()); return; }
        int f = SignalFilterCombo.SelectedIndex;
        bool Pass(MinuteSignalKind k) => f switch
        {
            1 => k is MinuteSignalKind.HoldConfirm or MinuteSignalKind.StrongGoldenCross or MinuteSignalKind.GoldenCross,
            2 => k is MinuteSignalKind.Rebound or MinuteSignalKind.FollowThrough,
            3 => k is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross or MinuteSignalKind.CrossTurn,
            4 => k == MinuteSignalKind.WeakGoldenCross,
            _ => k != MinuteSignalKind.MorningBrief,
        };
        var markers = _csvSignals.Where(s => Pass(s.Kind))
            .Select(s => new ChartSignal(s.Time, s.Price, IconOf(s.Kind), s.IsBearish));
        Chart.SetSignals(markers);
    }

    private static string IconOf(MinuteSignalKind k) => k switch
    {
        MinuteSignalKind.HoldConfirm => "🚀",
        MinuteSignalKind.StrongGoldenCross => "🔥",
        MinuteSignalKind.GoldenCross => "✅",
        MinuteSignalKind.Rebound => "📈",
        MinuteSignalKind.FollowThrough => "↗",
        MinuteSignalKind.CrossTurn => "🔁",
        MinuteSignalKind.TopWarn => "📉",
        MinuteSignalKind.DeadCross => "🔻",
        MinuteSignalKind.WeakGoldenCross => "⚠",
        _ => "☀",
    };

    private async Task LoadAsync()
    {
        if (!_ready || _loading) return;
        _loading = true;
        var iv = SelectedInterval();
        var src = SelectedSource();
        StatusText.Text = $"{ChartDataService.Label(iv)} 불러오는 중…";
        try
        {
            var candles = await _registry.Chart.FetchAsync(_code, iv, src);
            _set = new IndicatorSet(candles);
            Chart.Show(_set, CurrentOptions());
            StatusText.Text = $"{SelectedSource()} · {ChartDataService.Label(iv)} · {candles.Count}봉 · {DateTime.Now:HH:mm:ss} 갱신";
        }
        catch (Exception ex)
        {
            _set = null;
            Chart.Clear();
            StatusText.Text = "⚠ " + ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    // 봉종류·소스 변경 → 재조회(CSV 모드에선 콤보 비활성이라 발생 안 함)
    private async void Reload_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_csvMode) await LoadAsync();
    }

    // 지표 토글 → 데이터 재조회 없이 다시 그림(CSV 모드는 시그널 마커도 함께 유지)
    private void Indicator_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || _set == null) return;
        Chart.Show(_set, CurrentOptions());
        if (_csvMode) ApplySignals();
    }

    // 새로고침: CSV 모드였다면 온라인 조회로 복귀.
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_csvMode)
        {
            _csvMode = false;
            _csvSignals = new();
            Chart.SetSignals(Array.Empty<ChartSignal>());
            SetOnlineControlsEnabled(true);
            CsvInfoText.Text = "";
            TitleText.Text = string.IsNullOrEmpty(_name) ? _code : $"{_name} ({_code})";
        }
        _ = LoadAsync();
    }

    private void Auto_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (AutoCheck.IsChecked == true) { ApplyTimerInterval(); _timer.Start(); }
        else _timer.Stop();
    }

    private void Period_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && _timer.IsEnabled) ApplyTimerInterval();
    }

    private void ApplyTimerInterval()
    {
        int sec = PeriodCombo.SelectedItem is PeriodItem p ? p.Seconds : 10;
        _timer.Interval = TimeSpan.FromSeconds(sec);
    }
}
