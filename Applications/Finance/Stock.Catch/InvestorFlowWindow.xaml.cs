using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>
/// 즐겨찾기 국내 종목의 <b>실시간 수급·호가</b> 창 — 외국인/기관/개인/프로그램 순매수·외국인 소진율·체결강도 +
/// 10단 호가창. 선택 종목을 주기(기본 1.8초)로 KIS 폴링해 갱신한다(장중에만 호가·체결강도가 채워짐).
/// </summary>
public partial class InvestorFlowWindow : Window
{
    private readonly PriceSourceRegistry _registry;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<DepthRow> _asks = new();
    private readonly ObservableCollection<DepthRow> _bids = new();
    private readonly Brush _up, _down, _muted, _fg;
    private string? _code;
    private bool _busy;

    public InvestorFlowWindow(PriceSourceRegistry registry, AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _registry = registry;
        _config = config;
        _up = (Brush)Application.Current.Resources["BullBrush"];      // 상승·순매수(빨강)
        _down = (Brush)Application.Current.Resources["BearBrush"];    // 하락·순매도(파랑)
        _muted = (Brush)Application.Current.Resources["FgMuted"];
        _fg = (Brush)Application.Current.Resources["FgBrush"];
        AskList.ItemsSource = _asks;
        BidList.ItemsSource = _bids;

        // 즐겨찾기 중 국내(6자리 코드)만.
        FavList.ItemsSource = (_config.Favorites ?? new())
            .Where(f => !string.IsNullOrWhiteSpace(f.Code) && f.Code.Trim().Length == 6).ToList();
        if (FavList.Items.Count > 0) FavList.SelectedIndex = 0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();

        if (!_config.HasKisCredentials)
            PollText.Text = "KIS 키 없음 — 설정에서 입력하세요.";
    }

    private async void FavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavList.SelectedItem is not FavoriteStock f) return;
        _code = f.Code.Trim();
        NameText.Text = string.IsNullOrEmpty(f.Name) ? _code : $"{f.Name} ({_code})";
        PriceText.Text = ""; RateText.Text = "";
        _asks.Clear(); _bids.Clear();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy || _code is null || !_config.HasKisCredentials) return;
        _busy = true;
        try
        {
            var sdTask = _registry.KrSupplyDemandAsync(_code);
            var mdTask = _registry.KrMarketDepthAsync(_code);
            await Task.WhenAll(sdTask, mdTask);
            RenderSupply(sdTask.Result);
            RenderDepth(mdTask.Result);
            PollText.Text = $"갱신 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            PollText.Text = "조회 실패: " + (ex.Message.Length > 40 ? ex.Message[..40] : ex.Message);
        }
        finally { _busy = false; }
    }

    private void RenderSupply(SupplyDemand s)
    {
        PriceText.Text = s.Price > 0 ? $"{s.Price:N0}" : "-";
        PriceText.Foreground = s.ChangeRate > 0 ? _up : s.ChangeRate < 0 ? _down : _fg;
        RateText.Text = s.Price > 0 ? $"{s.ChangeRate:+0.00;-0.00;0.00}%" : "";
        RateText.Foreground = s.ChangeRate > 0 ? _up : s.ChangeRate < 0 ? _down : _muted;

        SetNet(FrgnText, s.ForeignNet);
        SetNet(OrgnText, s.InstitutionNet);
        SetNet(PrsnText, s.PersonNet);
        SetNet(PgtrText, s.ProgramNet);
        EhrtText.Text = $"{s.ForeignExhaust:0.00}%";
        ExecText.Text = s.ExecStrength > 0 ? $"{s.ExecStrength:0.0}" : "-";
        ExecText.Foreground = s.ExecStrength >= 100 ? _up : s.ExecStrength > 0 ? _down : _muted;   // 100 초과=매수 우위
    }

    /// <summary>순매수 값 표기: 양수=매수 우위(빨강)·음수=매도 우위(파랑). 천 단위·부호.</summary>
    private void SetNet(TextBlock tb, long qty)
    {
        tb.Text = $"{qty:+#,##0;-#,##0;0}";
        tb.Foreground = qty > 0 ? _up : qty < 0 ? _down : _muted;
    }

    private void RenderDepth(MarketDepth d)
    {
        long max = 1;
        foreach (var a in d.Asks) max = Math.Max(max, a.Qty);
        foreach (var b in d.Bids) max = Math.Max(max, b.Qty);
        const double MaxBar = 168;

        _asks.Clear();
        for (int i = 9; i >= 0; i--)   // 매도10(고가) → 매도1(저가·스프레드 근접)
            _asks.Add(Row($"매도{i + 1}", d.Asks[i], _down, max, MaxBar));
        _bids.Clear();
        for (int i = 0; i < 10; i++)   // 매수1(고가·스프레드 근접) → 매수10(저가)
            _bids.Add(Row($"매수{i + 1}", d.Bids[i], _up, max, MaxBar));

        BidStrengthText.Text = d.TotalAsk + d.TotalBid > 0
            ? $"총 매도 {d.TotalAsk:N0} / 매수 {d.TotalBid:N0} · 매수우위 {d.BidStrength:+0.0;-0.0}%"
            : "장 시간 외 — 호가 미표시";
    }

    private DepthRow Row(string label, AskBid ab, Brush brush, long max, double maxBar) => new()
    {
        Label = label,
        PriceText = ab.Price > 0 ? $"{ab.Price:N0}" : "-",
        QtyText = ab.Qty > 0 ? $"{ab.Qty:N0}" : "",
        PriceBrush = ab.Price > 0 ? brush : _muted,
        BarBrush = brush,
        BarWidth = ab.Qty > 0 ? Math.Max(2, (double)ab.Qty / max * maxBar) : 0,
        BarAlign = HorizontalAlignment.Right,
    };
}

/// <summary>호가 한 줄(바인딩용).</summary>
public sealed class DepthRow
{
    public string Label { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string QtyText { get; set; } = "";
    public Brush PriceBrush { get; set; } = Brushes.Gray;
    public Brush BarBrush { get; set; } = Brushes.Gray;
    public double BarWidth { get; set; }
    public HorizontalAlignment BarAlign { get; set; } = HorizontalAlignment.Right;
}
