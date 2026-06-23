using Stock.Watch.Conditions;
using Stock.Watch.Indicators;
using Stock.Watch.Models;

namespace Stock.Watch.Services;

/// <summary>종목 1개의 폴링 결과(현재가 + 계산된 지표 세트). UI 차트/시세표 갱신용.</summary>
public sealed record StockUpdate(WatchedStock Stock, Quote Quote, IndicatorSet Indicators);

/// <summary>
/// 폴링 루프의 핵심. 주기마다 관심종목 시세·일봉을 조회하고 지표를 계산해
/// 매수/매도 룰을 평가한다. 룰이 false→true로 전이하고 쿨다운이 지났을 때만 알림을 발생시킨다(도배 방지).
/// 이벤트는 백그라운드 스레드에서 발생하므로 UI 구독자는 Dispatcher로 마샬링해야 한다.
/// </summary>
public sealed class MonitorService
{
    private readonly AppConfig _config;
    private readonly KisApiClient _api;
    private readonly SlackNotifier _slack;
    private CancellationTokenSource? _cts;

    public MonitorService(AppConfig config, KisApiClient api, SlackNotifier slack)
    {
        _config = config;
        _api = api;
        _slack = slack;
    }

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public event Action<StockUpdate>? StockUpdated;
    public event Action<AlertLog>? AlertRaised;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        StatusChanged?.Invoke("감시 중지됨");
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        StatusChanged?.Invoke("감시 시작");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_config.MarketHoursOnly || IsMarketOpen(DateTime.Now))
                    await PollAllAsync(ct);
                else
                    StatusChanged?.Invoke($"장 시간 외 대기 중 ({DateTime.Now:HH:mm})");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _config.PollIntervalSeconds)), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollAllAsync(CancellationToken ct)
    {
        foreach (var stock in _config.Watchlist.ToList())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await RefreshStockAsync(stock, ct);
            }
            catch (KisApiException ex) { ErrorOccurred?.Invoke($"{stock.Display}: {ex.Message}"); }
            // 호출 간 짧은 간격으로 레이트리밋 완화
            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }
        StatusChanged?.Invoke($"갱신 완료 {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>단일 종목 시세·지표 갱신 후 조건 평가. 수동 새로고침에서도 재사용.</summary>
    public async Task<IndicatorSet?> RefreshStockAsync(WatchedStock stock, CancellationToken ct = default)
    {
        var candles = await _api.GetDailyCandlesAsync(stock.Code, 120, ct);
        if (candles.Count == 0) return null;

        var quote = await _api.GetQuoteAsync(stock.Code, ct);
        MergeLiveQuote(candles, quote);

        stock.LastPrice = quote.Price;
        stock.LastChangeRate = quote.ChangeRate;
        if (string.IsNullOrWhiteSpace(stock.Name)) stock.Name = stock.Code;

        var set = new IndicatorSet(candles);
        StockUpdated?.Invoke(new StockUpdate(stock, quote, set));

        EvaluateRules(stock, set, quote);
        return set;
    }

    /// <summary>오늘 봉을 실시간 현재가로 갱신(없으면 추가)해 지표가 장중 가격을 반영하도록 한다.</summary>
    private static void MergeLiveQuote(List<Candle> candles, Quote quote)
    {
        if (quote.Price <= 0) return;
        var last = candles[^1];
        if (last.Time.Date == DateTime.Today)
        {
            candles[^1] = last with
            {
                High = Math.Max(last.High, quote.Price),
                Low = last.Low <= 0 ? quote.Price : Math.Min(last.Low, quote.Price),
                Close = quote.Price,
                Volume = quote.Volume > 0 ? quote.Volume : last.Volume
            };
        }
        else if (DateTime.Today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            candles.Add(new Candle(DateTime.Today, quote.Price, quote.Price, quote.Price, quote.Price, quote.Volume));
        }
    }

    private void EvaluateRules(WatchedStock stock, IndicatorSet set, Quote quote)
    {
        int i = set.LastIndex;
        bool buyNow = stock.BuyRules.Evaluate(set, i);
        bool sellNow = stock.SellRules.Evaluate(set, i);

        if (buyNow && !stock.BuyWasTrue && CooldownPassed(stock.LastBuyAlertAt))
        {
            stock.LastBuyAlertAt = DateTime.Now;
            RaiseAlert(stock, RuleKind.Buy, stock.BuyRules, set, quote);
        }
        if (sellNow && !stock.SellWasTrue && CooldownPassed(stock.LastSellAlertAt))
        {
            stock.LastSellAlertAt = DateTime.Now;
            RaiseAlert(stock, RuleKind.Sell, stock.SellRules, set, quote);
        }

        stock.BuyWasTrue = buyNow;
        stock.SellWasTrue = sellNow;
    }

    private bool CooldownPassed(DateTime last)
        => (DateTime.Now - last).TotalSeconds >= Math.Max(0, _config.AlertCooldownSeconds);

    private void RaiseAlert(WatchedStock stock, RuleKind kind, RuleSet rules, IndicatorSet set, Quote quote)
    {
        var snap = set.Latest();
        string detail = snap == null ? "" :
            $"RSI {Fmt(snap.Rsi14)}, 볼린저 {Fmt(snap.BollLower)}~{Fmt(snap.BollUpper)}, 거래량 {snap.Volume:N0}(평균 {Fmt(snap.VolumeMa20)})";

        var alert = new AlertLog
        {
            Code = stock.Code,
            Name = stock.Name,
            Kind = kind,
            RuleSummary = rules.Summary(),
            Price = quote.Price,
            IndicatorDetail = detail
        };

        AlertRaised?.Invoke(alert);
        _ = SendSlackSafeAsync(alert);
    }

    private async Task SendSlackSafeAsync(AlertLog alert)
    {
        try { await _slack.SendAsync(alert); }
        catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }
    }

    private static string Fmt(double v) => double.IsNaN(v) ? "-" : v.ToString("0.#");

    /// <summary>한국 정규장 09:00~15:30, 평일.</summary>
    public static bool IsMarketOpen(DateTime now)
    {
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var t = now.TimeOfDay;
        return t >= new TimeSpan(9, 0, 0) && t <= new TimeSpan(15, 30, 0);
    }
}
