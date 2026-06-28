using System.Text;
using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class LadderWindow : Window
{
    private static readonly string[] Hoga =
        { "1호가 (정상)", "2호가 (조정)", "3호가 (중조정)", "4호가 (폭락)" };

    private readonly string _copyText;

    public LadderWindow(LadderResult r)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        string title = string.IsNullOrEmpty(r.Name) ? r.Code : $"{r.Name} ({r.Code})";
        TitleText.Text = $"{title} — 매수/익절 래더";
        SubText.Text = $"최근 {r.TradingDays}거래일 기준 · 하방변동성 σ_down {r.SigmaDown}%";

        PrevLowText.Text = Won(r.PrevLow);
        PrevHighText.Text = Won(r.PrevHigh);
        PrevCloseText.Text = Won(r.PrevClose);
        GapText.Text = Won(r.GapCancelLine);

        for (int i = 0; i < 4; i++)
            AddBuyRow(i + 1, Hoga[i], r.BuyOffsets[i], r.BuyPrices[i]);

        AvgText.Text = Won(r.AvgPrice);
        TotalText.Text = Won(r.TotalAmount);
        StopText.Text = Won(r.StopPrice);
        LossText.Text = Won(r.StopLoss);

        for (int i = 0; i < r.SellTargets.Length; i++)
            AddSellRow(i + 1, r.SellTargets[i]);

        _copyText = BuildCopyText(r, title);
    }

    private void AddSellRow(int row, SellTarget t)
    {
        var muted = (System.Windows.Media.Brush)FindResource("FgMuted");
        var fg = (System.Windows.Media.Brush)FindResource("FgBrush");
        var bull = (System.Windows.Media.Brush)FindResource("BullBrush");

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

        SellGrid.Children.Add(namePanel);
        SellGrid.Children.Add(price);
        SellGrid.Children.Add(ret);
    }

    private static string SignedPct(decimal r) => (r >= 0 ? "+" : "") + r.ToString("P2");

    private void AddBuyRow(int row, string label, int off, decimal price)
    {
        var lbl = new TextBlock { Text = label, Style = (Style)FindResource("Lbl") };
        Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);

        var offT = new TextBlock
        {
            Text = Pct(off),
            Foreground = (System.Windows.Media.Brush)FindResource("FgMuted"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 3, 16, 3)
        };
        Grid.SetRow(offT, row); Grid.SetColumn(offT, 1);

        var priceT = new TextBlock { Text = Won(price), Style = (Style)FindResource("Val") };
        Grid.SetRow(priceT, row); Grid.SetColumn(priceT, 2);

        BuyGrid.Children.Add(lbl);
        BuyGrid.Children.Add(offT);
        BuyGrid.Children.Add(priceT);
    }

    private static string Won(decimal d) => d.ToString("N0");
    private static string Pct(int p) => $"{p}%";

    private static string BuildCopyText(LadderResult r, string title)
    {
        var sb = new StringBuilder();
        sb.Append(title).Append(" — 매수/익절 래더\n");
        sb.Append($"최근 {r.TradingDays}거래일 · σ_down {r.SigmaDown}%\n\n");
        sb.Append($"전일저가\t{Won(r.PrevLow)}\n");
        sb.Append($"전일고가\t{Won(r.PrevHigh)}\n");
        sb.Append($"전일종가(정규)\t{Won(r.PrevClose)}\n");
        sb.Append($"갭다운취소선\t{Won(r.GapCancelLine)}\n");
        for (int i = 0; i < 4; i++)
            sb.Append($"{Hoga[i]} {Pct(r.BuyOffsets[i])}\t{Won(r.BuyPrices[i])}\n");
        sb.Append($"평단\t{Won(r.AvgPrice)}\n");
        sb.Append($"전량금액\t{Won(r.TotalAmount)}\n");
        sb.Append($"손절가\t{Won(r.StopPrice)}\n");
        sb.Append($"손해\t{Won(r.StopLoss)}\n");
        sb.Append($"익절오프셋\t{Pct(r.SellOffset)}\n");
        sb.Append($"ATR\t{Won(r.Atr)}\n");
        foreach (var t in r.SellTargets)
            sb.Append($"익절·{t.Name}\t{Won(t.Price)}\t{SignedPct(t.ReturnPct)}\n");
        return sb.ToString();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_copyText); CopyBtn.Content = "✓ 복사됨"; }
        catch { /* 클립보드 일시 점유는 무시 */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
