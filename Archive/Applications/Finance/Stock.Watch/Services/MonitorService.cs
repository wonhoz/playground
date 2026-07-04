using Stock.Watch.Conditions;
using Stock.Watch.Indicators;
using Stock.Watch.Models;

namespace Stock.Watch.Services;

/// <summary>종목 1개의 시세 결과(현재가 + 계산된 지표 세트). UI 차트/시세표 갱신용.</summary>
public sealed record StockUpdate(WatchedStock Stock, Quote Quote, IndicatorSet Indicators);

/// <summary>
/// 감시 엔진. 폴링으로 일봉·지표 기준선을 주기 갱신하고, WebSocket 실시간 체결가로 장중 틱을 받아
/// 당일 봉을 갱신·재평가한다. 룰이 false→true로 전이하고 쿨다운이 지났을 때만 알림을 발생시킨다(도배 방지).
/// 이벤트는 백그라운드 스레드에서 발생하므로 UI 구독자는 Dispatcher로 마샬링해야 한다.
/// </summary>
public sealed class MonitorService
{
    private readonly AppConfig _config;
    private readonly KisApiClient _api;
    private readonly SlackNotifier _slack;
    private readonly KisRealtimeClient _realtime;
    private CancellationTokenSource? _cts;

    // 폴링으로 받은 일봉(실시간 병합 전 기준선). 실시간 틱이 이 위에 당일 가격을 얹는다.
    private readonly Dictionary<string, List<Candle>> _baseCandles = new();
    private readonly Dictionary<string, DateTime> _lastTickEval = new();
    private readonly object _sync = new();

    public MonitorService(AppConfig config, KisApiClient api, SlackNotifier slack)
    {
        _config = config;
        _api = api;
        _slack = slack;
        _realtime = new KisRealtimeClient(api);
        _realtime.Tick += OnRealtimeTick;
        _realtime.StatusChanged += s => StatusChanged?.Invoke(s);
        _realtime.ErrorOccurred += s => ErrorOccurred?.Invoke(s);
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

        if (_config.UseRealtime)
            _realtime.Start(_config.Watchlist.Select(s => s.Code));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _realtime.Stop();
        StatusChanged?.Invoke("감시 중지됨");
    }

    /// <summary>감시 중 종목 추가 시 실시간 구독도 즉시 반영.</summary>
    public void AddRealtimeCode(string code)
    {
        if (_config.UseRealtime && _realtime.IsRunning) _ = _realtime.AddCodeAsync(code);
    }

    public void RemoveRealtimeCode(string code)
    {
        lock (_sync) { _baseCandles.Remove(code); _lastTickEval.Remove(code); }
        if (_realtime.IsRunning) _ = _realtime.RemoveCodeAsync(code);
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
            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }
        StatusChanged?.Invoke($"갱신 완료 {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>단일 종목 시세·지표 갱신 후 조건 평가. 수동 새로고침에서도 재사용.</summary>
    public async Task<IndicatorSet?> RefreshStockAsync(WatchedStock stock, CancellationToken ct = default)
    {
        var candles = await _api.GetDailyCandlesAsync(stock.Code, 120, ct);
        if (candles.Count == 0) return null;

        // 실시간 틱이 얹을 기준선 저장(병합 전 복사본)
        lock (_sync) _baseCandles[stock.Code] = new List<Candle>(candles);

        var quote = await _api.GetQuoteAsync(stock.Code, ct);
        MergeLivePrice(candles, quote.Price, quote.Volume);

        stock.LastPrice = quote.Price;
        stock.LastChangeRate = quote.ChangeRate;
        if (string.IsNullOrWhiteSpace(stock.Name)) stock.Name = stock.Code;

        var set = new IndicatorSet(candles);
        StockUpdated?.Invoke(new StockUpdate(stock, quote, set));

        EvaluateRules(stock, set, quote);
        return set;
    }

    // ──────────────────────────── 실시간 틱 ────────────────────────────
    private void OnRealtimeTick(RealtimeTick tick)
    {
        var stock = _config.Watchlist.FirstOrDefault(s => s.Code == tick.Code);
        if (stock == null) return;

        // 가격은 항상 갱신, 지표 재계산·평가는 종목당 최소 0.8초 간격으로 스로틀
        stock.LastPrice = tick.Price;
        stock.LastChangeRate = tick.ChangeRate;

        List<Candle>? baseC;
        lock (_sync)
        {
            if (_lastTickEval.TryGetValue(tick.Code, out var last) && (DateTime.Now - last).TotalMilliseconds < 800)
                return;
            _lastTickEval[tick.Code] = DateTime.Now;
            if (!_baseCandles.TryGetValue(tick.Code, out var bc)) return; // 폴링으로 일봉을 먼저 받아야 함
            baseC = new List<Candle>(bc);
        }

        MergeLivePrice(baseC, tick.Price, tick.CumVolume);
        var set = new IndicatorSet(baseC);
        var quote = new Quote(tick.Code, tick.Price, 0, tick.ChangeRate, tick.CumVolume, tick.Time);

        StockUpdated?.Invoke(new StockUpdate(stock, quote, set));
        EvaluateRules(stock, set, quote);
    }

    /// <summary>오늘 봉을 실시간 가격으로 갱신(없으면 추가)해 지표가 장중 가격을 반영하도록 한다.</summary>
    private static void MergeLivePrice(List<Candle> candles, decimal price, long cumVolume)
    {
        if (price <= 0 || candles.Count == 0) return;
        var last = candles[^1];
        if (last.Time.Date == DateTime.Today)
        {
            candles[^1] = last with
            {
                High = Math.Max(last.High, price),
                Low = last.Low <= 0 ? price : Math.Min(last.Low, price),
                Close = price,
                Volume = cumVolume > 0 ? cumVolume : last.Volume
            };
        }
        else if (DateTime.Today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            candles.Add(new Candle(DateTime.Today, price, price, price, price, cumVolume));
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

    public void DisposeRealtime() => _realtime.Dispose();
}
