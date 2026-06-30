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

    /// <summary>
    /// 현재가 스냅샷(미국 종목 등 접미사 없는 글로벌 티커용). includePrePost로 프리/정규/애프터마켓을 모두 받아
    /// marketState에 맞는 가격을 선택하고, 전일 종가(chartPreviousClose) 대비 등락율을 계산한다.
    /// </summary>
    public async Task<Quote> FetchQuoteAsync(string symbol, CancellationToken ct = default)
    {
        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=1d&interval=1d&includePrePost=true";
        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (resp.StatusCode is HttpStatusCode.NotFound)
                throw new PriceSourceException($"Yahoo Finance에서 종목 '{symbol}'을(를) 찾지 못했습니다.");
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (PriceSourceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Yahoo Finance 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement.GetProperty("chart");
        if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            throw new PriceSourceException($"Yahoo Finance 응답 오류: 종목 '{symbol}'.");
        if (!root.TryGetProperty("result", out var resultArr) || resultArr.GetArrayLength() == 0)
            throw new PriceSourceException($"Yahoo Finance에서 종목 '{symbol}' 데이터가 없습니다.");

        var meta = resultArr[0].GetProperty("meta");
        decimal reg = MetaDec(meta, "regularMarketPrice");
        string state = meta.TryGetProperty("marketState", out var ms) && ms.ValueKind == JsonValueKind.String
            ? ms.GetString()! : "";

        // 세션에 맞는 현재가 선택: 프리마켓→preMarketPrice, 애프터마켓→postMarketPrice, 그 외→정규가.
        decimal price = state switch
        {
            "PRE" or "PREPRE" => Pick(MetaDec(meta, "preMarketPrice"), reg),
            "POST" or "POSTPOST" or "POSTCLOSE" => Pick(MetaDec(meta, "postMarketPrice"), reg),
            _ => reg
        };
        if (price <= 0) price = reg;

        decimal prev = MetaDec(meta, "chartPreviousClose");
        if (prev <= 0) prev = MetaDec(meta, "previousClose");
        decimal rate = prev > 0 ? Math.Round((price / prev - 1) * 100, 2) : 0m;
        return new Quote(symbol, price, rate, DateTime.Now);
    }

    private static decimal Pick(decimal primary, decimal fallback) => primary > 0 ? primary : fallback;

    private static decimal MetaDec(JsonElement meta, string prop)
        => meta.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;

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
