using Stock.Fetch.Indicators;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 관심 종목의 <b>바닥 반등 시그널</b> 알림 엔진(국내 · 당일 1분봉 · KIS 분봉 필요).
/// 차트에서 반복 관찰된 "상승 시작 가능성" 패턴(급락 → 볼린저 하단 터치 → 반등)을 조건화했다:
/// <list type="number">
/// <item>셋업: 최근 N봉(기본 5) 내 저가가 볼린저(20,2σ) 하단 터치/이탈 + 그 구간 최저 RSI(14)가 과매도(기본 ≤35)</item>
/// <item>트리거(1차 · 반등 시그널): 완성봉이 밴드 안으로 복귀 마감(종가&gt;하단) + 상승봉 + RSI 상승 전환
///       + 터치 구간 최대 거래량이 20봉 평균의 배수(기본 1.5×) 이상</item>
/// <item>확인(2차 · 골든크로스): 1차 후 M분(기본 20) 내 MA5가 MA20 상향 돌파(옵션)</item>
/// </list>
/// 판정은 <b>완성봉</b>(현재 형성 중인 분봉 제외)에서만 하고 같은 봉은 재판정하지 않는다(엣지).
/// 1차 알림 후 쿨다운(기본 15분) 동안 재알림하지 않는다. 분봉은 폴링마다 최신 1페이지(30봉)만 증분 병합해
/// KIS 유량 부담을 최소화한다. 알림은 <see cref="Raised"/>(트레이)와 Slack으로 동시 전송.
/// </summary>
public sealed class BottomSignalEngine(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack)
{
    public event Action<BottomSignal>? Raised;

    private sealed class State
    {
        public SortedDictionary<DateTime, Candle> Bars = new();   // 당일 1분봉 캐시
        public DateTime LastEvalBar = DateTime.MinValue;          // 마지막 판정 완성봉 시각
        public DateTime LastFetch = DateTime.MinValue;            // 조회 스로틀
        public DateTime FiredAt = DateTime.MinValue;              // 1차 알림 봉 시각(쿨다운·확인 창 기준)
        public bool AwaitCross;                                    // 2차(골든크로스) 확인 대기
        public bool PrevBelow;                                     // 직전 완성봉 MA5≤MA20 여부
        public bool HasPrevRel;                                    // PrevBelow 유효 여부
    }

    private readonly Dictionary<string, State> _states = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>국내 종목·옵트인일 때만 호출한다(호출 측에서 게이팅). KIS 키가 없으면 조용히 건너뛴다.</summary>
    public async Task EvaluateAsync(WatchItem item, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials) return;   // 분봉은 KIS 전용
        ResetIfNewDay();

        var now = DateTime.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
        // 정규장(+마감 직후 여유)만. BB(20)이 서려면 어차피 09:20대 이후에나 판정 가능하다.
        if (now.TimeOfDay < new TimeSpan(9, 5, 0) || now.TimeOfDay > new TimeSpan(15, 40, 0)) return;

        if (!_states.TryGetValue(item.Symbol, out var st)) _states[item.Symbol] = st = new State();
        if ((now - st.LastFetch).TotalSeconds < 25) return;   // 폴링보다 잦은 재조회 방지
        st.LastFetch = now;

        // 분봉 수집: 캐시가 얕으면 워밍업(최대 ~100봉), 이후엔 최신 1페이지(30봉)만 증분 병합.
        List<Candle> page;
        try { page = await registry.KisRecentMinutesAsync(item.Symbol, st.Bars.Count < 45 ? 100 : 30, ct); }
        catch (OperationCanceledException) { throw; }
        catch { return; }   // 유량 초과 등 일시 실패 → 다음 폴링에서 재시도
        foreach (var b in page)
            if (b.Date.Date == DateTime.Today) st.Bars[b.Date] = b;

        // 완성봉만(현재 형성 중인 분봉 제외).
        var nowMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var bars = st.Bars.Values.Where(b => b.Date < nowMinute).ToList();
        int last = bars.Count - 1;
        if (last < 25) return;   // BB20 + RSI14 최소 표본

        // 지표 일괄 계산.
        var closes = bars.Select(b => (double)b.Close).ToList();
        var (_, _, lower) = IndicatorMath.Bollinger(closes);
        var rsi = IndicatorMath.Rsi(closes);
        var ma5 = IndicatorMath.Sma(closes, 5);
        var ma20 = IndicatorMath.Sma(closes, 20);
        var volMa = IndicatorMath.Sma(bars.Select(b => (double)b.Volume).ToList(), 20);

        // 새로 완성된 봉들만 순서대로 판정(폴링 간격 동안 여러 봉이 쌓였어도 놓치지 않음).
        int start = 0;
        while (start <= last && bars[start].Date <= st.LastEvalBar) start++;
        for (int i = Math.Max(start, 21); i <= last; i++)
            EvaluateBar(item, st, bars, i, lower, rsi, ma5, ma20, volMa);
        st.LastEvalBar = bars[last].Date;
    }

    /// <summary>완성봉 1개(인덱스 i)에 대한 1차(반등)·2차(골든크로스) 판정.</summary>
    private void EvaluateBar(WatchItem item, State st, List<Candle> bars, int i,
        double[] lower, double[] rsi, double[] ma5, double[] ma20, double[] volMa)
    {
        var bar = bars[i];

        // ── 2차: 골든크로스 확인(직전 봉 MA5≤MA20 → 이번 봉 MA5>MA20) ──
        if (!double.IsNaN(ma5[i]) && !double.IsNaN(ma20[i]))
        {
            bool below = ma5[i] <= ma20[i];
            int confirmWin = Math.Max(1, config.BottomConfirmWindowMinutes);
            if (st.AwaitCross && (bar.Date - st.FiredAt).TotalMinutes > confirmWin)
                st.AwaitCross = false;   // 확인 창 만료
            if (st.AwaitCross && st.HasPrevRel && st.PrevBelow && !below)
            {
                st.AwaitCross = false;
                Fire(item, BottomSignalKind.GoldenCross, bar.Close, bar.Date,
                    $"MA5 {ma5[i]:N0} > MA20 {ma20[i]:N0} 상향 돌파 — 반등 흐름 확인 (1차 시그널 {st.FiredAt:HH:mm} 후속)");
            }
            st.PrevBelow = below;
            st.HasPrevRel = true;
        }

        // ── 1차: 밴드 하단 반등 시그널 ──
        if (double.IsNaN(lower[i]) || double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 쿨다운: 최근 1차 알림 후 일정 시간 재알림 금지.
        if ((bar.Date - st.FiredAt).TotalMinutes < Math.Max(1, config.BottomCooldownMinutes)) return;

        // 셋업: 최근 lookback봉 내 볼린저 하단 터치(저가 ≤ 하단).
        int lookback = Math.Max(2, config.BottomTouchLookback);
        int touchIdx = -1;
        for (int k = Math.Max(1, i - lookback); k <= i; k++)
        {
            if (double.IsNaN(lower[k])) continue;
            if ((double)bars[k].Low <= lower[k]) { touchIdx = k; break; }   // 가장 이른 터치 봉
        }
        if (touchIdx < 0) return;

        // 셋업: 터치 구간 최저 RSI가 과매도 상한 이하 + 트리거: RSI 상승 전환.
        double minRsi = double.MaxValue;
        long maxVol = 0;
        for (int k = touchIdx; k <= i; k++)
        {
            if (!double.IsNaN(rsi[k])) minRsi = Math.Min(minRsi, rsi[k]);
            maxVol = Math.Max(maxVol, bars[k].Volume);
        }
        if (minRsi > config.BottomRsiMax) return;
        if (rsi[i] <= rsi[i - 1]) return;

        // 트리거: 밴드 안 복귀 마감 + 상승봉.
        if ((double)bar.Close <= lower[i]) return;
        if (bar.Close <= bar.Open && bar.Close <= bars[i - 1].Close) return;

        // 트리거: 터치 구간 거래량 급증(20봉 평균 대비 배수).
        if (volMa[i] <= 0) return;
        double volRatio = maxVol / volMa[i];
        if (volRatio < config.BottomVolumeRatio) return;

        st.FiredAt = bar.Date;
        st.AwaitCross = config.BottomConfirmCross;
        Fire(item, BottomSignalKind.Rebound, bar.Close, bar.Date,
            $"볼린저 하단 {lower[i]:N0}원 터치({bars[touchIdx].Date:HH:mm}) 후 복귀 마감 {bar.Close:N0}원 · " +
            $"RSI {rsi[i]:0.#} 상승 전환(저점 {minRsi:0.#}) · 거래량 {volRatio:0.0}× (20봉 평균 대비)");
    }

    // ───────────────────────── 발생/전송 ─────────────────────────
    private void Fire(WatchItem item, BottomSignalKind kind, decimal price, DateTime barTime, string detail)
    {
        var s = new BottomSignal(item.Symbol, item.Name, kind, price, detail, barTime);
        Raised?.Invoke(s);
        _ = SafeSlackAsync(s);
    }

    private async Task SafeSlackAsync(BottomSignal s)
    {
        try { await slack.SendBottomSignalAsync(s); }
        catch { /* Slack 실패는 무시 */ }
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today == _day) return;
        _states.Clear();
        _day = today;
    }
}
