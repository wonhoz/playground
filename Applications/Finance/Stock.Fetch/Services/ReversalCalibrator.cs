using Stock.Fetch.Indicators;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 반등 점수의 과거 적중률을 백테스트로 학습해 <see cref="ReversalCalibration"/>(신뢰도 곡선)을 만든다.
/// 관심 종목 전체의 과거 일봉에서 매일 <see cref="ReversalEstimator.ScoreAt"/> raw 점수를 계산하고,
/// 이후 K거래일 내 반등 방향으로 X% 이동했는지로 적중을 라벨링해 점수 10구간별 적중률을 집계한다.
/// 표본 부족 구간은 베이스 적중률로 평활하고, 점수↑→적중률↑ 단조성을 PAV로 보정한다.
/// </summary>
public sealed class ReversalCalibrator(PriceSourceRegistry registry)
{
    private const int Bins = 10;

    public async Task<ReversalCalibration> RunAsync(
        IReadOnlyList<WatchItem> items, int horizonDays = 5, double thresholdPct = 2.0,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        double x = thresholdPct / 100.0;
        var raws = new List<double>();
        var hits = new List<bool>();

        // 지수 제외, 중복 코드 제거
        var targets = items.Where(it => !it.IsIndex)
            .GroupBy(it => $"{it.Market}|{it.Symbol}").Select(g => g.First()).ToList();

        int done = 0;
        foreach (var item in targets)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"{++done}/{targets.Count} · {item.Symbol}");
            List<Candle> candles;
            try
            {
                candles = item.Market == MarketKind.US
                    ? await registry.UsDailyAsync(item.Symbol, "2y", ct)
                    : (await registry.KrDailyAsync(item.Symbol, 400, ct)).ToList();
            }
            catch { continue; }
            if (candles.Count < 40) continue;

            var ind = new IndicatorSet(candles);
            int last = candles.Count - 1 - horizonDays;
            for (int i = 20; i <= last; i++)
            {
                var (dir, raw, _) = ReversalEstimator.ScoreAt(candles, ind, i);
                decimal c0 = candles[i].Close;
                if (c0 <= 0) continue;
                bool hit;
                if (dir == ReversalDir.BottomUp)
                {
                    decimal hi = candles.Skip(i + 1).Take(horizonDays).Max(c => c.Close);
                    hit = hi >= c0 * (decimal)(1 + x);
                }
                else
                {
                    decimal loMin = candles.Skip(i + 1).Take(horizonDays).Min(c => c.Close);
                    hit = loMin <= c0 * (decimal)(1 - x);
                }
                raws.Add(raw);
                hits.Add(hit);
            }

            try { await Task.Delay(250, ct); } catch (OperationCanceledException) { break; }
        }

        return Build(raws, hits, horizonDays, thresholdPct);
    }

    private static ReversalCalibration Build(List<double> raws, List<bool> hits, int horizonDays, double thresholdPct)
    {
        var cal = new ReversalCalibration
        {
            HorizonDays = horizonDays,
            ThresholdPct = thresholdPct,
            TotalSamples = raws.Count,
            CreatedAt = DateTime.Now
        };
        if (raws.Count == 0) return cal;

        double baseRate = hits.Count(h => h) / (double)hits.Count;
        cal.BaseRate = baseRate;

        var binHit = new int[Bins];
        var binCnt = new int[Bins];
        for (int k = 0; k < raws.Count; k++)
        {
            int b = Math.Clamp((int)(raws[k] * Bins), 0, Bins - 1);
            binCnt[b]++;
            if (hits[k]) binHit[b]++;
        }

        // 평활(베이스 사전 m=20) → 구간 적중률
        const double m = 20;
        var rate = new double[Bins];
        for (int b = 0; b < Bins; b++)
            rate[b] = (binHit[b] + baseRate * m) / (binCnt[b] + m);

        // 단조 비감소 보정(PAV, count+사전 가중)
        var w = new double[Bins];
        for (int b = 0; b < Bins; b++) w[b] = binCnt[b] + m;
        PoolAdjacentViolators(rate, w);

        cal.BinRates = rate;
        cal.BinCounts = binCnt;
        return cal;
    }

    /// <summary>Pool Adjacent Violators — 가중 평균으로 비감소 단조화(제자리 갱신).</summary>
    private static void PoolAdjacentViolators(double[] y, double[] w)
    {
        int n = y.Length;
        var val = (double[])y.Clone();
        var wt = (double[])w.Clone();
        var cnt = new int[n];
        var idx = new int[n];
        int m = 0;
        for (int i = 0; i < n; i++)
        {
            val[m] = y[i]; wt[m] = w[i]; cnt[m] = 1; idx[m] = i;
            while (m > 0 && val[m - 1] > val[m])
            {
                double tw = wt[m - 1] + wt[m];
                val[m - 1] = (val[m - 1] * wt[m - 1] + val[m] * wt[m]) / tw;
                wt[m - 1] = tw; cnt[m - 1] += cnt[m];
                m--;
            }
            m++;
        }
        // 블록 값을 원래 위치에 펼침
        int pos = 0;
        for (int b = 0; b < m; b++)
            for (int c = 0; c < cnt[b]; c++)
                y[pos++] = Math.Clamp(val[b], 0, 1);
    }
}
