using Stock.Catch.Indicators;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 관심 종목의 <b>멀티 타임프레임 1분봉 시그널</b> 알림 엔진(국내 · 당일 1분봉 · KIS 분봉 필요).
/// 1분봉을 기준으로 3/5/10/15분 <b>롤링(convolution) 봉</b>을 합성해 타임프레임별로 독립 판정한다
/// — 롤링 봉은 매 1분마다 최근 N분을 집계(O=첫 시가, H/L=구간 최고/최저, C=마지막 종가, V=합)하므로
/// 고정 경계 봉과 달리 매분 갱신되는 "약간 멀리서 본" 지표를 제공한다.
///
/// <para><b>바닥 반등 시그널</b> (급락 → 볼린저 하단 터치 → 반등):</para>
/// <list type="number">
/// <item>셋업: 최근 N봉(기본 5) 내 저가가 볼린저(20,2σ) 하단 터치/이탈 + 그 구간 최저 RSI(14) 과매도(기본 ≤35)</item>
/// <item>트리거(1차): 밴드 안 복귀 마감 + %b ≥ 하한(기본 0.15) + 상승봉 + RSI 상승 전환
///       + 터치 구간 최대 거래량 ≥ 20봉 평균 × 배수(기본 1.5)</item>
/// <item>밴드워킹 필터: 최근 10봉 중 하단 터치 봉이 임계(기본 7) 초과면 지속 하락으로 스킵</item>
/// <item>확인(2차): 1차 후 M분×tf 내 MA5↗MA20 골든크로스 — 모멘텀(1차 대비 상승률)로
///       🔥강력(≥2.0%)/✅강(≥0.8%)/⚠약 등급. 직후 양봉 조기 확인은 1분봉 전용.</item>
/// </list>
///
/// <para><b>고점 경고 시그널</b>: 상단 밴드워킹(≥2봉) + RSI 과매수(≥70) → 밴드 안 복귀 하락봉
/// + RSI 하향 전환 + 소진 증거(긴 윗꼬리/클라이맥스 거래량) → M분×tf 내 데드크로스 확인.</para>
///
/// 판정은 <b>완성봉</b>에서만(같은 봉 재판정 없음 · 엣지), 쿨다운·확인 창은 타임프레임에 비례해
/// 스케일(예: 15분봉 쿨다운 = 15분 × 15). 일봉 추세점수·전일 종가(갭)는 컨텍스트로 표기만 한다
/// (강등 아님 — 실측상 최고 시그널이 폭락 직후(추세 −1.00)에 발생). 알림은 <see cref="Raised"/>
/// (트레이)와 Slack으로 동시 전송하며 타임프레임을 [N분] 접두로 구분한다.
/// </summary>
public sealed class MinuteSignalEngine(AppConfig config, PriceSourceRegistry registry, SlackNotifier slack)
{
    /// <summary>밴드워킹 필터 검사 구간(완성봉 수). 바닥·고점 공용.</summary>
    private const int WalkWindow = 10;

    public event Action<MinuteSignal>? Raised;

    /// <summary>타임프레임별 판정 상태(쿨다운·확인 대기·크로스 추적).</summary>
    private sealed class State
    {
        public DateTime LastEvalBar = DateTime.MinValue;          // 마지막 판정 완성봉 시각
        // 바닥(반등) 상태
        public DateTime BottomFiredAt = DateTime.MinValue;        // 1차 알림 봉 시각(쿨다운·확인 창 기준)
        public decimal BottomFiredPrice;                           // 1차 알림 봉 종가(GC 모멘텀 판정 기준)
        public bool AwaitGolden;                                   // 2차(골든크로스) 확인 대기
        public bool AwaitFollow;                                   // 직후 봉 양봉 지속(조기 확인) 대기 — 1분봉 전용
        public bool BottomFiredDiv;                                // 1차 시그널의 다이버전스 여부(GC가 상속)
        // 고점(경고) 상태
        public DateTime TopFiredAt = DateTime.MinValue;
        public bool AwaitDead;                                     // 2차(데드크로스) 확인 대기
        // MA5/MA20 관계 추적(골든·데드 공용)
        public bool PrevBelow;                                     // 직전 완성봉 MA5≤MA20 여부
        public bool HasPrevRel;
    }

    /// <summary>종목 단위 공용 데이터(분봉 캐시·일봉 컨텍스트) + 타임프레임별 상태.</summary>
    private sealed class SymbolData
    {
        public SortedDictionary<DateTime, Candle> Bars = new();   // 당일 1분봉 캐시
        public DateTime LastFetch = DateTime.MinValue;            // 조회 스로틀
        public double? DayTrend;                                   // 일봉 추세점수(−1~+1 · 컨텍스트)
        public decimal PrevClose;                                  // 전일 정규 종가(갭 컨텍스트 · 0=미로드)
        public DateTime LastTrendTry = DateTime.MinValue;
        public bool BriefSent;                                     // 개장 브리핑(하루 1회) 전송 여부
        public Dictionary<int, State> Tf = new();                  // 타임프레임 → 판정 상태
        // ── 라이브 전용 상태 ──
        public DateTime LastTopWarnAt = DateTime.MinValue;         // 최근 고점 경고 시각(교차 알림용)
        public DateTime LastCrossAt = DateTime.MinValue;           // 최근 전환 확인 발화(쿨다운 30분)
        public int BoostLevel;                                     // 적응형 폴링: 0=기본 · 1=관찰(20초) · 2=주목(15초)
        public DateTime BoostUntil = DateTime.MinValue;            // 부스트 만료(확인 창 종료 시각)
    }

    private readonly Dictionary<string, SymbolData> _symbols = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>설정의 시그널 타임프레임 목록 정제(1~60분 · 중복 제거 · 오름차순 · 비면 1분).</summary>
    private int[] Timeframes()
    {
        var list = (config.SignalTimeframes ?? new()).Where(t => t is >= 1 and <= 60).Distinct().OrderBy(t => t).ToArray();
        return list.Length > 0 ? list : new[] { 1 };
    }

    /// <summary>국내 종목·옵트인(바닥/고점 중 1개 이상)일 때만 호출한다(호출 측에서 게이팅). KIS 키가 없으면 조용히 건너뛴다.</summary>
    public async Task EvaluateAsync(WatchItem item, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials) return;   // 분봉은 KIS 전용
        ResetIfNewDay();

        var now = DateTime.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
        // 정규장(+마감 직후 여유)만. BB(20)이 서려면 어차피 09:20대 이후에나 판정 가능하다.
        if (now.TimeOfDay < new TimeSpan(9, 5, 0) || now.TimeOfDay > new TimeSpan(15, 40, 0)) return;

        if (!_symbols.TryGetValue(item.Symbol, out var sd)) _symbols[item.Symbol] = sd = new SymbolData();
        // 재조회 스로틀: 기본 25초, 적응형 부스트 중(1차 후 확인 창)엔 12초까지 촘촘하게.
        int fetchGap = sd.BoostLevel > 0 && sd.BoostUntil > now ? 12 : 25;
        if ((now - sd.LastFetch).TotalSeconds < fetchGap) return;
        sd.LastFetch = now;

        // 일봉 컨텍스트(추세점수·전일 종가)를 하루 1회 로드 — 실패 시 10분 후 재시도, 없어도 판정은 계속.
        if (config.BottomTrendGate && sd.DayTrend is null && (now - sd.LastTrendTry).TotalMinutes >= 10)
        {
            sd.LastTrendTry = now;
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
                    sd.DayTrend = r.TrendScore;
                    sd.PrevClose = completed[^1].Close;
                }
            }
            catch { /* 일봉 조회 실패 → 10분 후 재시도 */ }
        }

        // 분봉 수집: 캐시가 얕으면 워밍업(최대 ~100봉), 이후엔 최신 1페이지(30봉)만 증분 병합.
        List<Candle> page;
        try { page = await registry.KisRecentMinutesAsync(item.Symbol, sd.Bars.Count < 45 ? 100 : 30, ct); }
        catch (OperationCanceledException) { throw; }
        catch { return; }   // 유량 초과 등 일시 실패 → 다음 폴링에서 재시도
        foreach (var b in page)
            if (b.Date.Date == DateTime.Today) sd.Bars[b.Date] = b;

        // 완성봉만(현재 형성 중인 분봉 제외).
        var nowMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var bars = sd.Bars.Values.Where(b => b.Date < nowMinute).ToList();
        if (bars.Count < 26) return;   // BB20 + RSI14 최소 표본(1분봉 기준)
        var vwap = SessionVwap(bars);  // 컨텍스트용 세션 VWAP(원본 1분봉 기준)

        // ☀ 개장 브리핑(하루 1회 · 라이브 전용): 갭 + 일봉 추세로 오늘 분위기를 미리 요약.
        if (!sd.BriefSent && sd.DayTrend is { } dtb && bars.Count >= 1)
        {
            sd.BriefSent = true;
            double gapPct = sd.PrevClose > 0 ? (double)(bars[0].Open / sd.PrevClose - 1) * 100 : 0;
            string mood = dtb <= -0.5 ? "전일 급락 대세 — V바닥 반등 기회 관찰 구간"
                : dtb >= 0.5 ? "상승 대세 지속 — 눌림목 반등 우위"
                : "중립 — 시그널 컨텍스트 보고 판단";
            Fire(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.MorningBrief, bars[0].Open,
                $"갭 {gapPct:+0.0;-0.0}% · 일봉추세 {dtb:+0.00;-0.00} · {mood}", bars[0].Date));
        }

        foreach (int tf in Timeframes())
        {
            var tfBars = RollBars(bars, tf);
            if (tfBars.Count < 26) continue;   // 해당 타임프레임 표본 부족(장 초반 상위 TF)
            if (!sd.Tf.TryGetValue(tf, out var st)) sd.Tf[tf] = st = new State();

            int start = 0;
            while (start < tfBars.Count && tfBars[start].Date <= st.LastEvalBar) start++;
            ScanBars(item, st, tfBars, start, Fire, item.BottomAlert, item.TopAlert, tf, sd.DayTrend, sd.PrevClose, vwap);
            st.LastEvalBar = tfBars[^1].Date;
        }
    }

    /// <summary>
    /// 당일 세션 VWAP(거래량 가중 평균가) — 원본 1분봉 누적으로 계산해 봉 종료 시각으로 조회한다
    /// (롤링 봉은 구간이 겹쳐 거래량이 중복되므로 반드시 1분봉 기준). 컨텍스트 표기 전용.
    /// 실측(14일×3종목): 확인GC(✅🔥)가 VWAP 위면 승률 71%·낙폭 −0.97%, 아래면 46%·−2.19% —
    /// 단 최고 시그널(07-03 10:05 🔥)은 VWAP 아래에서 발생 → 강등 아닌 표기로만 사용.
    /// </summary>
    private static Dictionary<DateTime, double> SessionVwap(List<Candle> minuteBars)
    {
        var map = new Dictionary<DateTime, double>(minuteBars.Count);
        double pv = 0, vv = 0;
        foreach (var b in minuteBars)
        {
            pv += (double)(b.High + b.Low + b.Close) / 3.0 * b.Volume;
            vv += b.Volume;
            map[b.Date] = vv > 0 ? pv / vv : double.NaN;
        }
        return map;
    }

    /// <summary>
    /// 과거 1분봉 목록을 라이브와 <b>동일한 조건</b>(타임프레임·쿨다운·확인 창·필터 포함)으로 스캔해
    /// 알림이 발생했을 시점 목록을 반환한다(알림 전송·상태 오염 없음). 분봉 CSV 백테스트용.
    /// dayTrend/prevClose를 주면 컨텍스트(일봉추세·갭)도 라이브와 동일하게 표기된다.
    /// </summary>
    public List<MinuteSignal> Backtest(IReadOnlyList<Candle> minuteBars, string code, string name,
        double? dayTrend = null, decimal prevClose = 0m)
    {
        var bars = minuteBars.OrderBy(b => b.Date).ToList();
        var result = new List<MinuteSignal>();
        if (bars.Count < 26) return result;

        var item = new WatchItem { Symbol = code, Name = name };
        var vwap = SessionVwap(bars);
        foreach (int tf in Timeframes())
        {
            var tfBars = RollBars(bars, tf);
            if (tfBars.Count < 26) continue;
            var st = new State();   // 라이브 상태와 분리된 임시 상태
            ScanBars(item, st, tfBars, 0, result.Add, bottom: true, top: true, tf, dayTrend, prevClose, vwap);
        }
        return result.OrderBy(s => s.Time).ThenBy(s => s.Timeframe).ToList();
    }

    /// <summary>
    /// 1분봉 → N분 롤링(convolution) 봉 합성: 각 1분 완성 시각 t에 대해 [t−N+1, t] 구간을 집계.
    /// 봉 시각은 구간 마지막(t) — 매분 갱신되므로 상위 타임프레임도 1분 해상도로 판정된다.
    /// </summary>
    private static List<Candle> RollBars(List<Candle> bars, int n)
    {
        if (n <= 1) return bars;
        var result = new List<Candle>(Math.Max(0, bars.Count - n + 1));
        for (int i = n - 1; i < bars.Count; i++)
        {
            decimal high = decimal.MinValue, low = decimal.MaxValue;
            long vol = 0;
            for (int k = i - n + 1; k <= i; k++)
            {
                if (bars[k].High > high) high = bars[k].High;
                if (bars[k].Low < low) low = bars[k].Low;
                vol += bars[k].Volume;
            }
            result.Add(new Candle(bars[i].Date, bars[i - n + 1].Open, high, low, bars[i].Close, vol));
        }
        return result;
    }

    /// <summary>지표 일괄 계산 후 fromIdx부터 완성봉을 순서대로 판정 — 라이브(Fire)·백테스트(수집) 공용 코어.</summary>
    private void ScanBars(WatchItem item, State st, List<Candle> bars, int fromIdx,
        Action<MinuteSignal> emit, bool bottom, bool top, int tf, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null)
    {
        var closes = bars.Select(b => (double)b.Close).ToList();
        var (upper, _, lower) = IndicatorMath.Bollinger(closes);
        var rsi = IndicatorMath.Rsi(closes);
        var ma5 = IndicatorMath.Sma(closes, 5);
        var ma20 = IndicatorMath.Sma(closes, 20);
        var volMa = IndicatorMath.Sma(bars.Select(b => (double)b.Volume).ToList(), 20);

        for (int i = Math.Max(fromIdx, 21); i < bars.Count; i++)
        {
            EvaluateCross(item, st, bars, i, ma5, ma20, emit, tf, dayTrend, prevClose, vwap);
            if (bottom && tf == 1) EvaluateFollow(item, st, bars, i, emit);   // 직후 양봉은 1분봉 전용
            if (bottom) EvaluateBottom(item, st, bars, i, upper, lower, rsi, volMa, emit, tf, dayTrend, prevClose, vwap);
            if (top) EvaluateTop(item, st, bars, i, upper, lower, rsi, volMa, emit, tf);
        }
    }

    /// <summary>
    /// 1차 반등 직후 첫 완성봉의 양봉 지속(조기 확인) 판정 — 골든크로스보다 1~5분 빠른 힌트.
    /// 실측: 진짜 반등 3/4에서 직후 양봉, 가짜 2/3에서 직후 음봉/보합. 롤링 봉은 구간이 겹쳐
    /// 판정이 무뎌지므로 1분봉에서만 사용한다.
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

    /// <summary>MA5/MA20 관계를 봉마다 추적해 1차 시그널 후 확인 창(×tf) 내 크로스를 알림한다.</summary>
    private void EvaluateCross(WatchItem item, State st, List<Candle> bars, int i, double[] ma5, double[] ma20,
        Action<MinuteSignal> emit, int tf, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null)
    {
        if (double.IsNaN(ma5[i]) || double.IsNaN(ma20[i])) return;
        var bar = bars[i];
        bool below = ma5[i] <= ma20[i];

        // 확인 창 만료 처리 — 타임프레임 비례(15분봉이면 MA 크로스도 그만큼 느리다).
        int goldenWin = Math.Max(1, config.BottomConfirmWindowMinutes) * tf;
        int deadWin = Math.Max(1, config.TopConfirmWindowMinutes) * tf;
        if (st.AwaitGolden && (bar.Date - st.BottomFiredAt).TotalMinutes > goldenWin) st.AwaitGolden = false;
        if (st.AwaitDead && (bar.Date - st.TopFiredAt).TotalMinutes > deadWin) st.AwaitDead = false;

        if (st.HasPrevRel)
        {
            if (st.AwaitGolden && st.PrevBelow && !below)
            {
                st.AwaitGolden = false;
                // GC 모멘텀 등급: 1차 이후 상승률 — 실측상 건당 기대수익이 모멘텀 구간별 단조 증가
                // (0.8~1.5% +0.11 → 1.5~2.5% +0.26 → 2.5%+ +0.41%/건). 일봉 추세는 표기만(강등 아님).
                double rise = st.BottomFiredPrice > 0 ? (double)(bar.Close / st.BottomFiredPrice - 1) * 100 : 0;
                bool weakMomentum = rise < config.BottomGcMinRisePct;
                string reason = weakMomentum ? $" (모멘텀 {config.BottomGcMinRisePct:0.0#}% 미달 — 횡보성 크로스 주의)" : "";
                var kind = weakMomentum ? MinuteSignalKind.WeakGoldenCross
                    : rise >= config.BottomGcStrongPct ? MinuteSignalKind.StrongGoldenCross
                    : MinuteSignalKind.GoldenCross;
                emit(new MinuteSignal(item.Symbol, item.Name, kind, bar.Close,
                    $"MA5 {ma5[i]:N0} > MA20 {ma20[i]:N0} 돌파 · 1차({st.BottomFiredAt:HH:mm}) 후 {(bar.Date - st.BottomFiredAt).TotalMinutes:0}분 · " +
                    $"{rise:+0.00;-0.00}%{reason}", bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap),
                    AboveVwapAt(vwap, bar), st.BottomFiredDiv));
            }
            if (st.AwaitDead && !st.PrevBelow && below)
            {
                st.AwaitDead = false;
                emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.DeadCross, bar.Close,
                    $"MA5 {ma5[i]:N0} < MA20 {ma20[i]:N0} 돌파 · 경고({st.TopFiredAt:HH:mm}) 후 {(bar.Date - st.TopFiredAt).TotalMinutes:0}분", bar.Date, tf));
            }
        }
        st.PrevBelow = below;
        st.HasPrevRel = true;
    }

    /// <summary>시그널 봉 종가의 세션 VWAP 상/하 판정(널=VWAP 미계산).</summary>
    private static bool? AboveVwapAt(IReadOnlyDictionary<DateTime, double>? vwap, Candle bar)
        => vwap != null && vwap.TryGetValue(bar.Date, out var v) && !double.IsNaN(v) && v > 0
            ? (double)bar.Close >= v : null;

    /// <summary>사람 판단 보조 컨텍스트: 갭·당일 등락·저점比·VWAP 위치·일봉추세 — "폭락 후 V바닥 초입"인지 식별용.</summary>
    private string Context(List<Candle> bars, int i, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null)
    {
        var bar = bars[i];
        double dayChg = bars[0].Open > 0 ? (double)(bar.Close / bars[0].Open - 1) * 100 : 0;
        decimal dayLow = bars.Take(i + 1).Min(b => b.Low);
        double fromLow = dayLow > 0 ? (double)(bar.Close / dayLow - 1) * 100 : 0;
        string gap = prevClose > 0 && bars[0].Open > 0
            ? $"갭 {(double)(bars[0].Open / prevClose - 1) * 100:+0.0;-0.0}% · " : "";
        // VWAP 위치: 확인GC가 VWAP 위면 실측 승률 71%·낙폭 −0.97% (아래 46%·−2.19%) — 표기만, 강등 아님.
        string vw = "";
        if (vwap != null && vwap.TryGetValue(bar.Date, out var v) && !double.IsNaN(v) && v > 0)
            vw = $" · VWAP {((double)bar.Close >= v ? "위" : "아래")} {((double)bar.Close / v - 1) * 100:+0.0;-0.0}%";
        string trend = config.BottomTrendGate && dayTrend is { } dt ? $" · 일봉추세 {dt:+0.00;-0.00}" : "";
        return $"{gap}당일 {dayChg:+0.0;-0.0}% · 저점比 +{fromLow:0.0}%{vw}{trend}";
    }

    // ───────────────────────── 1차: 바닥 반등 ─────────────────────────

    /// <summary>완성봉 1개(인덱스 i)에 대한 바닥 반등 판정. 밴드워킹·%b 필터로 지속 하락·약반등을 걸러낸다.</summary>
    private void EvaluateBottom(WatchItem item, State st, List<Candle> bars, int i,
        double[] upper, double[] lower, double[] rsi, double[] volMa, Action<MinuteSignal> emit, int tf, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null)
    {
        var bar = bars[i];
        if (double.IsNaN(lower[i]) || double.IsNaN(upper[i]) ||
            double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 쿨다운(×tf): 최근 1차 알림 후 일정 시간 재알림 금지.
        if ((bar.Date - st.BottomFiredAt).TotalMinutes < Math.Max(1, config.BottomCooldownMinutes) * tf) return;

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
        for (int k = touchIdx; k <= i; k++)
            if (!double.IsNaN(rsi[k])) minRsi = Math.Min(minRsi, rsi[k]);
        // 거래량 급증 탐색: 기본은 터치 구간, BottomVolWindowBars>0이면 최근 N봉
        // — 투매 피크가 RSI 전환보다 몇 분 일러 터치 구간 밖으로 밀리는 케이스 포착(07-02 11:12 실측).
        long maxVol = 0;
        int volFrom = config.BottomVolWindowBars > 0 ? Math.Max(0, i - config.BottomVolWindowBars + 1) : touchIdx;
        for (int k = volFrom; k <= i; k++) maxVol = Math.Max(maxVol, bars[k].Volume);
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

        // RSI 불리시 다이버전스(참고 표기): 트리거 저점(최근 6봉)이 이전 스윙 저점(7~30봉 전)보다 낮은데
        // RSI는 더 높음 — 실측(1분 TF): 이후 확인GC 승률 67%(n=7)·평균 +3.11%로 유망하나 표본 부족 → 표기만.
        bool divergence = false;
        {
            int loIdx = i; decimal lo = decimal.MaxValue;
            for (int k = Math.Max(0, i - 5); k <= i; k++) if (bars[k].Low < lo) { lo = bars[k].Low; loIdx = k; }
            int prevIdx = -1; decimal prevLo = decimal.MaxValue;
            for (int k = Math.Max(0, i - 30); k <= i - 7; k++) if (bars[k].Low < prevLo) { prevLo = bars[k].Low; prevIdx = k; }
            if (prevIdx >= 0 && !double.IsNaN(rsi[loIdx]) && !double.IsNaN(rsi[prevIdx]))
                divergence = lo < prevLo && rsi[loIdx] > rsi[prevIdx] + 1;
        }

        st.BottomFiredAt = bar.Date;
        st.BottomFiredPrice = bar.Close;
        st.BottomFiredDiv = divergence;
        st.AwaitGolden = config.BottomConfirmCross;
        st.AwaitFollow = tf == 1 && config.BottomFollowCandle;
        st.AwaitDead = false;   // 방향 전환 — 반대편 확인 대기 해제
        // 일봉 추세·갭 등은 컨텍스트 줄로(강등 아님) — 폭락 직후가 오히려 V바닥 기회일 수 있다(실측 07-03 10:05).
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.Rebound, bar.Close,
            $"하단터치 {bars[touchIdx].Date:HH:mm} → 복귀 %b {pb:0.00} · RSI {rsi[i]:0.#}↑(저점 {minRsi:0.#}) · 거래량 {volRatio:0.0}×" +
            (divergence ? " · 다이버전스" : ""),
            bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap),
            AboveVwapAt(vwap, bar), divergence));
    }

    // ───────────────────────── 1차: 고점 경고 ─────────────────────────

    /// <summary>
    /// 완성봉 1개(인덱스 i)에 대한 고점 경고 판정(바닥의 거울상).
    /// 상단 밴드워킹(최소 봉 수) + RSI 과매수 → 밴드 안 복귀 하락봉 + RSI 하향 전환 + 소진 증거(윗꼬리/거래량).
    /// </summary>
    private void EvaluateTop(WatchItem item, State st, List<Candle> bars, int i,
        double[] upper, double[] lower, double[] rsi, double[] volMa, Action<MinuteSignal> emit, int tf)
    {
        var bar = bars[i];
        if (double.IsNaN(lower[i]) || double.IsNaN(upper[i]) ||
            double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 쿨다운(×tf · 고점 방향 별도).
        if ((bar.Date - st.TopFiredAt).TotalMinutes < Math.Max(1, config.TopCooldownMinutes) * tf) return;

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
            $"상단워킹 {touchCount}봉 → 이탈 %b {pb:0.00} · RSI {rsi[i]:0.#}↓(고점 {maxRsi:0.#}) · {evidence}", bar.Date, tf));
    }

    // ───────────────────────── 발생/전송 ─────────────────────────

    /// <summary>
    /// 적응형 폴링 제안(초). 1차 반등 후 확인 창 동안만 관찰 20초 / 주목(★3) 15초로 단축을 제안하고,
    /// GC 확인(어느 등급이든)·창 만료 시 기본으로 복귀한다. null=부스트 없음(기본 주기 사용).
    /// 분봉은 1분에 1개 완성되므로 상시 단축은 낭비 — 확인 창에서만 GC 포착 지연을 줄인다.
    /// </summary>
    public int? SuggestedPollSeconds
    {
        get
        {
            var now = DateTime.Now;
            int? best = null;
            foreach (var sd in _symbols.Values)
                if (sd.BoostLevel > 0 && sd.BoostUntil > now)
                    best = Math.Min(best ?? int.MaxValue, sd.BoostLevel >= 2 ? 15 : 20);
            return best;
        }
    }

    /// <summary>라이브 경로의 emit — 라이브 상태 갱신(부스트·교차) 후 트레이 이벤트 + Slack 전송.</summary>
    private void Fire(MinuteSignal s)
    {
        UpdateLiveState(s);
        Raised?.Invoke(s);
        _ = SafeSlackAsync(s);
    }

    /// <summary>
    /// 라이브 시그널에 따른 적응형 폴링 부스트와 🔁 전환 확인(교차) 판정.
    /// 부스트: 📈1차=20초 관찰(다이버전스면 15초), ↗직후양봉=15초 주목 — 확인 창(20분) 동안만.
    /// GC(등급 무관)가 뜨면 확인이 소비된 것이므로 기본 주기로 복귀.
    /// 교차: 고점 경고 시각을 기록해 두고, 반대 짝 종목(PairSymbol)의 반등 확인(✅🔥)이 15분 내
    /// 따라오면 경고 종목 명의로 전환 확인을 발화(쿨다운 30분 · 실측 93% 하락 근거).
    /// </summary>
    private void UpdateLiveState(MinuteSignal s)
    {
        if (!_symbols.TryGetValue(s.Code, out var sd)) return;
        var window = TimeSpan.FromMinutes(Math.Max(1, config.BottomConfirmWindowMinutes));

        switch (s.Kind)
        {
            case MinuteSignalKind.Rebound:
                sd.BoostLevel = Math.Max(sd.BoostLevel, s.Divergence ? 2 : 1);
                sd.BoostUntil = s.Time + window;
                break;
            case MinuteSignalKind.FollowThrough:
                sd.BoostLevel = 2;
                sd.BoostUntil = s.Time + window;
                break;
            case MinuteSignalKind.GoldenCross or MinuteSignalKind.StrongGoldenCross or MinuteSignalKind.WeakGoldenCross:
                sd.BoostLevel = 0;   // 확인 소비 → 기본 주기 복귀
                if (s.Kind != MinuteSignalKind.WeakGoldenCross) TryCrossTurn(s);
                break;
            case MinuteSignalKind.TopWarn:
                sd.LastTopWarnAt = s.Time;
                sd.BoostLevel = 0;   // 방향 전환 — 반등 확인 대기 부스트 해제
                break;
            case MinuteSignalKind.DeadCross:
                sd.BoostLevel = 0;
                break;
        }
    }

    /// <summary>반등 확인(✅🔥)이 뜬 종목의 반대 짝에서 15분 내 선행 고점 경고가 있었으면 전환 확인 발화.</summary>
    private void TryCrossTurn(MinuteSignal gc)
    {
        // 확인이 뜬 종목(gc.Code)을 짝으로 지정한 경고 종목을 찾는다(경고→확인 순서만 유효 — 역순은 실측 실패).
        var warnItem = config.Watchlist.FirstOrDefault(w =>
            string.Equals(w.PairSymbol, gc.Code, StringComparison.OrdinalIgnoreCase));
        if (warnItem is null || !_symbols.TryGetValue(warnItem.Symbol, out var wd)) return;

        double sinceWarn = (gc.Time - wd.LastTopWarnAt).TotalMinutes;
        if (sinceWarn is < 0 or > 15) return;
        if ((gc.Time - wd.LastCrossAt).TotalMinutes < 30) return;   // 쿨다운
        wd.LastCrossAt = gc.Time;

        decimal warnPrice = wd.Bars.Count > 0 ? wd.Bars.Values.Last().Close : 0;   // 경고(매도 대상) 종목 현재가
        Fire(new MinuteSignal(warnItem.Symbol, warnItem.Name, MinuteSignalKind.CrossTurn, warnPrice,
            $"고점 경고({wd.LastTopWarnAt:HH:mm}) 후 {sinceWarn:0}분 — 반대 종목 {gc.Display} 반등 확인 · 방향 전환 교차 검증",
            gc.Time, gc.Timeframe));
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
        _symbols.Clear();
        _day = today;
    }
}
