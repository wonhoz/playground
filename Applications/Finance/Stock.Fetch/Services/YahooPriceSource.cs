using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// Yahoo Finance 차트 API(v8) 기반 무인증 데이터 소스.
/// 국내 종목은 시장 접미사(.KS=코스피, .KQ=코스닥)가 필요하므로 둘 다 시도한다.
/// </summary>
public sealed class YahooPriceSource(HttpClient http) : IPriceSource
{
    public SourceKind Kind => SourceKind.Yahoo;
    public string DisplayName => "Yahoo Finance";
    public bool RequiresApiKey => false;
    public override string ToString() => DisplayName;

    public async Task<StockSeries> FetchAsync(string code, DateTime from, DateTime to, CancellationToken ct = default)
    {
        // .KS(코스피) → 실패 시 .KQ(코스닥) 순으로 시도.
        foreach (var (suffix, market) in new[] { (".KS", "KOSPI"), (".KQ", "KOSDAQ") })
        {
            var series = await TryFetchAsync(code, suffix, market, from, to, ct);
            if (series is { Candles.Count: > 0 }) return series;
        }
        throw new PriceSourceException($"Yahoo Finance에서 종목 '{code}'(.KS/.KQ)를 찾지 못했습니다.");
    }

    private async Task<StockSeries?> TryFetchAsync(string code, string suffix, string market,
        DateTime from, DateTime to, CancellationToken ct)
    {
        long p1 = ToUnix(from.Date);
        long p2 = ToUnix(to.Date.AddDays(1)); // 종료일 포함
        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{code}{suffix}" +
                     $"?period1={p1}&period2={p2}&interval=1d";

        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (resp.StatusCode is HttpStatusCode.NotFound) return null;
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Yahoo Finance 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement.GetProperty("chart");
        if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            return null;
        if (!root.TryGetProperty("result", out var resultArr) || resultArr.GetArrayLength() == 0)
            return null;

        var result = resultArr[0];
        if (!result.TryGetProperty("timestamp", out var ts) || ts.ValueKind != JsonValueKind.Array)
            return null;

        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var vols = quote.GetProperty("volume");

        string name = result.TryGetProperty("meta", out var meta) &&
                      meta.TryGetProperty("longName", out var ln) && ln.ValueKind == JsonValueKind.String
            ? ln.GetString()!
            : (meta.ValueKind != JsonValueKind.Undefined && meta.TryGetProperty("shortName", out var sn) && sn.ValueKind == JsonValueKind.String
                ? sn.GetString()! : string.Empty);

        var candles = new List<Candle>();
        int n = ts.GetArrayLength();
        for (int i = 0; i < n; i++)
        {
            // null 봉(거래정지 등) 방어
            if (closes[i].ValueKind == JsonValueKind.Null) continue;
            var date = DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64())
                .ToOffset(TimeSpan.FromHours(9)).Date; // KST 기준 거래일
            candles.Add(new Candle(
                date,
                GetDec(opens, i),
                GetDec(highs, i),
                GetDec(lows, i),
                GetDec(closes, i),
                vols[i].ValueKind == JsonValueKind.Number ? vols[i].GetInt64() : 0));
        }

        candles.Sort((a, b) => a.Date.CompareTo(b.Date));
        return new StockSeries(code, name, market, Kind, candles);
    }

    private static decimal GetDec(JsonElement arr, int i)
        => arr[i].ValueKind == JsonValueKind.Number ? Math.Round(arr[i].GetDecimal(), 2) : 0m;

    private static long ToUnix(DateTime dt)
        => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
