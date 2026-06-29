using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

/// <summary>매매 기록(롯) 추가/수정 다이얼로그. 저장 시 전달받은 <see cref="Trade"/>를 직접 갱신한다.</summary>
public partial class TradeEditWindow : Window
{
    private readonly Trade _trade;
    private readonly PriceSourceRegistry _registry;

    public TradeEditWindow(Trade trade, PriceSourceRegistry registry)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _trade = trade;
        _registry = registry;

        HeaderText.Text = _trade.Side == TradeSide.Buy ? "매수 기록" : "매도 기록";
        CodeBox.Text = trade.Code;
        NameBox.Text = trade.Name;
        SideCombo.SelectedIndex = trade.Side == TradeSide.Buy ? 0 : 1;
        DateBox.Text = trade.Date.ToString("yyyy-MM-dd");
        PriceBox.Text = trade.Price > 0 ? trade.Price.ToString("0.##") : string.Empty;
        QtyBox.Text = trade.Quantity > 0 ? trade.Quantity.ToString() : string.Empty;
        NoteBox.Text = trade.Note;
    }

    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        if (string.IsNullOrEmpty(code)) return;
        NameBox.Text = "조회 중…";
        try
        {
            var name = await _registry.LookupNameAsync(code);
            NameBox.Text = name ?? "";
        }
        catch { NameBox.Text = ""; }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        if (string.IsNullOrEmpty(code)) { Error("종목코드를 입력하세요."); return; }
        if (!DateOnly.TryParse(DateBox.Text.Trim(), CultureInfo.InvariantCulture, out var date))
        { Error("날짜 형식은 yyyy-MM-dd 입니다."); return; }
        if (!decimal.TryParse(PriceBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
        { Error("체결가를 올바르게 입력하세요."); return; }
        if (!int.TryParse(QtyBox.Text.Trim(), out var qty) || qty <= 0)
        { Error("수량을 올바르게 입력하세요."); return; }

        _trade.Code = code;
        _trade.Name = NameBox.Text.Trim() is "조회 중…" ? "" : NameBox.Text.Trim();
        _trade.Side = SideCombo.SelectedIndex == 1 ? TradeSide.Sell : TradeSide.Buy;
        _trade.Date = date;
        _trade.Price = price;
        _trade.Quantity = qty;
        _trade.Note = NoteBox.Text.Trim();

        DialogResult = true;
    }

    private void Error(string msg) => ErrorText.Text = "⚠ " + msg;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
