using Stock.Catch.Indicators;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 래더 계산 입력 파라미터(공격성·추세).
/// <para>BuyAggressiveness/SellStrength: 0=보수, 1=공격. UseTrend=true면 추세점수로 자동 산정.</para>
/// </summary>
public readonly record struct LadderParams(
    double BuyAggressiveness,
    double SellStrength,
    bool UseTrend,
    int HoldingQty = 0,
    decimal HoldingAvg = 0m)
{
    /// <summary>기본=보수(기존 v1.3.0 방법론과 동일한 오프셋).</summary>
    public static LadderParams Conservative => new(0.0, 0.0, false);
}

/// <summary>
/// stock-update 스킬의 매수/익절 래더 방법론에 <b>공격성·추세 반영</b>을 더한 계산기.
/// <list type="bullet">
/// <item>공격성 a=0(보수)에서는 기존 방법론과 동일: 1·2호가=저가변화율 P40/P25(cap0/floor−2),
///       3·4호가=σ_down 폭락안전망 min(−6,−1.5σ)/min(−10,−2.0σ).</item>
/// <item>a를 올리면 1·2호가는 목표 체결확률을 높여 더 얕게(전일저가 근처~약간 위), 3·4호가는
///       폭락안전망을 유지하되 깊이를 최대 30% 축소.</item>
/// <item>익절은 강도 s로 분위(도달확률)·평단가산%·ATR 배수를 보수→공격으로 보간.</item>
/// <item>UseTrend=true면 RSI 레벨·기울기·이격도·최근수익률 추세점수로 a/s를 자동 산정.</item>
/// </list>
/// 체결/도달 확률은 최근 변화율의 정규근사 CDF로 추정해 표시한다.
/// </summary>
public static class LadderCalculator
{
    /// <summary>최소 표본: 11거래일(→10개 일일변화율). 백분위·σ_down은 조회한 전체 기간으로 계산한다.</summary>
    public const int RequiredDays = 11;
    /// <summary>ATR 고정 기간(표준 14). 표본 윈도우와 무관하게 최근 14개 True Range로 계산.</summary>
    public const int AtrPeriod = 14;

    public static LadderResult Calculate(StockSeries series) => Calculate(series, LadderParams.Conservative);

    public static LadderResult Calculate(StockSeries series, LadderParams p)
    {
        var all = series.Candles;
        if (all.Count < RequiredDays)
            throw new InvalidOperationException(
                $"매수/익절 계산에는 최소 {RequiredDays}거래일이 필요합니다(현재 {all.Count}일). 기간을 늘려 다시 조회하세요.");

        // 표본 = 조회한 전체 기간(오래된→최신). 조회 기간을 바꾸면 표본 기간도 함께 바뀐다.
        var win = all;

        // 일일변화율(전체 기간 → all.Count−1개).
        var lowCh = new List<double>();
        var highCh = new List<double>();
        for (int i = 1; i < win.Count; i++)
        {
            lowCh.Add((double)(win[i].Low / win[i - 1].Low) - 1.0);
            highCh.Add((double)(win[i].High / win[i - 1].High) - 1.0);
        }

        var (meanLow, sdLow) = MeanStd(lowCh);
        var (meanHigh, sdHigh) = MeanStd(highCh);

        // σ_down: 음수 저가변화율(%)만의 RMS.
        var neg = lowCh.Where(x => x < 0).Select(x => x * 100).ToList();
        double sigma = neg.Count > 0 ? Math.Sqrt(neg.Sum(x => x * x) / neg.Count) : 0;

        // ── 추세 점수 → 공격성 자동 산정 ──
        var trend = ComputeTrend(all);
        double aBuy = p.UseTrend ? trend.AutoAggr : Math.Clamp(p.BuyAggressiveness, 0, 1);
        double aSell = p.UseTrend ? trend.AutoAggr : Math.Clamp(p.SellStrength, 0, 1);

        // ── 매수 오프셋 ──
        // 1·2호가: 목표 체결확률(분위)을 공격성으로 상향, 매수 상한(cap)도 완화.
        double f1 = Lerp(0.40, 0.62, aBuy);   // 1호가 목표 체결확률
        double f2 = Lerp(0.25, 0.45, aBuy);   // 2호가 목표 체결확률
        int maxOff1 = RoundAway(Lerp(0, 2, aBuy));    // 보수 ≤0, 공격 ≤+2(전일저가 약간 위 허용)
        int maxOff2 = RoundAway(Lerp(-2, 1, aBuy));
        int off1 = Math.Min(RoundAway(Percentile(lowCh, f1) * 100), maxOff1);
        int off2 = Math.Min(RoundAway(Percentile(lowCh, f2) * 100), maxOff2);
        // 3·4호가: σ_down 폭락안전망(공격성에 따라 깊이 최대 30% 축소·절대 플로어 완화).
        int floor3 = RoundAway(Lerp(-6, -3, aBuy));
        int floor4 = RoundAway(Lerp(-10, -6, aBuy));
        int off3 = Math.Min(floor3, RoundAway(-1.5 * sigma * (1 - 0.3 * aBuy)));
        int off4 = Math.Min(floor4, RoundAway(-2.0 * sigma * (1 - 0.3 * aBuy)));
        // 단조성 보정(1호가 ≥ 2 ≥ 3 ≥ 4).
        off2 = Math.Min(off2, off1);
        off3 = Math.Min(off3, off2);
        off4 = Math.Min(off4, off3);

        var offs = new[] { off1, off2, off3, off4 };

        var last = win[^1];
        decimal pLow = last.Low, pHigh = last.High, pClose = last.Close;

        var buys = offs.Select(o => Mround(pLow * (1 + (decimal)o / 100), 100)).ToArray();
        decimal avg = buys.Average();          // 신규 4주 래더 평단
        decimal total = buys.Sum();
        decimal gap = Mround(pClose * 0.95m, 100);

        // 보유 평단 반영: 보유 중이면 합산 평단(보유 + 신규4주)을 손절·익절 기준으로.
        decimal combinedAvg = avg;
        if (p.HoldingQty > 0 && p.HoldingAvg > 0)
            combinedAvg = (p.HoldingQty * p.HoldingAvg + total) / (p.HoldingQty + 4);
        decimal effAvg = combinedAvg;

        decimal stop = Mround(effAvg * 0.92m, 100);
        decimal loss = (effAvg - stop) * (p.HoldingQty + 4);

        // ── 매수 체결확률(정규근사: P(저가변화율 ≤ 오프셋)) ──
        double FillProb(int offPct) => sdLow <= 0
            ? (offPct / 100.0 >= meanLow ? 1.0 : 0.0)
            : NormCdf(((double)offPct / 100 - meanLow) / sdLow);
        var fillProbs = offs.Select(FillProb).ToArray();

        // ── 익절 4방식(강도 s 반영) ──
        double reachTarget = Lerp(0.80, 0.55, aSell);                 // 목표 도달확률
        int sellOff = RoundAway(Percentile(highCh, 1 - reachTarget) * 100);  // 방식 1·3 공통 분위
        double sellFloorPct = Lerp(8, 14, aSell);                    // 방식 2 평단 가산%
        double atrMult = Lerp(2.0, 3.0, aSell);                      // 방식 4 ATR 배수

        decimal Ret(decimal price) => effAvg == 0 ? 0 : price / effAvg - 1;
        // 매도 체결 = 당일 고가 ≥ 익절가 = 고가변화율 ≥ (익절가/전일고가 − 1).
        decimal ReachProb(decimal price)
        {
            double need = (double)(price / pHigh) - 1.0;
            double prob = sdHigh <= 0
                ? (need <= meanHigh ? 1.0 : 0.0)
                : 1.0 - NormCdf((need - meanHigh) / sdHigh);
            return (decimal)Math.Clamp(prob, 0, 1);
        }

        decimal sHigh = Mround(pHigh * (1 + (decimal)sellOff / 100), 100);
        decimal sFloor = Mround(effAvg * (1 + (decimal)sellFloorPct / 100), 100);
        decimal medHigh = Median(win.TakeLast(5).Select(c => c.High).ToList());
        decimal sRecent = Mround(medHigh * (1 + (decimal)sellOff / 100), 100);
        decimal atr = Atr(all.TakeLast(AtrPeriod + 1).ToList());   // 표본과 무관하게 최근 14 TR 고정
        decimal sAtr = Mround(effAvg + (decimal)atrMult * atr, 100);

        var targets = new[]
        {
            new SellTarget("전일고가 추종",      $"{pHigh:N0} × {sellOff}% · 도달 ~{reachTarget:P0}", sHigh,   Ret(sHigh),   ReachProb(sHigh)),
            new SellTarget($"평단 +{sellFloorPct:0.#}% 고정", "최소 수익 바닥(시장 무관)",            sFloor,  Ret(sFloor),  ReachProb(sFloor)),
            new SellTarget("최근5일 고가중앙값",  $"{medHigh:N0} × {sellOff}%",                       sRecent, Ret(sRecent), ReachProb(sRecent)),
            new SellTarget($"ATR×{atrMult:0.#} 변동성", $"평단 + {atrMult:0.#} × ATR({atr:N0})",       sAtr,    Ret(sAtr),    ReachProb(sAtr)),
        };

        return new LadderResult(
            series.Code, series.Name, win.Count,
            pLow, pHigh, pClose, gap,
            offs, buys, avg, total,
            stop, loss,
            sellOff, Math.Round(atr), targets,
            (decimal)Math.Round(sigma, 2),
            aBuy, aSell, fillProbs,
            trend.Score, trend.Label, p.UseTrend,
            p.HoldingQty, p.HoldingAvg, combinedAvg);
    }

    // ───────────────────────── 추세 점수 ─────────────────────────

    private readonly record struct TrendInfo(double Score, string Label, double AutoAggr);

    /// <summary>
    /// RSI 레벨·RSI 기울기·종가/MA20 이격도·최근5일 수익률을 [−1,+1]로 정규화해 평균.
    /// AutoAggr = clamp(0.5 + 0.5·점수)로 상승추세일수록 공격적.
    /// </summary>
    private static TrendInfo ComputeTrend(IReadOnlyList<Candle> all)
    {
        var ind = new IndicatorSet(all);
        int n = all.Count;
        double close = (double)all[^1].Close;
        var comps = new List<double>();
        var parts = new List<string>();

        double rsi = LastValid(ind.Rsi14);
        if (!double.IsNaN(rsi))
        {
            comps.Add(Math.Clamp((rsi - 50) / 25.0, -1, 1));
            parts.Add($"RSI {rsi:0}");
        }
        double slope = RsiSlope(ind.Rsi14, 5);
        if (!double.IsNaN(slope))
            comps.Add(Math.Clamp(slope / 3.0, -1, 1));   // 일 3pt 기울기 = 만점

        double ma20 = LastValid(ind.Sma20);
        if (!double.IsNaN(ma20) && ma20 > 0)
        {
            double disp = close / ma20 - 1;
            comps.Add(Math.Clamp(disp / 0.05, -1, 1));   // MA20 대비 ±5% = 만점
            parts.Add($"이격 {disp:+0.0%;-0.0%}");
        }
        if (n >= 6)
        {
            double c5 = (double)all[n - 6].Close;
            if (c5 > 0)
            {
                double ret5 = close / c5 - 1;
                comps.Add(Math.Clamp(ret5 / 0.08, -1, 1)); // 5일 ±8% = 만점
                parts.Add($"5일 {ret5:+0.0%;-0.0%}");
            }
        }

        double score = comps.Count > 0 ? comps.Average() : 0;
        string dir = score > 0.33 ? "상승" : score < -0.33 ? "하락" : "중립";
        string label = parts.Count > 0
            ? $"{dir} ({score:+0.00;-0.00}) · {string.Join(" · ", parts)}"
            : $"{dir} ({score:+0.00;-0.00})";
        double autoAggr = Math.Clamp(0.5 + 0.5 * score, 0, 1);
        return new TrendInfo(score, label, autoAggr);
    }

    private static double LastValid(double[] a)
    {
        for (int i = a.Length - 1; i >= 0; i--)
            if (!double.IsNaN(a[i])) return a[i];
        return double.NaN;
    }

    /// <summary>마지막 유효 RSI 최대 count개의 일 단위 선형회귀 기울기(pt/day).</summary>
    private static double RsiSlope(double[] rsi, int count)
    {
        var ys = new List<double>();
        for (int i = rsi.Length - 1; i >= 0 && ys.Count < count; i--)
            if (!double.IsNaN(rsi[i])) ys.Add(rsi[i]);
        if (ys.Count < 2) return double.NaN;
        ys.Reverse();   // 오래된→최신
        int m = ys.Count;
        double mx = (m - 1) / 2.0, my = ys.Average();
        double num = 0, den = 0;
        for (int i = 0; i < m; i++) { num += (i - mx) * (ys[i] - my); den += (i - mx) * (i - mx); }
        return den == 0 ? double.NaN : num / den;
    }

    // ───────────────────────── 통계/수학 ─────────────────────────

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static (double mean, double std) MeanStd(List<double> d)
    {
        if (d.Count == 0) return (0, 0);
        double m = d.Average();
        double v = d.Sum(x => (x - m) * (x - m)) / d.Count;
        return (m, Math.Sqrt(v));
    }

    /// <summary>표준정규 CDF(Abramowitz-Stegun erf 근사).</summary>
    private static double NormCdf(double z) => 0.5 * (1.0 + Erf(z / Math.Sqrt(2)));

    private static double Erf(double x)
    {
        double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));
        double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                          - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return Math.Sign(x) * y;
    }

    /// <summary>평균 True Range. TR = max(고−저, |고−전일종|, |저−전일종|).</summary>
    private static decimal Atr(List<Candle> win)
    {
        var trs = new List<decimal>();
        for (int i = 1; i < win.Count; i++)
        {
            decimal tr = Math.Max(win[i].High - win[i].Low,
                         Math.Max(Math.Abs(win[i].High - win[i - 1].Close),
                                  Math.Abs(win[i].Low - win[i - 1].Close)));
            trs.Add(tr);
        }
        return trs.Count > 0 ? trs.Average() : 0;
    }

    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0) return 0;
        var a = values.OrderBy(x => x).ToArray();
        int n = a.Length;
        return n % 2 == 1 ? a[n / 2] : (a[n / 2 - 1] + a[n / 2]) / 2;
    }

    /// <summary>Excel/Sheets MROUND(x, m): m 단위 반올림(0.5는 0에서 멀어지게).</summary>
    private static decimal Mround(decimal x, decimal m) => Math.Round(x / m, MidpointRounding.AwayFromZero) * m;

    private static int RoundAway(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

    /// <summary>Google PERCENTILE = PERCENTILE.INC: rank=p(n−1) 선형보간.</summary>
    private static double Percentile(List<double> data, double p)
    {
        if (data.Count == 0) return 0;
        var a = data.OrderBy(x => x).ToArray();
        int n = a.Length;
        if (n == 1) return a[0];
        double rank = Math.Clamp(p, 0, 1) * (n - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return a[lo];
        return a[lo] + (rank - lo) * (a[hi] - a[lo]);
    }
}
