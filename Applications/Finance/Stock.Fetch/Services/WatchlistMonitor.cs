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
/// 시장별 장 시간 게이팅: 한국은 평일 08:00~20:00 KST(KRX만 설정 시 09:00~15:30),
/// 미국은 평일 프리~애프터(ET 04:00~20:00 · 서머타임 자동). 장외 종목은 폴링·알림을 건너뛰고
/// 추세 기준을 비워 재개장 첫 관측에서 새로 잡는다(개장 요약 1회). 이벤트는 백그라운드 스레드에서
/// 발생하므로 UI 구독자는 Dispatcher 마샬링이 필요하다.
/// </summary>
public sealed class WatchlistMonitor(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack, LadderAlertEngine ladder, ReversalEstimator reversal, MinuteSignalEngine minuteSignal) : IDisposable
{
    private CancellationTokenSource? _cts;
    // symbol → (조건 키 → 추세 상태). 조건(기간/단위)마다 기준값을 따로 추적한다.
    private readonly Dictionary<string, Dictionary<string, TrendState>> _trend = new();
    private readonly Dictionary<string, int> _failCount = new();   // 종목별 연속 실패 횟수
    private readonly HashSet<string> _failAlerted = new();          // 실패 알림을 이미 보낸 종목
    private DateTime _lastDigestAt = DateTime.MinValue;

    public event Action<WatchAlert>? WatchAlertRaised;
    /// <summary>모니터링 시작 시 종목별 시작 알림을 한 번에 모아 전달(요약 1건).</summary>
    public event Action<IReadOnlyList<WatchAlert>>? StartupSummary;
    public event Action<IReadOnlyList<WatchQuote>>? DigestReady;
    /// <summary>시세 조회 연속 실패 알림(item, 사유, 연속 실패 횟수).</summary>
    public event Action<WatchItem, string, int>? FetchFailed;
    /// <summary>실패 후 정상 복구 알림.</summary>
    public event Action<WatchItem>? FetchRecovered;
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
        _failCount.Clear();
        _failAlerted.Clear();
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

        var globalRules = (config.WatchRules ?? new()).Where(r => r.StepUp > 0 && r.StepDown > 0 && r.WindowMinutes > 0).ToList();
        var snapshot = new List<WatchQuote>();
        var startups = new List<WatchAlert>();   // 시작 알림은 모아서 한 번에 요약 전송

        // 목록에서 제거된 종목의 추세·실패 상태 정리
        var live = items.Select(i => i.Symbol).ToHashSet();
        foreach (var key in _trend.Keys.Where(k => !live.Contains(k)).ToList()) _trend.Remove(key);
        foreach (var key in _failCount.Keys.Where(k => !live.Contains(k)).ToList()) _failCount.Remove(key);
        _failAlerted.RemoveWhere(k => !live.Contains(k));

        int closed = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            // 장 시간 게이팅: 장외 종목은 폴링·알림 스킵 + 추세 기준 제거(재개장 시 새로 잡고 개장 요약 1회).
            if (!IsMarketActive(item))
            {
                closed++;
                _trend.Remove(item.Symbol);
                _failCount.Remove(item.Symbol);
                continue;
            }

            Quote? q = null;
            string? failReason = null;
            try { q = await registry.WatchQuoteAsync(item, ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { failReason = ex.Message; }
            if (q is null || q.Price <= 0) failReason ??= "시세를 가져오지 못했습니다(티커/소스 확인).";

            if (failReason != null)
            {
                HandleFailure(item, failReason);
            }
            else
            {
                HandleSuccess(item);
                item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Symbol : item.Name;
                snapshot.Add(new WatchQuote(item, q!.Price, q.ChangeRate));
                var rules = item.Rules.Count > 0 ? item.Rules : globalRules; // 종목별 조건 우선
                Evaluate(item, q.Price, q.ChangeRate, rules, startups);

                // 매수 래더·갭다운 알림(국내·비지수·옵트인)
                if (item.LadderAlert && !item.IsIndex && item.Market == MarketKind.KR)
                {
                    try { await ladder.EvaluateAsync(item.Symbol, item.Name, q.Price, ct); }
                    catch (OperationCanceledException) { break; }
                    catch { /* 래더 알림 실패는 무시 */ }
                }

                // 1분봉 시그널: 바닥 반등·고점 경고(국내·비지수·옵트인·KIS 1분봉 · 분봉 캐시 공유)
                if ((item.BottomAlert || item.TopAlert) && !item.IsIndex && item.Market == MarketKind.KR)
                {
                    try { await minuteSignal.EvaluateAsync(item, ct); }
                    catch (OperationCanceledException) { break; }
                    catch { /* 1분봉 시그널 실패는 무시 */ }
                }
            }

            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }

        if (startups.Count > 0) RaiseStartupSummary(startups);
        MaybeSendDigest(snapshot);
        StatusChanged?.Invoke(closed == items.Count
            ? $"장 시간 외 대기 중 ({DateTime.Now:HH:mm}) · 관심 {items.Count}종목"
            : $"갱신 {DateTime.Now:HH:mm:ss} · 관심 {snapshot.Count}/{items.Count}종목{(closed > 0 ? $" (장외 {closed}종목 대기)" : "")}");
    }

    /// <summary>
    /// 종목의 시장이 현재 거래 시간대인지. 한국(지수 포함)=평일 08:00~20:00 KST(KRX만 설정 시 09:00~15:30),
    /// 미국=평일 프리~애프터(ET 04:00~20:00 · 서머타임 자동).
    /// </summary>
    private bool IsMarketActive(WatchItem item)
        => item.Market == MarketKind.KR
            ? PortfolioMonitor.IsMarketOpen(DateTime.Now, config)
            : UsMarket.CurrentSession() != UsSession.Closed;

    /// <summary>
    /// 다중 조건 추세 감지: 조건마다 기준값을 따로 두고, 기준 대비 현재 등락율이 그 조건의 step만큼 변하면
    /// 방향과 함께 알림(엣지)하고 기준 갱신. 조건의 window 안에 step 변동이 없으면 기준값을 현재값으로 재설정.
    /// 종목 첫 관측은 모든 조건의 기준을 잡고 현재 수준 1회 알림(시작 알림).
    /// </summary>
    private void Evaluate(WatchItem item, decimal price, decimal rate, List<TrendRule> rules, List<WatchAlert> startups)
    {
        if (rules.Count == 0) return;
        var now = DateTime.Now;

        if (!_trend.TryGetValue(item.Symbol, out var states))
        {
            states = new Dictionary<string, TrendState>();
            foreach (var r in rules) states[r.Key] = new TrendState { RefRate = rate, RefTime = now };
            _trend[item.Symbol] = states;
            // 시작 알림은 개별 전송하지 않고 모아서 요약(아래 RaiseStartupSummary).
            startups.Add(new WatchAlert(item, price, rate, rate, 0, 0, IsStartup: true, now, TrendRule.Summary(rules)));
            return;
        }

        // 조건이 바뀌면 반영: 사라진 조건 제거, 새 조건은 기준만 잡고(알림 없음) 시작.
        var keys = rules.Select(r => r.Key).ToHashSet();
        foreach (var k in states.Keys.Where(k => !keys.Contains(k)).ToList()) states.Remove(k);

        foreach (var r in rules)
        {
            if (!states.TryGetValue(r.Key, out var st))
            {
                states[r.Key] = new TrendState { RefRate = rate, RefTime = now };
                continue;
            }
            double delta = (double)(rate - st.RefRate);
            bool up = delta >= 0;
            double step = up ? r.StepUp : r.StepDown;   // 상승/하락 임계값 개별 적용
            if (Math.Abs(delta) >= step)
            {
                // 방향 필터 + 한국 개장 직후 음소거(기준 갱신은 항상 수행).
                bool dirOk = (up && item.AlertUp) || (!up && item.AlertDown);
                bool muted = item.Market == MarketKind.KR && IsKrOpenMuted(now);
                if (dirOk && !muted)
                    Raise(item, new WatchAlert(item, price, rate, st.RefRate, step, r.WindowMinutes, IsStartup: false, now));
                st.RefRate = rate;
                st.RefTime = now;
            }
            else if (r.WindowMinutes > 0 && (now - st.RefTime).TotalMinutes >= r.WindowMinutes)
            {
                st.RefRate = rate;   // 기간 내 변동 없음 → 기준 재설정(최근 기간 추세만 추적)
                st.RefTime = now;
            }
        }
    }

    /// <summary>한국 개장 직후(프리 08:00·정규 09:00 후 N분) 추세 알림 음소거 구간 여부. 평일만.</summary>
    private bool IsKrOpenMuted(DateTime now)
    {
        if (!config.MuteKrOpenAlerts) return false;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        int mins = Math.Max(1, config.KrOpenMuteMinutes);
        var t = now.TimeOfDay;
        bool pre = t >= new TimeSpan(8, 0, 0) && t < new TimeSpan(8, 0, 0).Add(TimeSpan.FromMinutes(mins));
        bool reg = t >= new TimeSpan(9, 0, 0) && t < new TimeSpan(9, 0, 0).Add(TimeSpan.FromMinutes(mins));
        return pre || reg;
    }

    private void Raise(WatchItem item, WatchAlert alert) => _ = RaiseEnrichedAsync(item, alert);

    /// <summary>추세 알림에 반등 추정을 첨부(설정·비지수)한 뒤 트레이·Slack으로 전송.</summary>
    private async Task RaiseEnrichedAsync(WatchItem item, WatchAlert alert)
    {
        WatchAlert enriched = alert;
        if (config.WatchReversalEstimate && !item.IsIndex)
        {
            try
            {
                var est = await reversal.EstimateAsync(item, alert.Price);
                if (est != null)
                    enriched = alert with { ReversalProb = est.Probability, ReversalDirText = est.DirText, ReversalText = est.Detail, ReversalBasis = est.BasisText };
            }
            catch { /* 추정 실패 시 원본 알림 유지 */ }
        }
        WatchAlertRaised?.Invoke(enriched);
        await SafeAsync(() => slack.SendWatchAlertAsync(enriched));
    }

    /// <summary>모아둔 시작 알림을 요약 1건으로 전송(Slack·트레이 풍선 각 1회).</summary>
    private void RaiseStartupSummary(IReadOnlyList<WatchAlert> alerts)
    {
        StartupSummary?.Invoke(alerts);
        _ = SafeAsync(() => slack.SendWatchStartupSummaryAsync(alerts));
    }

    /// <summary>시세 조회 실패 누적 — 연속 임계 횟수 도달 시 1회 알림(엣지).</summary>
    private void HandleFailure(WatchItem item, string reason)
    {
        int n = _failCount[item.Symbol] = _failCount.GetValueOrDefault(item.Symbol) + 1;
        int thr = config.FetchFailAlertThreshold;
        if (thr > 0 && n == thr && _failAlerted.Add(item.Symbol))
        {
            FetchFailed?.Invoke(item, reason, n);
            _ = SafeAsync(() => slack.SendFetchFailureAsync(item.ToString(), "관심 종목", item.SourceLabel, reason, n));
        }
    }

    /// <summary>조회 성공 — 실패 카운터 리셋, 직전에 실패 알림을 보냈다면 복구 알림 1회.</summary>
    private void HandleSuccess(WatchItem item)
    {
        _failCount[item.Symbol] = 0;
        if (_failAlerted.Remove(item.Symbol))
        {
            FetchRecovered?.Invoke(item);
            _ = SafeAsync(() => slack.SendFetchRecoveryAsync(item.ToString(), "관심 종목"));
        }
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
