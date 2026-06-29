using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            .Select(h => new HoldingRow(h, _current.GetValueOrDefault(h.Code)))
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

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        var codes = PortfolioStore.Holdings(_pf).Where(h => h.Quantity > 0).Select(h => h.Code).ToList();
        if (codes.Count == 0) return;
        RefreshBtn.IsEnabled = false;
        RefreshBtn.Content = "갱신 중…";
        try
        {
            var tasks = codes.Select(async c => (c, price: await _registry.CurrentCloseAsync(c))).ToList();
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

    // ───────────────────────── 매매 추가/수정/삭제 ─────────────────────────

    private void AddBuy_Click(object sender, RoutedEventArgs e) => AddTrade(TradeSide.Buy);
    private void AddSell_Click(object sender, RoutedEventArgs e) => AddTrade(TradeSide.Sell);

    private void AddTrade(TradeSide side)
    {
        // 선택된 보유 종목 우선, 없으면 메인에서 넘어온 종목으로 프리필.
        string code = _prefCode, name = _prefName;
        if (HoldingsGrid.SelectedItem is HoldingRow hr) { code = hr.Code; name = hr.Name; }

        var t = new Trade { Code = code, Name = name, Side = side };
        var dlg = new TradeEditWindow(t, _registry) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _pf.Trades.Add(t);
            Persist();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (TradesGrid.SelectedItem is not TradeRow row) return;
        var dlg = new TradeEditWindow(row.Source, _registry) { Owner = this };
        if (dlg.ShowDialog() == true) Persist();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (TradesGrid.SelectedItem is not TradeRow row) return;
        if (MessageBox.Show($"이 매매 기록을 삭제할까요?\n{row.DateText} {row.Display} {row.SideText} {row.Price:N0}×{row.Quantity}",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _pf.Trades.Remove(row.Source);
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

    public sealed class HoldingRow(Holding h, decimal? current)
    {
        public string Code => h.Code;
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
