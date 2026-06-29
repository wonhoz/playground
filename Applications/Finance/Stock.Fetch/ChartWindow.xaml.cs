using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.Fetch.Indicators;
using Stock.Fetch.Services;
using Stock.Fetch.Views.Controls;

namespace Stock.Fetch;

public partial class ChartWindow : Window
{
    private readonly PriceSourceRegistry _registry;
    private readonly string _code;
    private readonly string _name;
    private readonly DispatcherTimer _timer = new();
    private IndicatorSet? _set;
    private bool _ready;
    private bool _loading;

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

    public ChartWindow(string code, string name, PriceSourceRegistry registry)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _registry = registry;
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
        IntervalCombo.SelectedIndex = 5; // 일봉

        SourceCombo.ItemsSource = new[]
        {
            new SourceItem(ChartSourceKind.Yahoo, "Yahoo (분/일/주/월)"),
            new SourceItem(ChartSourceKind.Kis, "KIS (일/주/월)"),
        };
        SourceCombo.SelectedIndex = 0;

        PeriodCombo.ItemsSource = new[]
        {
            new PeriodItem(5, "5초"), new PeriodItem(10, "10초"),
            new PeriodItem(30, "30초"), new PeriodItem(60, "1분"),
        };
        PeriodCombo.SelectedIndex = 1; // 10초

        _timer.Tick += async (_, _) => await LoadAsync();

        _ready = true;
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) => _timer.Stop();
    }

    private BarInterval SelectedInterval() => ((IntervalItem)IntervalCombo.SelectedItem).Iv;
    private ChartSourceKind SelectedSource() => ((SourceItem)SourceCombo.SelectedItem).Kind;

    private ChartOptions CurrentOptions()
    {
        var iv = SelectedInterval();
        string fmt = ChartDataService.IsIntraday(iv)
            ? (iv is BarInterval.Min60 ? "MM-dd HH:mm" : "HH:mm")
            : iv == BarInterval.Month ? "yy-MM" : "yy-MM-dd";
        return new ChartOptions(
            BollCheck.IsChecked == true, MaCheck.IsChecked == true,
            RsiCheck.IsChecked == true, VolCheck.IsChecked == true, fmt);
    }

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

    // 봉종류·소스 변경 → 재조회
    private async void Reload_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) await LoadAsync();
    }

    // 지표 토글 → 데이터 재조회 없이 다시 그림
    private void Indicator_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready && _set != null) Chart.Show(_set, CurrentOptions());
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();

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
