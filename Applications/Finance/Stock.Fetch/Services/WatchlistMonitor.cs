using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>관심 종목 1건의 폴링 결과(현재가·등락율).</summary>
public sealed record WatchQuote(WatchItem Item, decimal Price, decimal ChangeRate);

/// <summary>
/// 관심 종목(워치리스트)을 주기적으로 폴링해, ① 전일 대비 등락율이 설정 임계값(±3/5/7/10% 등)을
/// <b>새로 넘어설 때(엣지)</b> 알림하고, ② 다이제스트 주기마다 전체 종목 시세 요약을 알림한다.
/// 미국장은 KST 야간이므로 장 시간 게이팅 없이 항상 폴링(주기는 사용자 제어). 이벤트는 백그라운드
/// 스레드에서 발생하므로 UI 구독자는 Dispatcher 마샬링이 필요하다.
/// </summary>
public sealed class WatchlistMonitor(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack) : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, HashSet<double>> _prevUp = new();
    private readonly Dictionary<string, HashSet<double>> _prevDown = new();
    private DateTime _lastDigestAt = DateTime.MinValue;

    public event Action<WatchItem, decimal, decimal, double>? WatchAlertRaised; // item, price, rate, signedThreshold
    public event Action<IReadOnlyList<WatchQuote>>? DigestReady;
    public event Action<string>? StatusChanged;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        StatusChanged?.Invoke("관심 종목 모니터링 중지됨");
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        StatusChanged?.Invoke("관심 종목 모니터링 시작");
        while (!ct.IsCancellationRequested)
        {
            try { await PollAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { StatusChanged?.Invoke("오류: " + ex.Message); }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, config.WatchPollIntervalSeconds)), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var items = config.Watchlist.ToList();
        if (items.Count == 0) { StatusChanged?.Invoke("관심 종목 없음"); return; }

        var thresholds = config.WatchThresholds.Where(t => t > 0).OrderBy(t => t).ToList();
        var snapshot = new List<WatchQuote>();

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            Quote? q;
            try { q = await registry.WatchQuoteAsync(item, ct); }
            catch { continue; }
            if (q is null || q.Price <= 0) continue;

            item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Symbol : item.Name;
            snapshot.Add(new WatchQuote(item, q.Price, q.ChangeRate));
            Evaluate(item, q.Price, (double)q.ChangeRate, thresholds);

            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }

        MaybeSendDigest(snapshot);
        StatusChanged?.Invoke($"갱신 {DateTime.Now:HH:mm:ss} · 관심 {snapshot.Count}/{items.Count}종목");
    }

    /// <summary>등락율이 임계값을 새로 돌파할 때만(엣지) 알림. 같은 임계값은 되돌아갔다 재돌파해야 재알림.</summary>
    private void Evaluate(WatchItem item, decimal price, double rate, List<double> thresholds)
    {
        var nowUp = thresholds.Where(t => rate >= t).ToHashSet();
        var nowDown = thresholds.Where(t => rate <= -t).ToHashSet();
        var prevUp = _prevUp.GetValueOrDefault(item.Symbol) ?? new HashSet<double>();
        var prevDown = _prevDown.GetValueOrDefault(item.Symbol) ?? new HashSet<double>();

        foreach (var t in nowUp.Except(prevUp).OrderBy(x => x)) Raise(item, price, rate, +t);
        foreach (var t in nowDown.Except(prevDown).OrderBy(x => x)) Raise(item, price, rate, -t);

        _prevUp[item.Symbol] = nowUp;
        _prevDown[item.Symbol] = nowDown;
    }

    private void Raise(WatchItem item, decimal price, double rate, double signedThreshold)
    {
        WatchAlertRaised?.Invoke(item, price, (decimal)rate, signedThreshold);
        _ = SafeAsync(() => slack.SendWatchAlertAsync(item, price, (decimal)rate, signedThreshold));
    }

    private void MaybeSendDigest(List<WatchQuote> snapshot)
    {
        if (config.WatchDigestIntervalMinutes <= 0 || snapshot.Count == 0) return;
        if ((DateTime.Now - _lastDigestAt).TotalMinutes < config.WatchDigestIntervalMinutes) return;
        _lastDigestAt = DateTime.Now;
        DigestReady?.Invoke(snapshot);
        _ = SafeAsync(() => slack.SendDigestAsync(snapshot));
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { StatusChanged?.Invoke("Slack 전송 오류: " + ex.Message); }
    }

    public void Dispose() => _cts?.Cancel();
}
