using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class WatchlistWindow : Window
{
    private readonly AppConfig _config;
    private readonly PriceSourceRegistry _registry;
    private readonly WatchlistMonitor _monitor;
    private readonly Dictionary<string, Quote> _current = new();

    /// <summary>모니터링 토글 변경을 MainWindow에 알려 백그라운드 모니터 Start/Stop을 반영.</summary>
    public event Action<bool>? MonitorToggled;

    public WatchlistWindow(AppConfig config, PriceSourceRegistry registry, WatchlistMonitor monitor)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _config = config;
        _registry = registry;
        _monitor = monitor;

        MonitorCheck.IsChecked = _config.WatchEnabled;
        PollBox.Text = _config.WatchPollIntervalSeconds.ToString();
        StepBox.Text = _config.WatchStepPercent.ToString("0.#");
        WindowBox.Text = _config.WatchWindowMinutes.ToString("0.#");
        DigestBox.Text = _config.WatchDigestIntervalMinutes.ToString();

        _monitor.StatusChanged += OnStatus;
        StatusText.Text = _monitor.IsRunning ? "모니터링 실행 중" : "모니터링 꺼짐";
        Render();
    }

    private void OnStatus(string s) => Dispatcher.Invoke(() => StatusText.Text = s);

    // ───────────────────────── 렌더 ─────────────────────────
    private void Render()
        => WatchGrid.ItemsSource = _config.Watchlist
            .Select(w => new WatchRow(w, _current.GetValueOrDefault(w.Symbol)))
            .ToList();

    // ───────────────────────── 현재가 갱신 ─────────────────────────
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_config.Watchlist.Count == 0) return;
        RefreshBtn.IsEnabled = false;
        RefreshBtn.Content = "갱신 중…";
        try
        {
            foreach (var item in _config.Watchlist.ToList())
            {
                try { _current[item.Symbol] = await _registry.WatchQuoteAsync(item); }
                catch { /* 개별 종목 실패는 건너뜀 */ }
            }
            Render();
        }
        finally
        {
            RefreshBtn.IsEnabled = true;
            RefreshBtn.Content = "🔄 현재가 갱신";
        }
    }

    // ───────────────────────── 추가/수정/삭제 ─────────────────────────
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var item = new WatchItem();
        var dlg = new WatchEditWindow(item, _registry, isNew: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        if (_config.Watchlist.Any(w => w.Symbol.Equals(item.Symbol, StringComparison.OrdinalIgnoreCase) && w.Market == item.Market))
        {
            MessageBox.Show("이미 등록된 종목입니다.", "관심 종목", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _config.Watchlist.Add(item);
        _config.Save();
        Render();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (WatchGrid.SelectedItem is not WatchRow row) return;
        var dlg = new WatchEditWindow(row.Source, _registry, isNew: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _current.Remove(row.Source.Symbol); // 심볼/소스 변경 가능 → 캐시 무효화
        _config.Save();
        Render();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var rows = WatchGrid.SelectedItems.Cast<WatchRow>().ToList();
        if (rows.Count == 0) return;
        string prompt = rows.Count == 1
            ? $"'{rows[0].Display}'을(를) 관심 종목에서 삭제할까요?"
            : $"선택한 관심 종목 {rows.Count}건을 삭제할까요?";
        if (MessageBox.Show(prompt, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var row in rows)
        {
            _config.Watchlist.Remove(row.Source);
            _current.Remove(row.Source.Symbol);
        }
        _config.Save();
        Render();
    }

    // ───────────────────────── 모니터링 토글 ─────────────────────────
    private void Monitor_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SaveSettings();
        bool on = MonitorCheck.IsChecked == true;
        _config.WatchEnabled = on;
        _config.Save();
        MonitorToggled?.Invoke(on);
    }

    /// <summary>입력된 폴링·변화 단위·추세 기간·다이제스트 값을 파싱해 설정에 반영(저장은 호출 측).</summary>
    private void SaveSettings()
    {
        if (int.TryParse(PollBox.Text.Trim(), out var poll)) _config.WatchPollIntervalSeconds = Math.Max(10, poll);
        if (double.TryParse(StepBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var step) && step > 0)
            _config.WatchStepPercent = step;
        if (double.TryParse(WindowBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var win) && win >= 0)
            _config.WatchWindowMinutes = win;
        if (int.TryParse(DigestBox.Text.Trim(), out var dig)) _config.WatchDigestIntervalMinutes = Math.Max(0, dig);
        // UI를 정규화된 값으로 되돌림
        PollBox.Text = _config.WatchPollIntervalSeconds.ToString();
        StepBox.Text = _config.WatchStepPercent.ToString("0.#");
        WindowBox.Text = _config.WatchWindowMinutes.ToString("0.#");
        DigestBox.Text = _config.WatchDigestIntervalMinutes.ToString();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _monitor.StatusChanged -= OnStatus;
        SaveSettings();
        _config.Save();
        base.OnClosed(e);
    }

    // ───────────────────────── 뷰 모델 ─────────────────────────
    public sealed class WatchRow(WatchItem item, Quote? quote)
    {
        public WatchItem Source { get; } = item;
        public string Display => string.IsNullOrEmpty(Source.Name) ? Source.Symbol : $"{Source.Name} ({Source.Symbol})";
        public string MarketLabel => Source.MarketLabel;
        public string SourceLabel => Source.SourceLabel;
        public string PriceText => quote is null ? "—"
            : Source.Market == MarketKind.US ? $"${quote.Price:N2}" : $"{quote.Price:N0}";
        public double? RateValue => quote is null ? null : (double)quote.ChangeRate;
        public string RateText => quote is null ? "—" : $"{quote.ChangeRate:+0.00;-0.00;0.00}%";
    }
}
