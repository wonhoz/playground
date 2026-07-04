using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

public partial class LadderWindow : Window
{
    private static readonly string[] Hoga =
        { "1호가 (정상)", "2호가 (조정)", "3호가 (중조정)", "4호가 (폭락)" };

    private readonly StockSeries _series;
    private readonly PriceSourceRegistry _registry;
    private readonly AppConfig _config;
    private Holding? _holding;
    private string _name;          // 표시용 종목명(비었거나 코드와 같으면 조회로 보완)
    private string _copyText = string.Empty;
    private bool _ready;
    private bool _suppress;   // 추세 적용으로 슬라이더 값을 코드가 바꿀 때 재계산 루프 방지

    public LadderWindow(StockSeries series, PriceSourceRegistry registry, AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _series = series;
        _registry = registry;
        _config = config;

        // 포트폴리오에 보유 중이면 평단을 래더 계산에 반영.
        _holding = PortfolioStore.HoldingOf(PortfolioStore.Load(config), series.Code);

        _name = series.Name;
        UpdateTitle();
        // 이름이 없거나 코드와 같으면 종목명 조회로 보완.
        if (string.IsNullOrEmpty(_name) || _name == series.Code) _ = ResolveNameAsync();

        // 저장값 복원(_ready=false 동안이라 ValueChanged는 무시됨).
        AggrSlider.Value = Math.Clamp(config.LadderAggressiveness, 0, 1) * 100;
        SellSlider.Value = Math.Clamp(config.LadderSellStrength, 0, 1) * 100;
        TrendCheck.IsChecked = config.LadderUseTrend;

        _ready = true;
        Recompute();
    }

    private void UpdateTitle()
    {
        string title = string.IsNullOrEmpty(_name) || _name == _series.Code
            ? _series.Code
            : $"{_name} ({_series.Code})";
        TitleText.Text = $"{title} — 매수/익절 래더";
    }

    private async System.Threading.Tasks.Task ResolveNameAsync()
    {
        try
        {
            var n = await _registry.LookupNameAsync(_series.Code);
            if (!string.IsNullOrEmpty(n)) { _name = n!; UpdateTitle(); }
        }
        catch { /* 이름 조회 실패는 무시 */ }
    }

    // ───────────────────────── 이벤트 ─────────────────────────

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready && !_suppress) Recompute();
    }

    private void Trend_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready) Recompute();
    }

    // ───────────────────────── 재계산/렌더 ─────────────────────────

    private void Recompute()
    {
        bool useTrend = TrendCheck.IsChecked == true;
        var p = new LadderParams(AggrSlider.Value / 100.0, SellSlider.Value / 100.0, useTrend,
            _holding?.Quantity ?? 0, _holding?.AvgPrice ?? 0m);

        LadderResult r;
        try { r = LadderCalculator.Calculate(_series, p); }
        catch (InvalidOperationException ex) { SubText.Text = "⚠ " + ex.Message; return; }

        // 추세 적용 시: 자동 산정된 공격성으로 슬라이더 위치 갱신(+비활성화).
        if (useTrend)
        {
            _suppress = true;
            AggrSlider.Value = Math.Round(r.BuyAggressiveness * 100);
            SellSlider.Value = Math.Round(r.SellStrength * 100);
            _suppress = false;
        }
        AggrSlider.IsEnabled = SellSlider.IsEnabled = !useTrend;

        Render(r);
        SaveState();
    }

    private void Render(LadderResult r)
    {
        SubText.Text = r.HoldingQty > 0
            ? $"최근 {r.TradingDays}거래일 · σ_down {r.SigmaDown}% · 신규 평단 {Won(r.AvgPrice)}  |  보유 {r.HoldingQty}주 @ {Won(r.HoldingAvg)} → 합산 평단 {Won(r.CombinedAvg)} (손절·익절 기준)"
            : $"최근 {r.TradingDays}거래일 · 하방변동성 σ_down {r.SigmaDown}% · 평단 {Won(r.AvgPrice)}";

        AggrValueText.Text = $"{AggrLabel(r.BuyAggressiveness)} · {r.BuyAggressiveness * 100:0}%";
        SellValueText.Text = $"{SellLabel(r.SellStrength)} · {r.SellStrength * 100:0}%";
        TrendText.Text = r.TrendApplied
            ? $"추세: {r.TrendLabel} → 공격성 자동 {r.BuyAggressiveness * 100:0}%"
            : $"추세: {r.TrendLabel} (자동 반영 끔)";

        PrevLowText.Text = Won(r.PrevLow);
        PrevHighText.Text = Won(r.PrevHigh);
        PrevCloseText.Text = Won(r.PrevClose);
        GapText.Text = Won(r.GapCancelLine);

        ClearFromRow(BuyGrid, 1);
        for (int i = 0; i < 4; i++)
            AddBuyRow(i + 1, Hoga[i], r.BuyOffsets[i], r.BuyPrices[i], r.FillProbs[i]);

        AvgText.Text = Won(r.AvgPrice);
        TotalText.Text = Won(r.TotalAmount);
        StopText.Text = Won(r.StopPrice);
        LossText.Text = Won(r.StopLoss);

        ClearFromRow(SellGrid, 1);
        for (int i = 0; i < r.SellTargets.Length; i++)
            AddSellRow(i + 1, r.SellTargets[i]);

        _copyText = BuildCopyText(r);
    }

    private static void ClearFromRow(Grid g, int fromRow)
    {
        for (int i = g.Children.Count - 1; i >= 0; i--)
            if (Grid.GetRow(g.Children[i]) >= fromRow) g.Children.RemoveAt(i);
    }

    private static string AggrLabel(double a) => a < 0.34 ? "보수" : a < 0.67 ? "중도" : "공격";
    private static string SellLabel(double a) => a < 0.34 ? "보수" : a < 0.67 ? "중도" : "공격";

    private void AddBuyRow(int row, string label, int off, decimal price, double fill)
    {
        var muted = (Brush)FindResource("FgMuted");

        var lbl = new TextBlock { Text = label, Style = (Style)FindResource("Lbl") };
        Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);

        var offT = new TextBlock
        {
            Text = Pct(off), Foreground = muted,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 3, 16, 3)
        };
        Grid.SetRow(offT, row); Grid.SetColumn(offT, 1);

        var priceT = new TextBlock { Text = Won(price), Style = (Style)FindResource("Val") };
        Grid.SetRow(priceT, row); Grid.SetColumn(priceT, 2);

        var fillT = new TextBlock
        {
            Text = fill.ToString("P0"), Foreground = ProbBrush(fill),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 3, 0, 3)
        };
        Grid.SetRow(fillT, row); Grid.SetColumn(fillT, 3);

        BuyGrid.Children.Add(lbl);
        BuyGrid.Children.Add(offT);
        BuyGrid.Children.Add(priceT);
        BuyGrid.Children.Add(fillT);

        // 행 전체를 덮는 투명 히트영역(맨 위 z-order) — 더블클릭 시 매수 기록 추가.
        var hit = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Tag = price,
            ToolTip = "더블클릭 → 내 자산 매수 기록에 추가 (1주)"
        };
        Grid.SetRow(hit, row); Grid.SetColumn(hit, 0); Grid.SetColumnSpan(hit, 4);
        hit.MouseLeftButtonDown += BuyRow_MouseDown;
        BuyGrid.Children.Add(hit);
    }

    private void BuyRow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.Tag is decimal price)
            AddBuyToPortfolio(price);
    }

    /// <summary>선택한 매수가를 내 자산 매매 기록에 매수 1주로 추가하고, 보유 반영 후 재계산한다.</summary>
    private async void AddBuyToPortfolio(decimal price)
    {
        // 표시용 이름(_name) 우선 사용. 비었거나 코드와 같으면 조회로 보완.
        string name = _name;
        if (string.IsNullOrEmpty(name) || name == _series.Code)
        {
            try { name = await _registry.LookupNameAsync(_series.Code) ?? ""; } catch { name = ""; }
            if (!string.IsNullOrEmpty(name)) { _name = name; UpdateTitle(); }
        }
        string title = string.IsNullOrEmpty(name) ? _series.Code : $"{name} ({_series.Code})";
        if (MessageBox.Show($"{title}\n{Won(price)}원 × 1주를 매수 기록에 추가할까요?",
                "매수 기록 추가", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var pf = PortfolioStore.Load(_config);
        pf.Trades.Add(new Trade
        {
            Code = _series.Code,
            Name = name,
            Side = TradeSide.Buy,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Price = price,
            Quantity = 1,
            Note = "래더 매수"
        });
        PortfolioStore.Save(_config, pf);

        // 보유 평단 갱신 → 합산 평단·손절·익절 재계산.
        _holding = PortfolioStore.HoldingOf(pf, _series.Code);
        Recompute();
        SubText.Text = $"✓ 매수 기록 추가: {title} · {Won(price)}원 ×1주 ({DateTime.Now:HH:mm})";
    }

    private void AddSellRow(int row, SellTarget t)
    {
        var muted = (Brush)FindResource("FgMuted");
        var fg = (Brush)FindResource("FgBrush");
        var bull = (Brush)FindResource("BullBrush");

        var namePanel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        namePanel.Children.Add(new TextBlock { Text = t.Name, Foreground = fg, FontWeight = FontWeights.SemiBold });
        namePanel.Children.Add(new TextBlock { Text = t.Note, Foreground = muted, FontSize = 10.5 });
        Grid.SetRow(namePanel, row); Grid.SetColumn(namePanel, 0);

        var price = new TextBlock
        {
            Text = Won(t.Price), Foreground = fg, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 3, 16, 3)
        };
        Grid.SetRow(price, row); Grid.SetColumn(price, 1);

        var ret = new TextBlock
        {
            Text = SignedPct(t.ReturnPct), Foreground = bull, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 3)
        };
        Grid.SetRow(ret, row); Grid.SetColumn(ret, 2);

        var reach = new TextBlock
        {
            Text = ((double)t.ReachProb).ToString("P0"), Foreground = ProbBrush((double)t.ReachProb),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 3, 0, 3)
        };
        Grid.SetRow(reach, row); Grid.SetColumn(reach, 3);

        SellGrid.Children.Add(namePanel);
        SellGrid.Children.Add(price);
        SellGrid.Children.Add(ret);
        SellGrid.Children.Add(reach);
    }

    /// <summary>확률에 따라 색을 달리해 한눈에 가늠(낮음=보조색, 높음=강조색).</summary>
    private Brush ProbBrush(double p) => p >= 0.5
        ? (Brush)FindResource("AccentBrush")
        : (Brush)FindResource("FgMuted");

    private void SaveState()
    {
        _config.LadderAggressiveness = AggrSlider.Value / 100.0;
        _config.LadderSellStrength = SellSlider.Value / 100.0;
        _config.LadderUseTrend = TrendCheck.IsChecked == true;
        _config.Save();
    }

    // ───────────────────────── 포맷/복사 ─────────────────────────

    private static string SignedPct(decimal r) => (r >= 0 ? "+" : "") + r.ToString("P2");
    private static string Won(decimal d) => d.ToString("N0");
    private static string Pct(int p) => $"{p}%";

    private string BuildCopyText(LadderResult r)
    {
        string title = string.IsNullOrEmpty(r.Name) ? r.Code : $"{r.Name} ({r.Code})";
        var sb = new StringBuilder();
        sb.Append(title).Append(" — 매수/익절 래더\n");
        sb.Append($"최근 {r.TradingDays}거래일 · σ_down {r.SigmaDown}%\n");
        sb.Append($"공격성 매수 {r.BuyAggressiveness * 100:0}% / 익절 {r.SellStrength * 100:0}%");
        sb.Append(r.TrendApplied ? $" (추세 자동: {r.TrendLabel})\n" : "\n");
        if (r.HoldingQty > 0)
            sb.Append($"보유 {r.HoldingQty}주 @ {Won(r.HoldingAvg)} → 합산평단 {Won(r.CombinedAvg)}\n");
        sb.Append('\n');
        sb.Append($"전일저가\t{Won(r.PrevLow)}\n");
        sb.Append($"전일고가\t{Won(r.PrevHigh)}\n");
        sb.Append($"전일종가(정규)\t{Won(r.PrevClose)}\n");
        sb.Append($"갭다운취소선\t{Won(r.GapCancelLine)}\n");
        for (int i = 0; i < 4; i++)
            sb.Append($"{Hoga[i]} {Pct(r.BuyOffsets[i])}\t{Won(r.BuyPrices[i])}\t체결 {r.FillProbs[i]:P0}\n");
        sb.Append($"평단\t{Won(r.AvgPrice)}\n");
        sb.Append($"전량금액\t{Won(r.TotalAmount)}\n");
        sb.Append($"손절가\t{Won(r.StopPrice)}\n");
        sb.Append($"손해\t{Won(r.StopLoss)}\n");
        sb.Append($"익절오프셋\t{Pct(r.SellOffset)}\n");
        sb.Append($"ATR\t{Won(r.Atr)}\n");
        foreach (var t in r.SellTargets)
            sb.Append($"익절·{t.Name}\t{Won(t.Price)}\t{SignedPct(t.ReturnPct)}\t도달 {(double)t.ReachProb:P0}\n");
        return sb.ToString();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_copyText); CopyBtn.Content = "✓ 복사됨"; }
        catch { /* 클립보드 일시 점유는 무시 */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
