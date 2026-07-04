using Stock.Catch.Indicators;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 관심 종목의 <b>1분봉 시그널</b> 알림 엔진(국내 · 당일 1분봉 · KIS 분봉 필요).
/// 바닥·고점이 같은 분봉 캐시를 공유하므로 한 종목에 둘 다 켜도 KIS 호출은 폴링당 1회다.
///
/// <para><b>바닥 반등 시그널</b> (급락 → 볼린저 하단 터치 → 반등):</para>
/// <list type="number">
/// <item>셋업: 최근 N봉(기본 5) 내 저가가 볼린저(20,2σ) 하단 터치/이탈 + 그 구간 최저 RSI(14)가 과매도(기본 ≤30)</item>
/// <item>트리거(1차): 완성봉이 밴드 안 복귀 마감 + %b ≥ 하한(기본 0.15 · 밴드폭의 15% 이상 회복) + 상승봉
///       + RSI 상승 전환 + 터치 구간 최대 거래량 ≥ 20봉 평균 × 배수(기본 2.0)</item>
/// <item>밴드워킹 필터: 최근 10봉 중 하단 터치 봉이 임계(기본 4) 초과면 "지속 하락 중"으로 보고 스킵
///       (진짜 V바닥은 터치가 1~3봉에 집중 · 완만한 흘러내림은 바닥이 아님)</item>
/// <item>확인(2차): 1차 후 M분(기본 20) 내 MA5가 MA20 상향 돌파(골든크로스 · 옵션)</item>
/// </list>
///
/// <para><b>고점 경고 시그널</b> (상단 밴드워킹 → 이탈, 바닥의 거울상):</para>
/// <list type="number">
/// <item>셋업: 최근 N봉(기본 5) 내 고가가 상단 터치 ≥ 최소 봉 수(기본 2 · 밴드워킹 확인)
///       + 그 구간 최고 RSI가 과매수(기본 ≥70)</item>
/// <item>트리거(1차): 완성봉이 밴드 안 복귀 마감(%b ≤ 기본 0.8) + 하락봉 + RSI 하향 전환
///       + 소진 증거(구간 내 긴 윗꼬리 봉 또는 클라이맥스 거래량 ≥ 평균×배수) 중 1개 이상</item>
/// <item>확인(2차): 1차 후 M분 내 MA5가 MA20 하향 돌파(데드크로스 · 옵션)</item>
/// </list>
///
/// 판정은 <b>완성봉</b>(현재 형성 중인 분봉 제외)에서만 하고 같은 봉은 재판정하지 않는다(엣지).
/// 1차 알림 후 방향별 쿨다운(기본 15분) 동안 재알림하지 않는다. 분봉은 폴링마다 최신 1페이지(30봉)만
/// 증분 병합해 KIS 유량 부담을 최소화한다. 알림은 <see cref="Raised"/>(트레이)와 Slack으로 동시 전송.
/// </summary>
public sealed class MinuteSignalEngine(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack)
{
    /// <summary>밴드워킹 필터 검사 구간(완성봉 수). 바닥·고점 공용.</summary>
    private const int WalkWindow = 10;

    public event Action<MinuteSignal>? Raised;

    private sealed class State
    {
        public SortedDictionary<DateTime, Candle> Bars = new();   // 당일 1분봉 캐시
        public DateTime LastEvalBar = DateTime.MinValue;          // 마지막 판정 완성봉 시각
        public DateTime LastFetch = DateTime.MinValue;            // 조회 스로틀
        // 바닥(반등) 상태
        public DateTime BottomFiredAt = DateTime.MinValue;        // 1차 알림 봉 시각(쿨다운·확인 창 기준)
        public decimal BottomFiredPrice;                           // 1차 알림 봉 종가(GC 모멘텀 판정 기준)
        public bool AwaitGolden;                                   // 2차(골든크로스) 확인 대기
        public bool AwaitFollow;                                   // 직후 봉 양봉 지속(조기 확인) 대기
        // 고점(경고) 상태
        public DateTime TopFiredAt = DateTime.MinValue;
        public bool AwaitDead;                                     // 2차(데드크로스) 확인 대기
        // MA5/MA20 관계 추적(골든·데드 공용)
        public bool PrevBelow;                                     // 직전 완성봉 MA5≤MA20 여부
        public bool HasPrevRel;
        // 일봉 추세 게이트(하루 1회 계산 · 실패 시 10분 후 재시도)
        public double? DayTrend;                                   // 래더 추세점수(−1~+1 · null=미로드)
        public DateTime LastTrendTry = DateTime.MinValue;
    }

    private readonly Dictionary<string, State> _states = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>국내 종목·옵트인(바닥/고점 중 1개 이상)일 때만 호출한다(호출 측에서 게이팅). KIS 키가 없으면 조용히 건너뛴다.</summary>
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

        // 일봉 추세 게이트: 완성 일봉(오늘 제외) 기반 래더 추세점수를 하루 1회 계산.
        // 실측 근거: 일봉 하락 대세 종목(인버스)의 GC 승률 33% — 역추세 반등을 강등 표기.
        if (config.BottomTrendGate && st.DayTrend is null && (now - st.LastTrendTry).TotalMinutes >= 10)
        {
            st.LastTrendTry = now;
            try
            {
                var daily = await registry.KrDailyAsync(item.Symbol, 40, ct);
                var completed = daily.Where(c => c.Date.Date != DateTime.Today).ToList();
                if (completed.Count >= LadderCalculator.RequiredDays)
                {
                    var win = completed.TakeLast(LadderCalculator.RequiredDays).ToList();
                    var r = LadderCalculator.Calculate(
                        new StockSeries(item.Symbol, "", "", SourceKind.Naver, win),
                        new LadderParams(0, 0, UseTrend: true));
                    st.DayTrend = r.TrendScore;
                }
            }
            catch { /* 일봉 조회 실패 → 10분 후 재시도(게이트 없이 판정 계속) */ }
        }

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

        // 새로 완성된 봉들만 순서대로 판정(폴링 간격 동안 여러 봉이 쌓였어도 놓치지 않음).
        int start = 0;
        while (start <= last && bars[start].Date <= st.LastEvalBar) start++;
        ScanBars(item, st, bars, start, Fire, item.BottomAlert, item.TopAlert);
        st.LastEvalBar = bars[last].Date;
    }

    /// <summary>
    /// 과거 1분봉 목록을 라이브와 <b>동일한 조건</b>(쿨다운·확인 창·필터 포함)으로 스캔해
    /// 알림이 발생했을 시점 목록을 반환한다(알림 전송·상태 오염 없음). 분봉 CSV 백테스트용.
    /// dayTrend를 주면 라이브와 동일하게 일봉 추세 게이트도 적용한다(null=게이트 없음).
    /// </summary>
    public List<MinuteSignal> Backtest(IReadOnlyList<Candle> minuteBars, string code, string name, double? dayTrend = null)
    {
        var bars = minuteBars.OrderBy(b => b.Date).ToList();
        var result = new List<MinuteSignal>();
        if (bars.Count < 26) return result;

        var item = new WatchItem { Symbol = code, Name = name };
        var st = new State { DayTrend = dayTrend };   // 라이브 상태와 분리된 임시 상태
        ScanBars(item, st, bars, 0, result.Add, bottom: true, top: true);
        return result;
    }

    /// <summary>지표 일괄 계산 후 fromIdx부터 완성봉을 순서대로 판정 — 라이브(Fire)·백테스트(수집) 공용 코어.</summary>
    private void ScanBars(WatchItem item, State st, List<Candle> bars, int fromIdx,
        Action<MinuteSignal> emit, bool bottom, bool top)
    {
        var closes = bars.Select(b => (double)b.Close).ToList();
        var (upper, _, lower) = IndicatorMath.Bollinger(closes);
        var rsi = IndicatorMath.Rsi(closes);
        var ma5 = IndicatorMath.Sma(closes, 5);
        var ma20 = IndicatorMath.Sma(closes, 20);
        var volMa = IndicatorMath.Sma(bars.Select(b => (double)b.Volume).ToList(), 20);

        for (int i = Math.Max(fromIdx, 21); i < bars.Count; i++)
        {
            EvaluateCross(item, st, bars, i, ma5, ma20, emit);
            if (bottom) EvaluateFollow(item, st, bars, i, emit);
            if (bottom) EvaluateBottom(item, st, bars, i, upper, lower, rsi, volMa, emit);
            if (top) EvaluateTop(item, st, bars, i, upper, lower, rsi, volMa, emit);
        }
    }

    /// <summary>
    /// 1차 반등 직후 첫 완성봉의 양봉 지속(조기 확인) 판정 — 골든크로스보다 1~5분 빠른 힌트.
    /// 실측: 진짜 반등 3/4에서 직후 양봉, 가짜 2/3에서 직후 음봉/보합.
    /// </summary>
    private void EvaluateFollow(WatchItem item, State st, List<Candle> bars, int i, Action<MinuteSignal> emit)
    {
        if (!st.AwaitFollow || bars[i].Date <= st.BottomFiredAt) return;
        st.AwaitFollow = false;   // 직후 1봉만 판정(양봉 아니면 조용히 종료)
        var bar = bars[i];
        if (bar.Close <= bar.Open) return;

        // 몸통 크기(직전 20봉 평균 대비) — 지속 강도 참고치.
        decimal avgBody = 0;
        for (int k = i - 20; k < i; k++) avgBody += Math.Abs(bars[k].Close - bars[k].Open);
        avgBody /= 20;
        double ratio = avgBody > 0 ? (double)(Math.Abs(bar.Close - bar.Open) / avgBody) : 0;
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.FollowThrough, bar.Close,
            $"1차({st.BottomFiredAt:HH:mm}) 직후 양봉 지속 · 몸통 {ratio:0.0}×", bar.Date));
    }

    // ───────────────────────── 2차: 골든/데드크로스 확인 ─────────────────────────

    /// <summary>MA5/MA20 관계를 봉마다 추적해 1차 시그널 후 확인 창 내 크로스를 알림한다.</summary>
    private void EvaluateCross(WatchItem item, State st, List<Candle> bars, int i, double[] ma5, double[] ma20,
        Action<MinuteSignal> emit)
    {
        if (double.IsNaN(ma5[i]) || double.IsNaN(ma20[i])) return;
        var bar = bars[i];
        bool below = ma5[i] <= ma20[i];

        // 확인 창 만료 처리
        int goldenWin = Math.Max(1, config.BottomConfirmWindowMinutes);
        int deadWin = Math.Max(1, config.TopConfirmWindowMinutes);
        if (st.AwaitGolden && (bar.Date - st.BottomFiredAt).TotalMinutes > goldenWin) st.AwaitGolden = false;
        if (st.AwaitDead && (bar.Date - st.TopFiredAt).TotalMinutes > deadWin) st.AwaitDead = false;

        if (st.HasPrevRel)
        {
            if (st.AwaitGolden && st.PrevBelow && !below)
            {
                st.AwaitGolden = false;
                // GC 모멘텀 필터: 1차 이후 상승률이 임계(기본 0.8%) 미달이면 "약한 확인"으로 강등.
                // 실측: 가짜 GC(07-02 12:35)는 +0.51%, 진짜는 +0.90~6.73% — 횡보성 크로스 구분.
                double rise = st.BottomFiredPrice > 0 ? (double)(bar.Close / st.BottomFiredPrice - 1) * 100 : 0;
                bool weakMomentum = rise < config.BottomGcMinRisePct;
                // 일봉 추세 게이트: 일봉 대세가 하락(추세점수<0)이면 역추세 반등으로 강등.
                // 실측: 하락 대세 종목(인버스)의 GC 승률 33% vs 상승 대세(레버리지) 61%.
                bool counterTrend = config.BottomTrendGate && st.DayTrend is < 0;
                string reason = (weakMomentum, counterTrend) switch
                {
                    (true, true) => $" (모멘텀 {config.BottomGcMinRisePct:0.0#}% 미달 + 일봉 역추세 {st.DayTrend:0.00} — 주의)",
                    (true, false) => $" (모멘텀 {config.BottomGcMinRisePct:0.0#}% 미달 — 횡보성 크로스 주의)",
                    (false, true) => $" (일봉 역추세 {st.DayTrend:0.00} — 하락 대세 속 반등 주의)",
                    _ => "",
                };
                emit(new MinuteSignal(item.Symbol, item.Name,
                    weakMomentum || counterTrend ? MinuteSignalKind.WeakGoldenCross : MinuteSignalKind.GoldenCross, bar.Close,
                    $"MA5 {ma5[i]:N0} > MA20 {ma20[i]:N0} 돌파 · 1차({st.BottomFiredAt:HH:mm}) 후 {(bar.Date - st.BottomFiredAt).TotalMinutes:0}분 · " +
                    $"{rise:+0.00;-0.00}%{reason}", bar.Date));
            }
            if (st.AwaitDead && !st.PrevBelow && below)
            {
                st.AwaitDead = false;
                emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.DeadCross, bar.Close,
                    $"MA5 {ma5[i]:N0} < MA20 {ma20[i]:N0} 돌파 · 경고({st.TopFiredAt:HH:mm}) 후 {(bar.Date - st.TopFiredAt).TotalMinutes:0}분", bar.Date));
            }
        }
        st.PrevBelow = below;
        st.HasPrevRel = true;
    }

    // ───────────────────────── 1차: 바닥 반등 ─────────────────────────

    /// <summary>완성봉 1개(인덱스 i)에 대한 바닥 반등 판정. 밴드워킹·%b 필터로 지속 하락·약반등을 걸러낸다.</summary>
    private void EvaluateBottom(WatchItem item, State st, List<Candle> bars, int i,
        double[] upper, double[] lower, double[] rsi, double[] volMa, Action<MinuteSignal> emit)
    {
        var bar = bars[i];
        if (double.IsNaN(lower[i]) || double.IsNaN(upper[i]) ||
            double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 쿨다운: 최근 1차 알림 후 일정 시간 재알림 금지.
        if ((bar.Date - st.BottomFiredAt).TotalMinutes < Math.Max(1, config.BottomCooldownMinutes)) return;

        // 밴드워킹 필터: 최근 WalkWindow봉 중 하단 터치가 임계 초과면 완만한 지속 하락으로 보고 스킵.
        int walkTouches = 0;
        for (int k = Math.Max(0, i - WalkWindow + 1); k <= i; k++)
            if (!double.IsNaN(lower[k]) && (double)bars[k].Low <= lower[k]) walkTouches++;
        if (walkTouches > Math.Max(1, config.BottomWalkMaxTouches)) return;

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

        // 트리거: 밴드 안 복귀 + %b 하한(밴드폭 대비 회복 비율) + 상승봉.
        double width = upper[i] - lower[i];
        if (width <= 0) return;
        double pb = ((double)bar.Close - lower[i]) / width;
        if (pb < config.BottomMinPercentB) return;
        if (bar.Close <= bar.Open && bar.Close <= bars[i - 1].Close) return;

        // 트리거: 터치 구간 거래량 급증(20봉 평균 대비 배수).
        if (volMa[i] <= 0) return;
        double volRatio = maxVol / volMa[i];
        if (volRatio < config.BottomVolumeRatio) return;

        st.BottomFiredAt = bar.Date;
        st.BottomFiredPrice = bar.Close;
        st.AwaitGolden = config.BottomConfirmCross;
        st.AwaitFollow = config.BottomFollowCandle;
        st.AwaitDead = false;   // 방향 전환 — 반대편 확인 대기 해제
        string trendTag = config.BottomTrendGate && st.DayTrend is < 0 ? $" · 일봉 역추세 {st.DayTrend:0.00} ⚠" : "";
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.Rebound, bar.Close,
            $"하단터치 {bars[touchIdx].Date:HH:mm} → 복귀 %b {pb:0.00} · RSI {rsi[i]:0.#}↑(저점 {minRsi:0.#}) · 거래량 {volRatio:0.0}×{trendTag}", bar.Date));
    }

    // ───────────────────────── 1차: 고점 경고 ─────────────────────────

    /// <summary>
    /// 완성봉 1개(인덱스 i)에 대한 고점 경고 판정(바닥의 거울상).
    /// 상단 밴드워킹(최소 봉 수) + RSI 과매수 → 밴드 안 복귀 하락봉 + RSI 하향 전환 + 소진 증거(윗꼬리/거래량).
    /// </summary>
    private void EvaluateTop(WatchItem item, State st, List<Candle> bars, int i,
        double[] upper, double[] lower, double[] rsi, double[] volMa, Action<MinuteSignal> emit)
    {
        var bar = bars[i];
        if (double.IsNaN(lower[i]) || double.IsNaN(upper[i]) ||
            double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 쿨다운(고점 방향 별도).
        if ((bar.Date - st.TopFiredAt).TotalMinutes < Math.Max(1, config.TopCooldownMinutes)) return;

        // 셋업: 최근 lookback봉 내 상단 터치(고가 ≥ 상단) — 가장 이른 터치와 터치 봉 수.
        int lookback = Math.Max(2, config.TopTouchLookback);
        int touchIdx = -1, touchCount = 0;
        for (int k = Math.Max(1, i - lookback); k <= i; k++)
        {
            if (double.IsNaN(upper[k])) continue;
            if ((double)bars[k].High >= upper[k])
            {
                if (touchIdx < 0) touchIdx = k;
                touchCount++;
            }
        }
        // 밴드워킹 확인: 단발 터치가 아니라 최소 봉 수 이상 상단에 붙어 있었어야 진짜 과열.
        if (touchIdx < 0 || touchCount < Math.Max(1, config.TopMinWalkTouches)) return;

        // 셋업: 터치 구간 최고 RSI가 과매수 하한 이상 + 트리거: RSI 하향 전환.
        double maxRsi = double.MinValue;
        long maxVol = 0;
        bool hasWick = false;
        for (int k = touchIdx; k <= i; k++)
        {
            if (!double.IsNaN(rsi[k])) maxRsi = Math.Max(maxRsi, rsi[k]);
            maxVol = Math.Max(maxVol, bars[k].Volume);
            // 긴 윗꼬리(슈팅스타형): 윗꼬리가 몸통의 1.5배 이상 — 매수 소진 흔적.
            var b = bars[k];
            decimal body = Math.Abs(b.Close - b.Open);
            decimal wick = b.High - Math.Max(b.Close, b.Open);
            if (wick > 0 && wick >= body * 1.5m) hasWick = true;
        }
        if (maxRsi < config.TopRsiMin) return;
        if (rsi[i] >= rsi[i - 1]) return;

        // 트리거: 밴드 안 복귀 마감(%b 상한) + 하락봉.
        double width = upper[i] - lower[i];
        if (width <= 0) return;
        double pb = ((double)bar.Close - lower[i]) / width;
        if (pb > config.TopMaxPercentB) return;
        if (bar.Close >= bar.Open && bar.Close >= bars[i - 1].Close) return;

        // 트리거: 소진 증거 — 긴 윗꼬리 또는 클라이맥스 거래량 중 1개 이상.
        double volRatio = volMa[i] > 0 ? maxVol / volMa[i] : 0;
        bool climax = volRatio >= config.TopVolumeRatio;
        if (!hasWick && !climax) return;

        string evidence = (hasWick, climax) switch
        {
            (true, true) => $"긴 윗꼬리 + 거래량 {volRatio:0.0}×",
            (true, false) => "긴 윗꼬리(매수 소진)",
            _ => $"거래량 클라이맥스 {volRatio:0.0}×",
        };

        st.TopFiredAt = bar.Date;
        st.AwaitDead = config.TopConfirmCross;
        st.AwaitGolden = false;   // 방향 전환 — 반대편 확인 대기 해제
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.TopWarn, bar.Close,
            $"상단워킹 {touchCount}봉 → 이탈 %b {pb:0.00} · RSI {rsi[i]:0.#}↓(고점 {maxRsi:0.#}) · {evidence}", bar.Date));
    }

    // ───────────────────────── 발생/전송 ─────────────────────────

    /// <summary>라이브 경로의 emit — 트레이 이벤트 + Slack 전송.</summary>
    private void Fire(MinuteSignal s)
    {
        Raised?.Invoke(s);
        _ = SafeSlackAsync(s);
    }

    private async Task SafeSlackAsync(MinuteSignal s)
    {
        try { await slack.SendMinuteSignalAsync(s); }
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
