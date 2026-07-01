using Stock.Fetch.Indicators;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 반등(반전) 확률을 여러 지표로 추정한다. 일봉(코드/일자별 캐시)에 현재가를 오늘 봉으로 병합해
/// RSI·볼린저·이격도·연속봉·캔들·거래량 신호(각 0~1)를 가중 결합한다. <b>휴리스틱 추정치</b>이며
/// 검증된 확률이 아니다. 최근 추세가 하락이면 바닥반등, 상승이면 천정반전 관점으로 평가한다.
/// </summary>
public sealed class ReversalEstimator(PriceSourceRegistry registry)
{
    private sealed class DailyCache { public List<Candle> Candles = new(); public bool Failed; }

    private readonly Dictionary<string, DailyCache> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Today);

    // 가중치(합 1.0)
    private const double WRsi = 0.30, WBoll = 0.25, WDisp = 0.15, WStreak = 0.15, WCandle = 0.10, WVol = 0.05;

    public async Task<ReversalEstimate?> EstimateAsync(WatchItem item, decimal price, CancellationToken ct = default)
    {
        if (item.IsIndex || price <= 0) return null;

        var baseC = await GetDailyAsync(item, ct);
        if (baseC is null || baseC.Count < 20) return null;

        var candles = MergeLive(baseC, price);
        var ind = new IndicatorSet(candles);
        int i = candles.Count - 1;

        double close = (double)candles[i].Close;
        double prevClose = (double)candles[i - 1].Close;
        double c5 = (double)candles[Math.Max(0, i - 5)].Close;
        bool bottom = close <= c5;   // 최근 5봉 하락 → 바닥반등 관점
        var dir = bottom ? ReversalDir.BottomUp : ReversalDir.TopDown;

        double rsi = At(ind.Rsi14, i), rsiPrev = At(ind.Rsi14, i - 1);
        double up = At(ind.BollUpper, i), lo = At(ind.BollLower, i);
        double sma20 = At(ind.Sma20, i);
        double vol = (double)candles[i].Volume, volMa = At(ind.VolumeMa20, i);
        double open = (double)candles[i].Open, high = (double)candles[i].High, low = (double)candles[i].Low;
        double range = high - low;
        double pctB = (up - lo) > 0 ? (close - lo) / (up - lo) : double.NaN;
        double disp = (!double.IsNaN(sma20) && sma20 > 0) ? close / sma20 - 1 : double.NaN;
        double pos = range > 0 ? (close - low) / range : 0.5;
        bool bull = close >= open;

        var parts = new List<(double contrib, string label)>();
        double p;

        if (bottom)
        {
            double rsiSig = double.IsNaN(rsi) ? 0 : Clamp01((40 - rsi) / 25);
            if (!double.IsNaN(rsiPrev) && rsi > rsiPrev) rsiSig = Math.Min(1, rsiSig + 0.15);
            double bollSig = double.IsNaN(pctB) ? 0 : Clamp01((0.25 - pctB) / 0.45);
            double dispSig = double.IsNaN(disp) ? 0 : Clamp01(-disp / 0.06);
            int streak = DownStreak(candles, i);
            double streakSig = Clamp01(streak / 4.0) * (close > prevClose ? 1.0 : 0.3);
            double candleSig = bull ? Clamp01((pos - 0.5) / 0.4) : 0;
            double volSig = (!double.IsNaN(volMa) && volMa > 0) ? Clamp01((vol / volMa - 1.2) / 1.3) : 0;

            p = WRsi * rsiSig + WBoll * bollSig + WDisp * dispSig + WStreak * streakSig + WCandle * candleSig + WVol * volSig;
            AddPart(parts, WRsi * rsiSig, double.IsNaN(rsi) ? "RSI" : $"RSI {rsi:0}");
            AddPart(parts, WBoll * bollSig, "볼린저 하단");
            AddPart(parts, WDisp * dispSig, double.IsNaN(disp) ? "이격" : $"이격 {disp:+0.0%;-0.0%}");
            AddPart(parts, WStreak * streakSig, $"연속 {streak}일 하락 후 반등");
            AddPart(parts, WCandle * candleSig, "반등 캔들");
            AddPart(parts, WVol * volSig, "거래량 급증");
        }
        else
        {
            double rsiSig = double.IsNaN(rsi) ? 0 : Clamp01((rsi - 60) / 25);
            if (!double.IsNaN(rsiPrev) && rsi < rsiPrev) rsiSig = Math.Min(1, rsiSig + 0.15);
            double bollSig = double.IsNaN(pctB) ? 0 : Clamp01((pctB - 0.75) / 0.45);
            double dispSig = double.IsNaN(disp) ? 0 : Clamp01(disp / 0.06);
            int streak = UpStreak(candles, i);
            double streakSig = Clamp01(streak / 4.0) * (close < prevClose ? 1.0 : 0.3);
            double candleSig = !bull ? Clamp01((0.5 - pos) / 0.4) : 0;
            double volSig = (!double.IsNaN(volMa) && volMa > 0) ? Clamp01((vol / volMa - 1.2) / 1.3) : 0;

            p = WRsi * rsiSig + WBoll * bollSig + WDisp * dispSig + WStreak * streakSig + WCandle * candleSig + WVol * volSig;
            AddPart(parts, WRsi * rsiSig, double.IsNaN(rsi) ? "RSI" : $"RSI {rsi:0}");
            AddPart(parts, WBoll * bollSig, "볼린저 상단");
            AddPart(parts, WDisp * dispSig, double.IsNaN(disp) ? "이격" : $"이격 {disp:+0.0%;-0.0%}");
            AddPart(parts, WStreak * streakSig, $"연속 {streak}일 상승 후 눌림");
            AddPart(parts, WCandle * candleSig, "반전 캔들");
            AddPart(parts, WVol * volSig, "거래량 급증");
        }

        string detail = parts.Count > 0
            ? string.Join("·", parts.OrderByDescending(x => x.contrib).Take(2).Select(x => x.label))
            : "지표 신호 약함";
        return new ReversalEstimate(dir, Clamp01(p), detail);
    }

    // ───────────────────────── 일봉 캐시 ─────────────────────────
    private async Task<List<Candle>?> GetDailyAsync(WatchItem item, CancellationToken ct)
    {
        ResetIfNewDay();
        string key = $"{item.Market}|{item.Symbol}";
        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out var c)) return c.Failed ? null : c.Candles;
            var entry = new DailyCache();
            try
            {
                entry.Candles = item.Market == MarketKind.US
                    ? await registry.UsDailyAsync(item.Symbol, "6mo", ct)
                    : (await registry.KrDailyAsync(item.Symbol, 120, ct)).ToList();
                if (entry.Candles.Count < 20) entry.Failed = true;
            }
            catch { entry.Failed = true; }
            _cache[key] = entry;
            return entry.Failed ? null : entry.Candles;
        }
        finally { _gate.Release(); }
    }

    private static List<Candle> MergeLive(List<Candle> baseC, decimal price)
    {
        var list = new List<Candle>(baseC);
        var last = list[^1];
        list[^1] = last with
        {
            Close = price,
            High = Math.Max(last.High, price),
            Low = last.Low <= 0 ? price : Math.Min(last.Low, price)
        };
        return list;
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today == _day) return;
        // 캐시 접근은 GetDailyAsync 내부에서 gate로 보호되지만, 초기화도 안전하게.
        _gate.Wait();
        try { if (today != _day) { _cache.Clear(); _day = today; } }
        finally { _gate.Release(); }
    }

    // ───────────────────────── 헬퍼 ─────────────────────────
    private static double At(double[] a, int i) => (i >= 0 && i < a.Length) ? a[i] : double.NaN;
    private static double Clamp01(double x) => Math.Clamp(x, 0, 1);

    private static int DownStreak(List<Candle> c, int i)
    {
        int s = 0;
        for (int k = i - 1; k >= 1; k--) { if (c[k].Close < c[k - 1].Close) s++; else break; }
        return s;
    }

    private static int UpStreak(List<Candle> c, int i)
    {
        int s = 0;
        for (int k = i - 1; k >= 1; k--) { if (c[k].Close > c[k - 1].Close) s++; else break; }
        return s;
    }

    private static void AddPart(List<(double, string)> parts, double contrib, string label)
    {
        if (contrib > 0.02) parts.Add((contrib, label));
    }
}
