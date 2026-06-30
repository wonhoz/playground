using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

/// <summary>관심 종목 추가/수정 다이얼로그. 저장 시 전달받은 <see cref="WatchItem"/>를 직접 갱신한다.</summary>
public partial class WatchEditWindow : Window
{
    private readonly WatchItem _item;
    private readonly PriceSourceRegistry _registry;

    private sealed record SourceOption(string Label, WatchSource Source)
    {
        public override string ToString() => Label;
    }

    public WatchEditWindow(WatchItem item, PriceSourceRegistry registry, bool isNew)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _item = item;
        _registry = registry;

        HeaderText.Text = isNew ? "관심 종목 추가" : "관심 종목 수정";
        MarketCombo.SelectedIndex = item.Market == MarketKind.US ? 1 : 0; // SelectionChanged가 소스 콤보 채움
        SymbolBox.Text = item.Symbol;
        NameBox.Text = item.Name;
        StepBox.Text = item.StepPercent > 0 ? item.StepPercent.ToString("0.###", CultureInfo.InvariantCulture) : "";
        SelectSource(item.Source);
        SelectExchange(item.Exchange);
    }

    private MarketKind CurrentMarket => MarketCombo.SelectedIndex == 1 ? MarketKind.US : MarketKind.KR;

    private void Market_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) { PopulateSources(); return; }
        PopulateSources();
        UpdateLabels();
    }

    private void Source_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateLabels();

    /// <summary>시장에 맞는 소스 목록을 채운다. KR=네이버/KIS, US=Yahoo/KIS.</summary>
    private void PopulateSources()
    {
        var prev = (SourceCombo.SelectedItem as SourceOption)?.Source;
        SourceCombo.ItemsSource = CurrentMarket == MarketKind.US
            ? new[]
            {
                new SourceOption("Yahoo (지연)", WatchSource.Yahoo),
                new SourceOption("Finnhub (실시간·무료키)", WatchSource.Finnhub),
                new SourceOption("Alpaca (실시간 IEX·무료키)", WatchSource.Alpaca),
                new SourceOption("KIS (준실시간)", WatchSource.Kis),
            }
            : new[] { new SourceOption("네이버 (지연)", WatchSource.Naver), new SourceOption("KIS (실시간)", WatchSource.Kis) };
        SelectSource(prev ?? (CurrentMarket == MarketKind.US ? WatchSource.Yahoo : WatchSource.Naver));
    }

    private void SelectSource(WatchSource src)
    {
        if (SourceCombo.ItemsSource is not IEnumerable<SourceOption> opts) return;
        SourceCombo.SelectedItem = opts.FirstOrDefault(o => o.Source == src) ?? opts.First();
    }

    private void SelectExchange(string code)
    {
        foreach (ComboBoxItem it in ExchangeCombo.Items)
            if ((it.Tag as string) == (string.IsNullOrEmpty(code) ? "NAS" : code)) { ExchangeCombo.SelectedItem = it; return; }
        ExchangeCombo.SelectedIndex = 0;
    }

    private void UpdateLabels()
    {
        bool us = CurrentMarket == MarketKind.US;
        SymbolLbl.Text = us ? "티커" : "종목코드";
        LookupBtn.IsEnabled = !us;  // 이름 자동 조회는 국내만
        var src = (SourceCombo.SelectedItem as SourceOption)?.Source;
        ExchangeCombo.IsEnabled = us && src == WatchSource.Kis;  // 거래소 코드는 미국+KIS에서만 필요
    }

    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        string code = SymbolBox.Text.Trim();
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
        string symbol = SymbolBox.Text.Trim();
        var market = CurrentMarket;
        if (string.IsNullOrEmpty(symbol)) { Error("종목코드/티커를 입력하세요."); return; }
        if (market == MarketKind.KR && (symbol.Length is < 5 or > 6 || !symbol.All(char.IsDigit)))
        { Error("국내 종목코드는 6자리 숫자입니다(예: 005930)."); return; }
        if (SourceCombo.SelectedItem is not SourceOption opt) { Error("시세 소스를 선택하세요."); return; }

        _item.Market = market;
        _item.Symbol = market == MarketKind.US ? symbol.ToUpperInvariant() : symbol;
        _item.Name = NameBox.Text.Trim() is "조회 중…" ? "" : NameBox.Text.Trim();
        _item.Source = opt.Source;
        _item.Exchange = (ExchangeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "NAS";

        // 변화 단위(전용) — 비우면 0(전역 사용). 소수점 허용.
        string stepText = StepBox.Text.Trim();
        if (string.IsNullOrEmpty(stepText)) _item.StepPercent = 0;
        else if (double.TryParse(stepText, NumberStyles.Number, CultureInfo.InvariantCulture, out var step) && step > 0)
            _item.StepPercent = step;
        else { Error("변화 단위는 0보다 큰 숫자이거나 비워두세요(예: 1.5)."); return; }

        DialogResult = true;
    }

    private void Error(string msg) => ErrorText.Text = "⚠ " + msg;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
