using System.Net.Http;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>차트 봉 주기.</summary>
public enum BarInterval
{
    Min1, Min5, Min15, Min30, Min60, Day, Week, Month
}

/// <summary>차트 데이터 소스.</summary>
public enum ChartSourceKind { Yahoo, Kis }

/// <summary>
/// 차트용 봉 데이터 조회. Yahoo는 분/일/주/월 전부(무인증), KIS는 일/주/월(API 키)만 지원한다.
/// 분봉은 Date에 시각까지 담는다.
/// </summary>
public sealed class ChartDataService(HttpClient http, PriceSourceRegistry registry)
{
    public static string Label(BarInterval iv) => iv switch
    {
        BarInterval.Min1 => "1분", BarInterval.Min5 => "5분", BarInterval.Min15 => "15분",
        BarInterval.Min30 => "30분", BarInterval.Min60 => "60분",
        BarInterval.Day => "일봉", BarInterval.Week => "주봉", BarInterval.Month => "월봉",
        _ => iv.ToString()
    };

    /// <summary>분봉 여부(x축 날짜 포맷·자동갱신 판단용).</summary>
    public static bool IsIntraday(BarInterval iv) =>
        iv is BarInterval.Min1 or BarInterval.Min5 or BarInterval.Min15 or BarInterval.Min30 or BarInterval.Min60;

    public async Task<List<Candle>> FetchAsync(string code, BarInterval iv, ChartSourceKind src, CancellationToken ct = default)
    {
        if (src == ChartSourceKind.Kis)
        {
            if (IsIntraday(iv))
            {
                // KIS는 당일 1분봉만 제공 → 1분봉을 요청 주기로 집계.
                var min1 = await registry.KisMinutesAsync(code, ct);
                int m = iv switch
                {
                    BarInterval.Min1 => 1, BarInterval.Min5 => 5, BarInterval.Min15 => 15,
                    BarInterval.Min30 => 30, BarInterval.Min60 => 60, _ => 1
                };
                return Aggregate(min1, m);
            }
            char p = iv switch { BarInterval.Week => 'W', BarInterval.Month => 'M', _ => 'D' };
            var (from, to) = KisRange(iv);
            return await registry.KisChartAsync(code, from, to, p, ct);
        }
        return await YahooAsync(code, iv, ct);
    }

    /// <summary>1분봉을 N분봉으로 집계(O=첫·H=최대·L=최소·C=마지막·V=합). 09:00 기준 N분 버킷.</summary>
    private static List<Candle> Aggregate(List<Candle> min1, int minutes)
    {
        if (minutes <= 1 || min1.Count == 0) return min1;
        var result = new List<Candle>();
        foreach (var g in min1.GroupBy(c => Bucket(c.Date, minutes)).OrderBy(g => g.Key))
        {
            var bars = g.OrderBy(c => c.Date).ToList();
            result.Add(new Candle(g.Key,
                bars[0].Open, bars.Max(b => b.High), bars.Min(b => b.Low),
                bars[^1].Close, bars.Sum(b => b.Volume)));
        }
        return result;
    }

    private static DateTime Bucket(DateTime t, int minutes)
    {
        int total = t.Hour * 60 + t.Minute;
        int floor = total / minutes * minutes;
        return t.Date.AddMinutes(floor);
    }

    // ────────────────────────────── Yahoo ──────────────────────────────
    private static (string interval, string range) YahooParams(BarInterval iv) => iv switch
    {
        BarInterval.Min1 => ("1m", "5d"),
        BarInterval.Min5 => ("5m", "1mo"),
        BarInterval.Min15 => ("15m", "1mo"),
        BarInterval.Min30 => ("30m", "2mo"),
        BarInterval.Min60 => ("60m", "3mo"),
        BarInterval.Day => ("1d", "1y"),
        BarInterval.Week => ("1wk", "5y"),
        BarInterval.Month => ("1mo", "10y"),
        _ => ("1d", "1y")
    };

    private async Task<List<Candle>> YahooAsync(string code, BarInterval iv, CancellationToken ct)
    {
        var (interval, range) = YahooParams(iv);
        // 코스피(.KS)/코스닥(.KQ)을 모두 조회해 봉 수가 많은 쪽을 채택한다.
        // (코스닥 종목을 .KS로 조회하면 Yahoo가 빈 응답이 아니라 소수의 가짜 봉을 주므로
        //  단순히 첫 비어있지 않은 결과를 쓰면 잘못된 시장 데이터가 잡힌다.)
        var ksTask = YahooFetchAsync(code, ".KS", interval, range, ct);
        var kqTask = YahooFetchAsync(code, ".KQ", interval, range, ct);
        await Task.WhenAll(ksTask, kqTask);
        var best = kqTask.Result.Count > ksTask.Result.Count ? kqTask.Result : ksTask.Result;
        if (best.Count == 0)
            throw new PriceSourceException($"Yahoo에서 종목 '{code}'(.KS/.KQ) 차트를 찾지 못했습니다.");
        return best;
    }

    private async Task<List<Candle>> YahooFetchAsync(string code, string suffix, string interval, string range, CancellationToken ct)
    {
        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{code}{suffix}?interval={interval}&range={range}";
        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Yahoo 차트 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var chart = doc.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null) return [];
        if (!chart.TryGetProperty("result", out var arr) || arr.GetArrayLength() == 0) return [];

        var result = arr[0];
        if (!result.TryGetProperty("timestamp", out var ts) || ts.ValueKind != JsonValueKind.Array) return [];
        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var vols = quote.GetProperty("volume");

        var candles = new List<Candle>();
        int n = ts.GetArrayLength();
        for (int i = 0; i < n; i++)
        {
            if (closes[i].ValueKind == JsonValueKind.Null) continue;
            // KST 기준 봉 시각(분봉은 시·분 포함)
            var date = DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).ToOffset(TimeSpan.FromHours(9)).DateTime;
            candles.Add(new Candle(
                date,
                GetDec(opens, i), GetDec(highs, i), GetDec(lows, i), GetDec(closes, i),
                vols[i].ValueKind == JsonValueKind.Number ? vols[i].GetInt64() : 0));
        }
        candles.Sort((a, b) => a.Date.CompareTo(b.Date));
        return candles;
    }

    private static decimal GetDec(JsonElement arr, int i)
        => arr[i].ValueKind == JsonValueKind.Number ? Math.Round(arr[i].GetDecimal(), 2) : 0m;

    // ────────────────────────────── KIS 기간 ──────────────────────────────
    private static (DateTime from, DateTime to) KisRange(BarInterval iv)
    {
        var to = DateTime.Today;
        var from = iv switch
        {
            BarInterval.Week => to.AddDays(-700),   // ~100주
            BarInterval.Month => to.AddMonths(-100),
            _ => to.AddDays(-200)                    // 일봉 ~130거래일
        };
        return (from, to);
    }
}
