using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// stock-update 스킬의 매수/익절 래더 방법론을 그대로 구현한다(모드 B 오프셋 재산출 + 파생값).
/// - 매수 앵커=전일 저가(4호가, 보수+폭락 방어), 익절 앵커=전일 고가.
/// - 오프셋: 1·2호가=저가변화율 PERCENTILE(0.40/0.25) cap0/floor−2,
///           3·4호가=하방변동성 σ_down 연동 min(−6,−1.5σ)/min(−10,−2.0σ), 단조 보정.
/// - 익절오프셋=고가변화율 PERCENTILE(0.20). 가격은 100원 MROUND, 수량 1/1/1/1.
/// </summary>
public static class LadderCalculator
{
    /// <summary>방법론 전제: 11거래일(→10개 일일변화율).</summary>
    public const int RequiredDays = 11;

    public static LadderResult Calculate(StockSeries series)
    {
        var all = series.Candles;
        if (all.Count < RequiredDays)
            throw new InvalidOperationException(
                $"매수/익절 계산에는 최소 {RequiredDays}거래일이 필요합니다(현재 {all.Count}일). 기간을 늘려 다시 조회하세요.");

        // 최근 11거래일(오래된→최신).
        var win = all.Skip(all.Count - RequiredDays).ToList();

        // 일일변화율(10개).
        var lowCh = new List<double>();
        var highCh = new List<double>();
        for (int i = 1; i < win.Count; i++)
        {
            lowCh.Add((double)(win[i].Low / win[i - 1].Low) - 1.0);
            highCh.Add((double)(win[i].High / win[i - 1].High) - 1.0);
        }

        double p40 = Percentile(lowCh, 0.40) * 100;
        double p25 = Percentile(lowCh, 0.25) * 100;

        // σ_down: 음수 저가변화율(%)만의 RMS.
        var neg = lowCh.Where(x => x < 0).Select(x => x * 100).ToList();
        double sigma = neg.Count > 0 ? Math.Sqrt(neg.Sum(x => x * x) / neg.Count) : 0;

        int off1 = Math.Min(0, RoundAway(p40));
        int off2 = Math.Min(-2, RoundAway(p25));
        int off3 = Math.Min(-6, RoundAway(-1.5 * sigma));
        int off4 = Math.Min(-10, RoundAway(-2.0 * sigma));
        // 단조성 보정(1호가 ≥ 2 ≥ 3 ≥ 4): 위배 시 깊은 쪽으로 끌어내림.
        off2 = Math.Min(off2, off1);
        off3 = Math.Min(off3, off2);
        off4 = Math.Min(off4, off3);

        int sellOff = RoundAway(Percentile(highCh, 0.20) * 100);

        var last = win[^1];
        decimal pLow = last.Low, pHigh = last.High, pClose = last.Close;

        var offs = new[] { off1, off2, off3, off4 };
        var buys = offs.Select(o => Mround(pLow * (1 + (decimal)o / 100), 100)).ToArray();
        decimal avg = buys.Average();
        decimal total = buys.Sum();
        decimal stop = Mround(avg * 0.92m, 100);
        decimal loss = (avg - stop) * 4;
        decimal gap = Mround(pClose * 0.95m, 100);

        // ── 익절 4방식 ──
        decimal Ret(decimal price) => avg == 0 ? 0 : price / avg - 1;

        // 1) 전일고가 추종(P20, 도달 ~80%)
        decimal sHigh = Mround(pHigh * (1 + (decimal)sellOff / 100), 100);
        // 2) 평단 +8% 고정(최소 수익 바닥)
        decimal sFloor = Mround(avg * 1.08m, 100);
        // 3) 최근 5일 고가중앙값 추종(단일일 노이즈 완화)
        decimal medHigh = Median(win.TakeLast(5).Select(c => c.High).ToList());
        decimal sRecent = Mround(medHigh * (1 + (decimal)sellOff / 100), 100);
        // 4) ATR×2(변동성 기반): 평단 + 2×ATR
        decimal atr = Atr(win);
        decimal sAtr = Mround(avg + 2 * atr, 100);

        var targets = new[]
        {
            new SellTarget("전일고가 추종",      $"{pHigh:N0} × {sellOff}% · 도달 ~80%", sHigh,   Ret(sHigh)),
            new SellTarget("평단 +8% 고정",      "최소 수익 바닥(시장 무관)",            sFloor,  Ret(sFloor)),
            new SellTarget("최근5일 고가중앙값",  $"{medHigh:N0} × {sellOff}%",           sRecent, Ret(sRecent)),
            new SellTarget("ATR×2 변동성",        $"평단 + 2 × ATR({atr:N0})",            sAtr,    Ret(sAtr)),
        };

        return new LadderResult(
            series.Code, series.Name, win.Count,
            pLow, pHigh, pClose, gap,
            offs, buys, avg, total,
            stop, loss,
            sellOff, Math.Round(atr), targets,
            (decimal)Math.Round(sigma, 2));
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
        double rank = p * (n - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return a[lo];
        return a[lo] + (rank - lo) * (a[hi] - a[lo]);
    }
}
