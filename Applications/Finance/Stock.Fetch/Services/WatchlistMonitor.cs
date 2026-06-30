using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>관심 종목 1건의 폴링 결과(현재가·등락율).</summary>
public sealed record WatchQuote(WatchItem Item, decimal Price, decimal ChangeRate);

/// <summary>종목별 추세 추적 상태: 기준 등락율과 그 기준을 잡은 시각.</summary>
internal sealed class TrendState
{
    public decimal RefRate;
    public DateTime RefTime;
}

/// <summary>
/// 관심 종목(워치리스트)을 주기적으로 폴링해 <b>추세</b>를 감지·알림한다.
/// ① 종목별 첫 관측 시 기준값을 잡고 현재 수준을 1회 알림(시작 알림).
/// ② 이후 기준값(직전 알림 시점의 등락율) 대비 현재 등락율이 step(%)만큼 상승/하락하면
///    방향과 함께 알림하고 기준값을 현재값으로 갱신(엣지).
/// ③ window(분) 안에 step 변동이 없으면 기준값을 조용히 현재값으로 재설정 → "최근 기간의 추세"만 감지.
/// ④ 다이제스트 주기마다 전체 종목 시세 요약을 알림.
/// 미국장은 KST 야간이므로 장 시간 게이팅 없이 항상 폴링. 이벤트는 백그라운드 스레드에서 발생하므로
/// UI 구독자는 Dispatcher 마샬링이 필요하다.
/// </summary>
public sealed class WatchlistMonitor(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack) : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, TrendState> _trend = new();
    private DateTime _lastDigestAt = DateTime.MinValue;

    public event Action<WatchAlert>? WatchAlertRaised;
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
        _trend.Clear();           // 재시작 시 기준값을 새로 잡고 시작 알림을 다시 보내도록
        _lastDigestAt = DateTime.MinValue;
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

        double step = Math.Max(0.1, config.WatchStepPercent);
        double window = Math.Max(0, config.WatchWindowMinutes);
        var snapshot = new List<WatchQuote>();

        // 목록에서 제거된 종목의 추세 상태 정리
        var live = items.Select(i => i.Symbol).ToHashSet();
        foreach (var key in _trend.Keys.Where(k => !live.Contains(k)).ToList()) _trend.Remove(key);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            Quote? q;
            try { q = await registry.WatchQuoteAsync(item, ct); }
            catch { continue; }
            if (q is null || q.Price <= 0) continue;

            item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Symbol : item.Name;
            snapshot.Add(new WatchQuote(item, q.Price, q.ChangeRate));
            Evaluate(item, q.Price, q.ChangeRate, step, window);

            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }

        MaybeSendDigest(snapshot);
        StatusChanged?.Invoke($"갱신 {DateTime.Now:HH:mm:ss} · 관심 {snapshot.Count}/{items.Count}종목");
    }

    /// <summary>
    /// 추세 감지: 기준값 대비 현재 등락율이 step만큼 변하면 방향과 함께 알림(엣지)하고 기준 갱신.
    /// window 안에 step 변동이 없으면 기준값을 조용히 현재값으로 재설정한다. 첫 관측은 1회성 시작 알림.
    /// </summary>
    private void Evaluate(WatchItem item, decimal price, decimal rate, double step, double window)
    {
        var now = DateTime.Now;
        if (!_trend.TryGetValue(item.Symbol, out var st))
        {
            // 첫 관측: 기준값 설정 + 현재 수준 1회 알림(시작 알림)
            _trend[item.Symbol] = new TrendState { RefRate = rate, RefTime = now };
            Raise(new WatchAlert(item, price, rate, rate, step, window, IsStartup: true, now));
            return;
        }

        double delta = (double)(rate - st.RefRate);
        if (Math.Abs(delta) >= step)
        {
            Raise(new WatchAlert(item, price, rate, st.RefRate, step, window, IsStartup: false, now));
            st.RefRate = rate;
            st.RefTime = now;
        }
        else if (window > 0 && (now - st.RefTime).TotalMinutes >= window)
        {
            // 기간 내 step 변동 없음 → 기준값을 현재값으로 조용히 재설정(최근 기간 추세만 추적)
            st.RefRate = rate;
            st.RefTime = now;
        }
    }

    private void Raise(WatchAlert alert)
    {
        WatchAlertRaised?.Invoke(alert);
        _ = SafeAsync(() => slack.SendWatchAlertAsync(alert));
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
