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

    /// <summary>🔁 전환 확인: 고점 경고 후 반대 짝 반등 확인을 인정하는 최대 지연(분). 실측 근거.</summary>
    private const int CrossWindowMinutes = 15;
    /// <summary>🔁 전환 확인 재발화 쿨다운(분).</summary>
    private const int CrossCooldownMinutes = 30;

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
        public bool BottomFiredLowConv;                            // 1차가 애매(고낙폭 · 합류 게이트 대상)였는지 — 직후양봉 상속
        // 🚀 진입 적기(3차) 대기: GC 후 N봉 지속 확인
        public bool AwaitHold;
        public DateTime GcHoldAt = DateTime.MinValue;
        public decimal GcHoldPrice;
        public bool GcHoldStrong;                                  // GC가 🔥였는지(문구용)
        public bool GcHoldVwap;                                    // GC 시점 VWAP 위였는지(등급 상속)
        public bool GcHoldDiv;
        // 고점(경고) 상태
        public DateTime TopFiredAt = DateTime.MinValue;
        public bool AwaitDead;                                     // 2차(데드크로스) 확인 대기
        // MA5/MA20 관계 추적(골든·데드 공용)
        public bool PrevBelow;                                     // 직전 완성봉 MA5≤MA20 여부
        public bool HasPrevRel;
        // 📦 박스 상단 돌파(진입 권장) 추적: GC/🚀 직후 가변 박스
        public bool BoxActive;
        public DateTime BoxStart = DateTime.MinValue;              // 박스 시작(GC) 봉 시각
        public decimal BoxHi, BoxLo;                               // 박스 상/하단(확장)
        public int BoxBars;                                        // 시작 후 경과 완성봉 수
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
        => Backtest(minuteBars, new WatchItem { Symbol = code, Name = name }, dayTrend, prevClose);

    /// <summary>
    /// 종목별 시그널 override(<see cref="WatchItem"/>)를 그대로 반영하는 백테스트 — 라이브와 완전히 동일한
    /// 유효 설정으로 판정한다. 분봉 시그널 분석에서 관심 종목 설정까지 재현할 때 사용.
    /// </summary>
    public List<MinuteSignal> Backtest(IReadOnlyList<Candle> minuteBars, WatchItem item,
        double? dayTrend = null, decimal prevClose = 0m)
    {
        var bars = minuteBars.OrderBy(b => b.Date).ToList();
        var result = new List<MinuteSignal>();
        if (bars.Count < 26) return result;

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

    /// <summary>
    /// 이동평균 리본(5/20/60/120) 스프레드% = (max−min)/종가 × 100. 4개 MA가 모두 서기 전(≈120봉·11시 이후)은 NaN.
    /// 작을수록 리본 밀집(저변동·눌림 다지기) — 실측상 그 자리 진입은 이후 낙폭이 작아 버티기 쉽다.
    /// </summary>
    private static double[] BuildRibbon(List<double> closes)
    {
        var m5 = IndicatorMath.Sma(closes, 5);
        var m20 = IndicatorMath.Sma(closes, 20);
        var m60 = IndicatorMath.Sma(closes, 60);
        var m120 = IndicatorMath.Sma(closes, 120);
        var r = new double[closes.Count];
        for (int i = 0; i < closes.Count; i++)
        {
            if (double.IsNaN(m5[i]) || double.IsNaN(m20[i]) || double.IsNaN(m60[i]) || double.IsNaN(m120[i]) || closes[i] <= 0)
            { r[i] = double.NaN; continue; }
            double mx = Math.Max(Math.Max(m5[i], m20[i]), Math.Max(m60[i], m120[i]));
            double mn = Math.Min(Math.Min(m5[i], m20[i]), Math.Min(m60[i], m120[i]));
            r[i] = (mx - mn) / closes[i] * 100;
        }
        return r;
    }

    /// <summary>ribbon 배열에서 인덱스 i의 리본 스프레드%를 안전하게 얻는다(없으면 NaN).</summary>
    private static double RibbonAt(double[]? ribbon, int i)
        => ribbon != null && i >= 0 && i < ribbon.Length ? ribbon[i] : double.NaN;

    /// <summary>
    /// 역추세 플래그 배열: MA20·MA60의 5봉 기울기가 <b>둘 다 음(하락 중)</b>이면 true. MA60 미형성(≈10시 전)이면 false.
    /// 하락하는 중기 이평으로의 역추세 반등(예: 07-06 KODEX 10:19 MA20 −0.15·MA60 −0.22 → −1.9%)은 🚀 실패가 잦다.
    /// </summary>
    private static bool[] BuildCounterTrend(List<double> closes, double[] ma20)
    {
        var ma60 = IndicatorMath.Sma(closes, 60);
        var r = new bool[closes.Count];
        const int k = 5;   // 기울기 측정 구간(봉)
        for (int i = 0; i < closes.Count; i++)
        {
            if (i < k) continue;
            double s20 = !double.IsNaN(ma20[i]) && !double.IsNaN(ma20[i - k]) && ma20[i - k] != 0 ? ma20[i] - ma20[i - k] : double.NaN;
            double s60 = !double.IsNaN(ma60[i]) && !double.IsNaN(ma60[i - k]) && ma60[i - k] != 0 ? ma60[i] - ma60[i - k] : double.NaN;
            r[i] = !double.IsNaN(s20) && !double.IsNaN(s60) && s20 < 0 && s60 < 0;
        }
        return r;
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
        // 짧은 볼린저 병행(개장 급락으로 넓어진 20-밴드가 놓치는 두 번째 저점 포착) — 셋업 하단으로만 사용.
        double[]? loShort = config.BottomShortBandPeriod > 1
            ? IndicatorMath.Bollinger(closes, config.BottomShortBandPeriod, 2).Lower : null;
        // 이동평균 리본(5/20/60/120) 밀집도 — 밀집=진입 후 낙폭 작아 버티기 쉬움(실측 −0.77% vs 분산 −2.40%).
        // 컨텍스트·태그 표기 전용(강등 아님). 1분봉에서만(상위 TF는 120봉=120×tf분이라 의미가 흐려짐).
        double[]? ribbon = tf == 1 ? BuildRibbon(closes) : null;
        // ⚠ 역추세: MA20·MA60이 동시 하락(5봉 기울기<0) 중인 자리 — 🚀 진입 적기가 여기서 뜨면 실패 잦음
        // (실측 66건: 둘 다 하락 시 순상승 48%·낙폭≤−2% 27.6% vs 정렬 75%·0%). MA60 형성(≈10시) 후만.
        bool[]? counterTrend = tf == 1 ? BuildCounterTrend(closes, ma20) : null;

        for (int i = Math.Max(fromIdx, 21); i < bars.Count; i++)
        {
            EvaluateCross(item, st, bars, i, ma5, ma20, emit, tf, dayTrend, prevClose, vwap, ribbon);
            if (bottom) EvaluateHold(item, st, bars, i, emit, tf, dayTrend, prevClose, vwap, ribbon, counterTrend);   // 🚀 진입 적기(GC 후 지속 확인)
            if (bottom && tf == 1 && config.BoxBreakoutAlert) EvaluateBox(item, st, bars, i, emit, tf, dayTrend, prevClose, vwap, ribbon, counterTrend);   // 📦 진입 권장(박스 상단 돌파)
            if (bottom && tf == 1) EvaluateFollow(item, st, bars, i, emit, ribbon);   // 직후 양봉은 1분봉 전용
            if (bottom) EvaluateBottom(item, st, bars, i, upper, lower, rsi, volMa, emit, tf, dayTrend, prevClose, vwap, loShort, ribbon, counterTrend);
            if (top) EvaluateTop(item, st, bars, i, upper, lower, rsi, volMa, emit, tf);
        }
    }

    /// <summary>
    /// 1차 반등 직후 첫 완성봉의 양봉 지속(조기 확인) 판정 — 골든크로스보다 1~5분 빠른 힌트.
    /// 실측: 진짜 반등 3/4에서 직후 양봉, 가짜 2/3에서 직후 음봉/보합. 롤링 봉은 구간이 겹쳐
    /// 판정이 무뎌지므로 1분봉에서만 사용한다.
    /// </summary>
    private void EvaluateFollow(WatchItem item, State st, List<Candle> bars, int i, Action<MinuteSignal> emit, double[]? ribbon = null)
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
            $"1차({st.BottomFiredAt:HH:mm}) 직후 양봉 지속 · 몸통 {ratio:0.0}×", bar.Date,
            RibbonSpreadPct: RibbonAt(ribbon, i), LowConviction: st.BottomFiredLowConv));
    }

    /// <summary>
    /// 🚀 진입 적기(3차): GC(✅🔥) 후 config.BottomHoldConfirmBars봉(×tf)이 지난 첫 완성봉에서
    /// 종가가 GC 가격 이상이면 발화(추세 지속 확인). 미달이면 조용히 취소 — "무작정 GC 진입" 오탐 차단.
    /// 실측: GC 즉시 승률 57%/오탐 16% → +2봉 지속확인 82%/2%.
    /// </summary>
    private void EvaluateHold(WatchItem item, State st, List<Candle> bars, int i, Action<MinuteSignal> emit,
        int tf, double? dayTrend, decimal prevClose, IReadOnlyDictionary<DateTime, double>? vwap, double[]? ribbon = null,
        bool[]? counterTrend = null)
    {
        if (!st.AwaitHold) return;
        var bar = bars[i];
        if ((bar.Date - st.GcHoldAt).TotalMinutes < Math.Max(1, config.BottomHoldConfirmBars) * tf) return;   // 아직 관찰 중
        st.AwaitHold = false;   // 관찰 창 종료 — 1회 판정
        if (st.GcHoldPrice <= 0 || bar.Close < st.GcHoldPrice) return;   // 추세 미지속 → 진입 부적합, 조용히 취소

        double hold = (double)(bar.Close / st.GcHoldPrice - 1) * 100;
        bool chase = IsChaseWarn(vwap, bar);
        bool ct = counterTrend != null && i < counterTrend.Length && counterTrend[i];   // MA20·60 동시 하락(역추세)
        double stopPct = chase || ct ? config.BottomStopLossChasePct : config.BottomStopLossPct;   // 흔들림 주의·역추세는 더 깊게
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.HoldConfirm, bar.Close,
            $"{(st.GcHoldStrong ? "🔥" : "✅")} 확인({st.GcHoldAt:HH:mm}) 후 {(bar.Date - st.GcHoldAt).TotalMinutes:0}분 종가 유지 · {hold:+0.00;-0.00}% — 추세 지속 확인",
            bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap, RibbonAt(ribbon, i)), st.GcHoldVwap, st.GcHoldDiv, chase, stopPct, RibbonAt(ribbon, i), ct));
    }

    /// <summary>
    /// 📦 진입 권장(박스 상단 돌파): GC 직후 가변 박스(시드 BoxSeedBars봉 후 계속 확장)를 추적하다,
    /// 종가가 박스 상단(그동안의 최고가)을 돌파하면 발화 — "흔들림 통과 진입". 하단 이탈은 침묵(박스만 넓힘).
    /// BoxMaxBars(×tf) 안에 돌파 없으면 폐기. 손절선은 박스 하단(자연스러운 무효선). 실측: GC 즉시 대비 순상승↑·낙폭 위험 절반.
    /// </summary>
    private void EvaluateBox(WatchItem item, State st, List<Candle> bars, int i, Action<MinuteSignal> emit,
        int tf, double? dayTrend, decimal prevClose, IReadOnlyDictionary<DateTime, double>? vwap,
        double[]? ribbon, bool[]? counterTrend)
    {
        if (!st.BoxActive) return;
        var bar = bars[i];
        if (bar.Date <= st.BoxStart) return;   // 시드(GC) 봉 자체는 건너뜀
        st.BoxBars++;

        // 타임아웃: 정해진 봉 수 내 돌파 없으면 폐기.
        if (st.BoxBars > Math.Max(1, config.BoxMaxBars)) { st.BoxActive = false; return; }

        // 시드 구간: 박스만 형성(확장), 돌파 판정 보류.
        if (st.BoxBars <= Math.Max(1, config.BoxSeedBars))
        {
            if (bar.High > st.BoxHi) st.BoxHi = bar.High;
            if (bar.Low < st.BoxLo) st.BoxLo = bar.Low;
            return;
        }

        // 상단 돌파(종가 > 박스 최고가) = 진입 권장. 하단 이탈은 침묵(박스만 확장 · 흔들기 바닥일 때 다수).
        if (bar.Close > st.BoxHi)
        {
            st.BoxActive = false;
            double stopPct = st.BoxLo > 0 && st.BoxLo < bar.Close
                ? (double)((bar.Close - st.BoxLo) / bar.Close) * 100    // 손절선 = 박스 하단(무효선)
                : config.BottomStopLossPct;
            bool ct = counterTrend != null && i < counterTrend.Length && counterTrend[i];
            emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.BoxBreakout, bar.Close,
                $"박스({st.BoxStart:HH:mm}~{(bar.Date - st.BoxStart).TotalMinutes:0}분) 상단 {st.BoxHi:N0} 돌파 · 흔들림 통과 진입 권장",
                bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap, RibbonAt(ribbon, i)),
                AboveVwapAt(vwap, bar), false, IsChaseWarn(vwap, bar), stopPct, RibbonAt(ribbon, i), ct));
            return;
        }
        if (bar.High > st.BoxHi) st.BoxHi = bar.High;
        if (bar.Low < st.BoxLo) st.BoxLo = bar.Low;
    }

    // ───────────────────────── 2차: 골든/데드크로스 확인 ─────────────────────────

    /// <summary>MA5/MA20 관계를 봉마다 추적해 1차 시그널 후 확인 창(×tf) 내 크로스를 알림한다.</summary>
    private void EvaluateCross(WatchItem item, State st, List<Candle> bars, int i, double[] ma5, double[] ma20,
        Action<MinuteSignal> emit, int tf, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null, double[]? ribbon = null)
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
                double eGcMin = item.BottomGcMinRisePct ?? config.BottomGcMinRisePct;
                double eGcStrong = item.BottomGcStrongPct ?? config.BottomGcStrongPct;
                bool weakMomentum = rise < eGcMin;
                string reason = weakMomentum ? $" (모멘텀 {eGcMin:0.0#}% 미달 — 횡보성 크로스 주의)" : "";
                var kind = weakMomentum ? MinuteSignalKind.WeakGoldenCross
                    : rise >= eGcStrong ? MinuteSignalKind.StrongGoldenCross
                    : MinuteSignalKind.GoldenCross;
                bool aboveVwap = AboveVwapAt(vwap, bar) == true;
                bool chase = IsChaseWarn(vwap, bar);
                emit(new MinuteSignal(item.Symbol, item.Name, kind, bar.Close,
                    $"MA5 {ma5[i]:N0} > MA20 {ma20[i]:N0} 돌파 · 1차({st.BottomFiredAt:HH:mm}) 후 {(bar.Date - st.BottomFiredAt).TotalMinutes:0}분 · " +
                    $"{rise:+0.00;-0.00}%{reason}", bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap, RibbonAt(ribbon, i)),
                    AboveVwapAt(vwap, bar), st.BottomFiredDiv, chase, RibbonSpreadPct: RibbonAt(ribbon, i)));
                // 🚀 진입 적기(3차) 대기 시작 — 약한 GC(횡보성)는 제외.
                if (!weakMomentum && config.BottomHoldConfirmBars > 0)
                {
                    st.AwaitHold = true; st.GcHoldAt = bar.Date; st.GcHoldPrice = bar.Close;
                    st.GcHoldStrong = kind == MinuteSignalKind.StrongGoldenCross;
                    st.GcHoldVwap = aboveVwap; st.GcHoldDiv = st.BottomFiredDiv;
                }
                // 📦 박스 상단 돌파(진입 권장) 추적 시작 — GC 봉을 시드로(약한 GC 제외 · 1분봉 전용).
                if (!weakMomentum && tf == 1 && config.BoxBreakoutAlert)
                {
                    st.BoxActive = true; st.BoxStart = bar.Date; st.BoxHi = bar.High; st.BoxLo = bar.Low; st.BoxBars = 0;
                }
            }
            if (st.AwaitDead && !st.PrevBelow && below)
            {
                st.AwaitDead = false;
                st.AwaitHold = false;   // 하락 전환 — 진입 대기 취소
                st.BoxActive = false;   // 하락 전환 — 박스 추적 취소
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

    /// <summary>
    /// ⚠ 흔들림 주의: 종가가 세션 VWAP보다 설정 임계 이상 아래(하락 추세 진행 중)면 true.
    /// 실측: VWAP 깊은 약세 진입은 진입 후 낙폭이 커 버티기 어렵다(GC/🚀 확인 위험 표기용 · 강등 아님).
    /// </summary>
    private bool IsChaseWarn(IReadOnlyDictionary<DateTime, double>? vwap, Candle bar)
    {
        if (config.BottomChaseVwapBelowPct <= 0 || vwap is null) return false;
        if (!vwap.TryGetValue(bar.Date, out var v) || double.IsNaN(v) || v <= 0) return false;
        return ((double)bar.Close / v - 1) * 100 <= -config.BottomChaseVwapBelowPct;
    }

    /// <summary>사람 판단 보조 컨텍스트: 갭·당일 등락·저점比·VWAP 위치·일봉추세 — "폭락 후 V바닥 초입"인지 식별용.</summary>
    private string Context(List<Candle> bars, int i, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null, double ribbon = double.NaN)
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
        // 리본(5/20/60/120) 밀집도 — 밀집=진입 후 낙폭 작음(버티기 쉬움)·분산=낙폭 큼. MA120 미형성(≈11시 전)이면 생략.
        string rib = !double.IsNaN(ribbon)
            ? $" · 리본 {ribbon:0.0}%({(ribbon <= MinuteSignal.RibbonTightPct ? "밀집" : ribbon >= MinuteSignal.RibbonWidePct ? "분산" : "보통")})" : "";
        string trend = config.BottomTrendGate && dayTrend is { } dt ? $" · 일봉추세 {dt:+0.00;-0.00}" : "";
        return $"{gap}당일 {dayChg:+0.0;-0.0}% · 저점比 +{fromLow:0.0}%{vw}{rib}{trend}";
    }

    // ───────────────────────── 1차: 바닥 반등 ─────────────────────────

    /// <summary>완성봉 1개(인덱스 i)에 대한 바닥 반등 판정. 밴드워킹·%b 필터로 지속 하락·약반등을 걸러낸다.</summary>
    private void EvaluateBottom(WatchItem item, State st, List<Candle> bars, int i,
        double[] upper, double[] lower, double[] rsi, double[] volMa, Action<MinuteSignal> emit, int tf, double? dayTrend, decimal prevClose,
        IReadOnlyDictionary<DateTime, double>? vwap = null, double[]? loShort = null, double[]? ribbon = null, bool[]? counterTrend = null)
    {
        var bar = bars[i];
        if (double.IsNaN(lower[i]) || double.IsNaN(upper[i]) ||
            double.IsNaN(rsi[i]) || double.IsNaN(rsi[i - 1]) || double.IsNaN(volMa[i])) return;

        // 종목별 유효 파라미터(override ?? 전역) — 종목 성격에 맞춘 개별 최적화.
        double eRsiMax = item.BottomRsiMax ?? config.BottomRsiMax;
        double eVolRatio = item.BottomVolumeRatio ?? config.BottomVolumeRatio;
        int eWalkMax = item.BottomWalkMaxTouches ?? config.BottomWalkMaxTouches;
        double eMinPb = item.BottomMinPercentB ?? config.BottomMinPercentB;

        // 쿨다운(×tf): 최근 1차 알림 후 일정 시간 재알림 금지.
        if ((bar.Date - st.BottomFiredAt).TotalMinutes < Math.Max(1, config.BottomCooldownMinutes) * tf) return;

        // 밴드워킹 필터: 최근 WalkWindow봉 중 하단 터치가 임계 초과면 완만한 지속 하락으로 보고 스킵.
        int walkTouches = 0;
        for (int k = Math.Max(0, i - WalkWindow + 1); k <= i; k++)
            if (!double.IsNaN(lower[k]) && (double)bars[k].Low <= lower[k]) walkTouches++;
        if (walkTouches > Math.Max(1, eWalkMax)) return;

        // 셋업: 최근 lookback봉 내 볼린저 하단 터치(저가 ≤ 20-하단 OR 짧은-하단 — 이중 바닥 포착).
        int lookback = Math.Max(2, config.BottomTouchLookback);
        int touchIdx = -1;
        for (int k = Math.Max(1, i - lookback); k <= i; k++)
        {
            bool t20 = !double.IsNaN(lower[k]) && (double)bars[k].Low <= lower[k];
            bool tSh = loShort != null && !double.IsNaN(loShort[k]) && (double)bars[k].Low <= loShort[k];
            if (t20 || tSh) { touchIdx = k; break; }   // 가장 이른 터치 봉
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
        if (minRsi > eRsiMax) return;
        if (rsi[i] <= rsi[i - 1]) return;

        // 트리거: 밴드 안 복귀 + %b 하한(밴드폭 대비 회복 비율) + 상승봉.
        double width = upper[i] - lower[i];
        if (width <= 0) return;
        double pb = ((double)bar.Close - lower[i]) / width;
        if (pb < eMinPb) return;
        if (bar.Close <= bar.Open && bar.Close <= bars[i - 1].Close) return;

        // 트리거: 터치 구간 거래량 급증(20봉 평균 대비 배수).
        if (volMa[i] <= 0) return;
        double volRatio = maxVol / volMa[i];
        if (volRatio < eVolRatio) return;

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

        // 합류 게이트용 애매(고낙폭) 판정: 리본 분산(≥3%) OR (역추세[MA20·60 동시 하락] & VWAP 아래).
        // 실측(15일 704건): 이 자리 낙폭≤−2% 34~50%(통과분 21%). 알림만 억제(상태·확정신호는 유지 — Fire에서 게이팅).
        double ribNow = RibbonAt(ribbon, i);
        bool ct = counterTrend != null && i < counterTrend.Length && counterTrend[i];
        bool ribWide = !double.IsNaN(ribNow) && ribNow >= MinuteSignal.RibbonWidePct;
        bool belowVwap = AboveVwapAt(vwap, bar) == false;
        bool lowConv = ribWide || (ct && belowVwap);

        st.BottomFiredAt = bar.Date;
        st.BottomFiredPrice = bar.Close;
        st.BottomFiredDiv = divergence;
        st.BottomFiredLowConv = lowConv;
        st.AwaitGolden = config.BottomConfirmCross;
        st.AwaitFollow = tf == 1 && config.BottomFollowCandle;
        st.AwaitDead = false;   // 방향 전환 — 반대편 확인 대기 해제
        // 일봉 추세·갭 등은 컨텍스트 줄로(강등 아님) — 폭락 직후가 오히려 V바닥 기회일 수 있다(실측 07-03 10:05).
        emit(new MinuteSignal(item.Symbol, item.Name, MinuteSignalKind.Rebound, bar.Close,
            $"하단터치 {bars[touchIdx].Date:HH:mm} → 복귀 %b {pb:0.00} · RSI {rsi[i]:0.#}↑(저점 {minRsi:0.#}) · 거래량 {volRatio:0.0}×" +
            (divergence ? " · 다이버전스" : ""),
            bar.Date, tf, Context(bars, i, dayTrend, prevClose, vwap, RibbonAt(ribbon, i)),
            AboveVwapAt(vwap, bar), divergence, RibbonSpreadPct: RibbonAt(ribbon, i), LowConviction: lowConv));
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
        if (maxRsi < (item.TopRsiMin ?? config.TopRsiMin)) return;
        if (rsi[i] >= rsi[i - 1]) return;

        // 트리거: 밴드 안 복귀 마감(%b 상한) + 하락봉.
        double width = upper[i] - lower[i];
        if (width <= 0) return;
        double pb = ((double)bar.Close - lower[i]) / width;
        if (pb > config.TopMaxPercentB) return;
        if (bar.Close >= bar.Open && bar.Close >= bars[i - 1].Close) return;

        // 트리거: 소진 증거 — 긴 윗꼬리 또는 클라이맥스 거래량 중 1개 이상.
        double volRatio = volMa[i] > 0 ? maxVol / volMa[i] : 0;
        bool climax = volRatio >= (item.TopVolumeRatio ?? config.TopVolumeRatio);
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
        st.AwaitHold = false;     // 과열 전환 — 진입 대기 취소
        st.BoxActive = false;     // 과열 전환 — 박스 추적 취소
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

    /// <summary>종목별 직전 전송 알림(판정 문구·시각) — 중복(같은 내용 [3분][5분]) 억제용.</summary>
    private readonly Dictionary<string, (string Verdict, DateTime At)> _lastAlert = new();

    /// <summary>
    /// 라이브 경로의 emit — 라이브 상태 갱신(부스트·교차)은 항상 하고, 알림(트레이+Slack)은 중복이 아닐 때만 보낸다.
    /// 여러 타임프레임(1·3·5분)이 같은 반등/GC를 동시 발화해 같은 판정 문구가 연달아 오는 것을 막는다
    /// — 내용(VerdictLine)이 바뀌면 즉시 전송. 상태 갱신은 스킵과 무관(교차·부스트 로직 보존).
    /// </summary>
    private void Fire(MinuteSignal s)
    {
        UpdateLiveState(s);   // 부스트·교차 등 상태는 억제 여부와 무관하게 항상 갱신
        if (IsLowConvictionSuppressed(s)) return;   // 애매(고낙폭) 반등 1차·직후양봉 알림 억제(합류 게이트)
        if (IsDuplicateAlert(s)) return;
        _lastAlert[s.Code] = (s.VerdictLine, s.Time);
        Raised?.Invoke(s);
        _ = SafeSlackAsync(s);
    }

    /// <summary>
    /// 합류 게이트: 애매(고낙폭) 반등 1차(<see cref="MinuteSignalKind.Rebound"/>)·직후양봉(<see cref="MinuteSignalKind.FollowThrough"/>)
    /// 알림을 억제한다 — 리본 분산 또는 역추세+VWAP아래 자리. 상태(<see cref="UpdateLiveState"/>·확인 대기)는 이미 갱신됐으므로
    /// 뒤이어 확정되는 GC·🚀·📦 알림은 그대로 발화한다("애매한 건 줄이고, 확실할 때만"). 기본 켬.
    /// </summary>
    private bool IsLowConvictionSuppressed(MinuteSignal s)
        => config.ReboundHighConvictionGate && s.LowConviction
           && s.Kind is MinuteSignalKind.Rebound or MinuteSignalKind.FollowThrough;

    /// <summary>직전에 보낸 알림과 판정 문구가 완전히 동일하고 dedup 창(분) 이내면 중복으로 본다(브리핑 제외).</summary>
    private bool IsDuplicateAlert(MinuteSignal s)
    {
        if (s.Kind == MinuteSignalKind.MorningBrief || config.SignalDedupWindowMinutes <= 0) return false;
        if (!_lastAlert.TryGetValue(s.Code, out var last)) return false;
        double gap = (s.Time - last.At).TotalMinutes;
        return last.Verdict == s.VerdictLine && gap >= 0 && gap <= config.SignalDedupWindowMinutes;
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

    /// <summary>반등 확인(✅🔥)이 뜬 종목의 반대 짝에서 15분 내 선행 고점 경고가 있었으면 전환 확인 발화(라이브).</summary>
    private void TryCrossTurn(MinuteSignal gc)
    {
        // 확인이 뜬 종목(gc.Code)을 짝으로 지정한 경고 종목을 찾는다(경고→확인 순서만 유효 — 역순은 실측 실패).
        var warnItem = config.Watchlist.FirstOrDefault(w =>
            string.Equals(w.PairSymbol, gc.Code, StringComparison.OrdinalIgnoreCase));
        if (warnItem is null || !_symbols.TryGetValue(warnItem.Symbol, out var wd)) return;

        double sinceWarn = (gc.Time - wd.LastTopWarnAt).TotalMinutes;
        if (sinceWarn is < 0 or > CrossWindowMinutes) return;
        if ((gc.Time - wd.LastCrossAt).TotalMinutes < CrossCooldownMinutes) return;   // 쿨다운
        wd.LastCrossAt = gc.Time;

        decimal warnPrice = wd.Bars.Count > 0 ? wd.Bars.Values.Last().Close : 0;   // 경고(매도 대상) 종목 현재가
        Fire(BuildCrossTurn(warnItem.Symbol, warnItem.Name, warnPrice, wd.LastTopWarnAt, gc, sinceWarn));
    }

    /// <summary>🔁 전환 확인 MinuteSignal 생성 — 라이브·백테스트 공용(포맷 일치).</summary>
    private static MinuteSignal BuildCrossTurn(string warnCode, string warnName, decimal warnPrice,
        DateTime warnTime, MinuteSignal confirm, double sinceWarn)
        => new(warnCode, warnName, MinuteSignalKind.CrossTurn, warnPrice,
            $"고점 경고({warnTime:HH:mm}) 후 {sinceWarn:0}분 — 반대 종목 {confirm.Display} 반등 확인 · 방향 전환 교차 검증",
            confirm.Time, confirm.Timeframe);

    /// <summary>
    /// 두 짝 종목(A·B) 시그널 목록에서 🔁 전환 확인(교차)을 검출한다 — 라이브 TryCrossTurn과 동일 규칙
    /// (경고→확인 순서 · 15분 창 · 30분 쿨다운 · 경고 종목 명의)을 시각순 재생으로 재현. 양방향 모두 판정.
    /// 반환은 발화 종목 Code로 구분되며, 호출 측이 종목별로 귀속시킨다.
    /// </summary>
    public static List<MinuteSignal> DetectCrossTurns(
        IReadOnlyList<MinuteSignal> sigsA, IReadOnlyList<Candle> barsA, string codeA, string nameA,
        IReadOnlyList<MinuteSignal> sigsB, IReadOnlyList<Candle> barsB, string codeB, string nameB)
    {
        var result = new List<MinuteSignal>();
        result.AddRange(CrossOneWay(sigsA, barsA, codeA, nameA, sigsB));   // A 경고 + B 확인 → A 명의
        result.AddRange(CrossOneWay(sigsB, barsB, codeB, nameB, sigsA));   // B 경고 + A 확인 → B 명의
        return result;
    }

    private static IEnumerable<MinuteSignal> CrossOneWay(
        IReadOnlyList<MinuteSignal> warnSigs, IReadOnlyList<Candle> warnBars, string warnCode, string warnName,
        IReadOnlyList<MinuteSignal> confirmSigs)
    {
        var warns = warnSigs.Where(s => s.Kind == MinuteSignalKind.TopWarn)
            .Select(s => s.Time).Distinct().OrderBy(t => t).ToList();
        if (warns.Count == 0) yield break;
        var confirms = confirmSigs
            .Where(s => s.Kind is MinuteSignalKind.GoldenCross or MinuteSignalKind.StrongGoldenCross)
            .OrderBy(s => s.Time).ThenBy(s => s.Timeframe).ToList();   // 라이브처럼 1분봉이 먼저 소비

        var sorted = warnBars.OrderBy(b => b.Date).ToList();
        var lastCross = DateTime.MinValue;
        foreach (var c in confirms)
        {
            DateTime? lastWarn = null;
            foreach (var w in warns) { if (w <= c.Time) lastWarn = w; else break; }   // 확인 직전 최근 경고
            if (lastWarn is not { } wt) continue;
            double since = (c.Time - wt).TotalMinutes;
            if (since > CrossWindowMinutes) continue;
            if ((c.Time - lastCross).TotalMinutes < CrossCooldownMinutes) continue;
            lastCross = c.Time;
            yield return BuildCrossTurn(warnCode, warnName, PriceAt(sorted, c.Time), wt, c, since);
        }
    }

    /// <summary>시각 t 시점(포함)의 마지막 완성봉 종가. bars는 시각 오름차순.</summary>
    private static decimal PriceAt(List<Candle> bars, DateTime t)
    {
        decimal p = 0;
        foreach (var b in bars) { if (b.Date <= t) p = b.Close; else break; }
        return p;
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
        _lastAlert.Clear();
        _day = today;
    }
}
