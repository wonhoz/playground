using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>관심 종목 추가/수정 다이얼로그. 저장 시 전달받은 <see cref="WatchItem"/>를 직접 갱신한다.</summary>
public partial class WatchEditWindow : Window
{
    private readonly WatchItem _item;
    private readonly PriceSourceRegistry _registry;
    private readonly AppConfig _config;

    private sealed record SourceOption(string Label, WatchSource Source)
    {
        public override string ToString() => Label;
    }

    public WatchEditWindow(WatchItem item, PriceSourceRegistry registry, AppConfig config, bool isNew)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _item = item;
        _registry = registry;
        _config = config;

        HeaderText.Text = isNew ? "관심 종목 추가" : "관심 종목 수정";
        IndexCheck.IsChecked = item.IsIndex;
        MarketCombo.SelectedIndex = item.Market == MarketKind.US ? 1 : 0; // SelectionChanged가 소스 콤보 채움
        SymbolBox.Text = item.Symbol;
        NameBox.Text = item.Name;
        RulesBox.Text = TrendRule.ToText(item.Rules);
        AlertUpCheck.IsChecked = item.AlertUp;
        AlertDownCheck.IsChecked = item.AlertDown;
        LadderCheck.IsChecked = item.LadderAlert;
        BottomCheck.IsChecked = item.BottomAlert;
        TopCheck.IsChecked = item.TopAlert;
        ChannelBox.Text = item.SlackChannel;
        PairBox.Text = item.PairSymbol;
        OvRsiBox.Text = item.BottomRsiMax?.ToString("0.#") ?? "";
        OvVolBox.Text = item.BottomVolumeRatio?.ToString("0.##") ?? "";
        OvPbBox.Text = item.BottomMinPercentB?.ToString("0.##") ?? "";
        SelectSource(item.Source);
        SelectExchange(item.Exchange);
        ApplyIndexMode();
    }

    private bool IsIndex => IndexCheck.IsChecked == true;

    private void Index_Click(object sender, RoutedEventArgs e)
    {
        if (IsIndex) MarketCombo.SelectedIndex = 0; // 지수는 국내
        ApplyIndexMode();
    }

    /// <summary>지수 모드: 시장/소스 콤보 비활성(국내·KIS 고정), 라벨·힌트 갱신.</summary>
    private void ApplyIndexMode()
    {
        bool idx = IsIndex;
        MarketCombo.IsEnabled = !idx;
        SourceCombo.IsEnabled = !idx;
        if (idx)
        {
            SymbolLbl.Text = "지수코드";
            LookupBtn.IsEnabled = false;
            ExchangeCombo.IsEnabled = false;
            LadderCheck.IsEnabled = false;   // 지수는 래더 알림 미지원
            BottomCheck.IsEnabled = false;   // 지수는 반등 시그널 미지원
            TopCheck.IsEnabled = false;      // 지수는 고점 경고 미지원
        }
        else UpdateLabels();
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
        LadderCheck.IsEnabled = !us;   // 매수 래더·갭다운은 국내 종목만
        BottomCheck.IsEnabled = !us;   // 바닥 반등 시그널도 국내 종목만(KIS 분봉)
        TopCheck.IsEnabled = !us;      // 고점 경고 시그널도 국내 종목만(KIS 분봉)
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
        if (string.IsNullOrEmpty(symbol)) { Error("종목코드/티커를 입력하세요."); return; }

        _item.IsIndex = IsIndex;
        if (IsIndex)
        {
            if (!symbol.All(char.IsDigit) || symbol.Length is < 1 or > 6)
            { Error("지수 코드는 숫자입니다(코스피 0001·코스닥 1001·코스피200 2001)."); return; }
            _item.Market = MarketKind.KR;
            _item.Symbol = symbol;
        }
        else
        {
            var market = CurrentMarket;
            // 신형 영숫자 코드(0193T0 등) 허용 — KIS·네이버 모두 조회 가능 확인됨.
            if (market == MarketKind.KR && (symbol.Length is < 5 or > 6 || !symbol.All(char.IsLetterOrDigit)))
            { Error("국내 종목코드는 5~6자리 영숫자입니다(예: 005930, 0193T0)."); return; }
            _item.Market = market;
            _item.Symbol = symbol.ToUpperInvariant();
        }

        if (SourceCombo.SelectedItem is SourceOption opt) _item.Source = opt.Source;
        string name = NameBox.Text.Trim() is "조회 중…" ? "" : NameBox.Text.Trim();
        if (IsIndex && string.IsNullOrEmpty(name)) name = IndexName(symbol);   // 알려진 지수 기본 이름
        _item.Name = name;
        _item.Exchange = (ExchangeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "NAS";

        // 추세 조건(전용) — 비우면 전역 사용. 형식: "3:1, 5:2".
        string rulesText = RulesBox.Text.Trim();
        if (string.IsNullOrEmpty(rulesText)) _item.Rules = new();
        else
        {
            var rules = TrendRule.Parse(rulesText);
            if (rules.Count == 0) { Error("추세 조건 형식은 '기간:변화단위'입니다(예: 3:1, 5:2). 비우면 전역 조건 사용."); return; }
            _item.Rules = rules;
        }

        _item.SlackChannel = NormalizedChannel();

        // 반대 짝 코드(교차 알림) — 비우면 미사용, 형식만 검증.
        string pair = PairBox.Text.Trim().ToUpperInvariant();
        if (pair.Length > 0 && (pair.Length is < 5 or > 6 || !pair.All(char.IsLetterOrDigit)))
        { Error("반대 짝 코드는 5~6자리 영숫자입니다(예: 0197X0). 비우면 사용 안 함."); return; }
        if (pair.Length > 0 && pair == _item.Symbol)
        { Error("반대 짝 코드는 자기 자신과 달라야 합니다(레버리지↔인버스)."); return; }
        _item.PairSymbol = pair;

        // 종목별 시그널 override — 빈칸은 null(전역), 값이 있으면 범위 검증.
        if (!TryOverride(OvRsiBox.Text, 5, 95, out var ovRsi, "RSI")) return;
        if (!TryOverride(OvVolBox.Text, 0, 20, out var ovVol, "거래량 배수")) return;
        if (!TryOverride(OvPbBox.Text, 0, 0.9, out var ovPb, "%b")) return;
        _item.BottomRsiMax = ovRsi;
        _item.BottomVolumeRatio = ovVol;
        _item.BottomMinPercentB = ovPb;

        _item.AlertUp = AlertUpCheck.IsChecked == true;
        _item.AlertDown = AlertDownCheck.IsChecked == true;
        // 래더·갭다운/반등·고점 시그널은 국내·비지수만
        bool krStock = _item.Market == MarketKind.KR && !_item.IsIndex;
        _item.LadderAlert = LadderCheck.IsChecked == true && krStock;
        _item.BottomAlert = BottomCheck.IsChecked == true && krStock;
        _item.TopAlert = TopCheck.IsChecked == true && krStock;

        DialogResult = true;
    }

    private static string IndexName(string code) => code switch
    {
        "0001" => "코스피",
        "1001" => "코스닥",
        "2001" => "코스피200",
        _ => ""
    };

    /// <summary>override 입력 파싱 — 빈칸이면 null(전역), 값이 있으면 [min,max] 검증. 실패 시 Error 표시 후 false.</summary>
    private bool TryOverride(string text, double min, double max, out double? value, string label)
    {
        value = null;
        string t = text.Trim();
        if (t.Length == 0) return true;
        if (!double.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) || v < min || v > max)
        { Error($"종목 시그널 {label}는 {min}~{max} 범위 숫자이거나 비워두세요(전역)."); return false; }
        value = v;
        return true;
    }

    /// <summary>종목 전용 Slack 채널 입력 정규화 — 비우면 빈값(기본 채널), #/@ 접두사가 없으면 # 채널로.</summary>
    private string NormalizedChannel()
    {
        string channel = ChannelBox.Text.Trim();
        if (channel.Length > 0 && channel[0] is not ('#' or '@')) channel = "#" + channel;
        return channel;
    }

    /// <summary>입력한 채널로 테스트 알림 전송(비어 있으면 기본 채널 규칙).</summary>
    private async void ChannelTest_Click(object sender, RoutedEventArgs e)
    {
        string channel = NormalizedChannel();
        ChannelBox.Text = channel;   // 정규화 결과를 바로 보여줌(# 자동 접두)
        string name = NameBox.Text.Trim() is "조회 중…" or "" ? SymbolBox.Text.Trim() : NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "관심 종목";

        ChannelTestBtn.IsEnabled = false;
        try
        {
            using var slack = new SlackNotifier(_config);
            await slack.SendChannelTestAsync(name, channel);
            Info($"✅ 테스트 전송 완료 → {(channel.Length > 0 ? channel : "기본 채널")}");
        }
        catch (Exception ex)
        {
            Error($"테스트 전송 실패: {ex.Message} (채널이 존재하는지 확인)");
        }
        finally { ChannelTestBtn.IsEnabled = true; }
    }

    private void Error(string msg)
    {
        ErrorText.Foreground = (System.Windows.Media.Brush)FindResource("BearBrush");
        ErrorText.Text = "⚠ " + msg;
    }

    private void Info(string msg)
    {
        ErrorText.Foreground = (System.Windows.Media.Brush)FindResource("FgMuted");
        ErrorText.Text = msg;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
