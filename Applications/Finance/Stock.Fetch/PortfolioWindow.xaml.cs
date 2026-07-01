using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class PortfolioWindow : Window
{
    private readonly AppConfig _config;
    private readonly PriceSourceRegistry _registry;
    private readonly string _prefCode;
    private readonly string _prefName;
    private Portfolio _pf;
    private readonly Dictionary<string, decimal?> _current = new();
    private readonly DispatcherTimer _autoTimer = new();

    public PortfolioWindow(AppConfig config, PriceSourceRegistry registry, string prefillCode, string prefillName)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _config = config;
        _registry = registry;
        _prefCode = prefillCode;
        _prefName = prefillName;

        _pf = PortfolioStore.Load(config);
        PathText.Text = PortfolioStore.ResolvePath(config);
        RenderAll();

        _autoTimer.Tick += async (_, _) => await RefreshPricesAsync();
        AutoRefreshCheck.IsChecked = _config.PortfolioAutoRefresh;
        if (_config.PortfolioAutoRefresh) StartAutoRefresh();
    }

    // ───────────────────────── 렌더 ─────────────────────────

    private void RenderAll()
    {
        RenderHoldings();
        RenderTrades();
    }

    private void RenderHoldings()
    {
        var rows = PortfolioStore.Holdings(_pf)
            .Select(h => new HoldingRow(h, _current.GetValueOrDefault(h.Code), _config))
            .ToList();
        HoldingsGrid.ItemsSource = rows;
        UpdateSummary(rows);
    }

    private void RenderTrades()
    {
        TradesGrid.ItemsSource = _pf.Trades
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Side == TradeSide.Buy ? 0 : 1)
            .Select(t => new TradeRow(t))
            .ToList();
    }

    private void UpdateSummary(List<HoldingRow> rows)
    {
        var held = rows.Where(r => r.Quantity > 0).ToList();
        decimal invested = held.Sum(r => r.Invested);
        decimal realized = rows.Sum(r => r.Realized);
        bool hasCurrent = held.Any(r => r.Current.HasValue);
        decimal eval = held.Where(r => r.EvalAmount.HasValue).Sum(r => r.EvalAmount!.Value);
        decimal evalPl = held.Where(r => r.EvalPL.HasValue).Sum(r => r.EvalPL!.Value);

        string s = $"보유 {held.Count}종목 · 매입 {invested:N0}원";
        if (hasCurrent)
        {
            double pct = invested > 0 ? (double)(evalPl / invested) : 0;
            s += $" · 평가 {eval:N0}원 · 평가손익 {evalPl:+#,0;-#,0;0}원 ({pct:+0.00%;-0.00%;0.00%})";
        }
        s += $" · 누적 실현손익 {realized:+#,0;-#,0;0}원";
        SummaryText.Text = s;
    }

    // ───────────────────────── 현재가 갱신 ─────────────────────────

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshPricesAsync();

    /// <summary>보유 종목 현재가를 일괄 조회해 갱신(수동 버튼·자동 타이머 공용).</summary>
    private async Task RefreshPricesAsync()
    {
        var codes = PortfolioStore.Holdings(_pf).Where(h => h.Quantity > 0).Select(h => h.Code).ToList();
        if (codes.Count == 0) return;
        RefreshBtn.IsEnabled = false;
        RefreshBtn.Content = "갱신 중…";
        try
        {
            // KIS 키가 있으면 KIS 실시간(inquire-price·통합 NXT), 없으면 네이버 최근 종가로 폴백.
            var tasks = codes.Select(async c => (c, price: (await _registry.QuoteAsync(c))?.Price)).ToList();
            foreach (var (c, price) in await Task.WhenAll(tasks))
                _current[c] = price;
            RenderHoldings();
        }
        finally
        {
            RefreshBtn.IsEnabled = true;
            RefreshBtn.Content = "🔄 현재가 갱신";
        }
    }

    private void AutoRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool on = AutoRefreshCheck.IsChecked == true;
        _config.PortfolioAutoRefresh = on;
        _config.Save();
        if (on) StartAutoRefresh();
        else _autoTimer.Stop();
    }

    private async void StartAutoRefresh()
    {
        _autoTimer.Interval = TimeSpan.FromSeconds(Math.Max(10, _config.PortfolioRefreshSeconds));
        _autoTimer.Start();
        await RefreshPricesAsync(); // 켜는 즉시 1회 갱신
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoTimer.Stop();
        base.OnClosed(e);
    }

    // ───────────────────────── 매매 추가/수정/삭제 ─────────────────────────

    private void AddBuy_Click(object sender, RoutedEventArgs e) => AddTrade(TradeSide.Buy);
    private void AddSell_Click(object sender, RoutedEventArgs e) => AddTrade(TradeSide.Sell);

    private void AddTrade(TradeSide side)
    {
        // 선택된 보유 종목 우선, 없으면 메인에서 넘어온 종목으로 프리필.
        string code = _prefCode, name = _prefName;
        if (HoldingsGrid.SelectedItem is HoldingRow hr) { code = hr.Code; name = hr.Name; }

        var t = new Trade { Code = code, Name = name, Side = side };
        var dlg = new TradeEditWindow(t, _registry, _config) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _pf.Trades.Add(t);
            Persist();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (TradesGrid.SelectedItem is not TradeRow row) return;
        var dlg = new TradeEditWindow(row.Source, _registry, _config) { Owner = this };
        if (dlg.ShowDialog() == true) Persist();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var rows = TradesGrid.SelectedItems.Cast<TradeRow>().ToList();
        if (rows.Count == 0) return;

        string prompt = rows.Count == 1
            ? $"이 매매 기록을 삭제할까요?\n{rows[0].DateText} {rows[0].Display} {rows[0].SideText} {rows[0].Price:N0}×{rows[0].Quantity}"
            : $"선택한 매매 기록 {rows.Count}건을 삭제할까요?";
        if (MessageBox.Show(prompt, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        foreach (var row in rows) _pf.Trades.Remove(row.Source);
        Persist();
    }

    private void Persist()
    {
        PortfolioStore.Save(_config, _pf);
        RenderAll();
    }

    // ───────────────────────── 기타 ─────────────────────────

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = PortfolioStore.ResolvePath(_config);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
        }
        catch { /* 탐색기 실행 실패는 무시 */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ───────────────────────── 뷰 모델 ─────────────────────────

    public sealed class HoldingRow(Holding h, decimal? current, AppConfig cfg)
    {
        public string Code => h.Code;

        /// <summary>매수/익절 래더·갭다운 알림 옵트인(config 저장). 체크박스 컬럼과 양방향 바인딩.</summary>
        public bool LadderAlert
        {
            get => cfg.LadderHoldingCodes.Contains(h.Code);
            set
            {
                if (value) { if (!cfg.LadderHoldingCodes.Contains(h.Code)) cfg.LadderHoldingCodes.Add(h.Code); }
                else cfg.LadderHoldingCodes.Remove(h.Code);
                cfg.Save();
            }
        }
        public string Name => h.Name;
        public string Display => string.IsNullOrEmpty(h.Name) ? h.Code : $"{h.Name} ({h.Code})";
        public int Quantity => h.Quantity;
        public decimal AvgPrice => h.AvgPrice;
        public decimal Invested => h.Invested;
        public decimal Realized => h.Realized;
        public decimal? Current { get; } = current;
        public decimal? EvalAmount => Current.HasValue ? Current.Value * h.Quantity : null;
        public decimal? EvalPL => Current.HasValue ? (Current.Value - h.AvgPrice) * h.Quantity : null;
        public double? ReturnPct => Current.HasValue && h.AvgPrice > 0 ? (double)(Current.Value / h.AvgPrice - 1) : null;
    }

    public sealed class TradeRow(Trade t)
    {
        public Trade Source { get; } = t;
        public string DateText => Source.Date.ToString("yyyy-MM-dd");
        public string Display => string.IsNullOrEmpty(Source.Name) ? Source.Code : $"{Source.Name} ({Source.Code})";
        public string SideText => Source.Side == TradeSide.Buy ? "매수" : "매도";
        public int SideSign => Source.Side == TradeSide.Buy ? 1 : -1;
        public decimal Price => Source.Price;
        public int Quantity => Source.Quantity;
        public decimal Amount => Source.Amount;
        public string Note => Source.Note;
    }
}
