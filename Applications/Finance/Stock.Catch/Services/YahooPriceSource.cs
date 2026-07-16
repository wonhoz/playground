using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

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

    // ───────────────────────── 급등락 전광판(미국 · 폴백 스크리너) ─────────────────────────

    /// <summary>
    /// 미국 사전정의 스크리너 — day_gainers / day_losers / most_actives. Alpaca 키가 없을 때의
    /// 폴백(15분 지연 가능). 값이 raw 숫자 또는 {raw,fmt} 객체 두 형태로 오므로 둘 다 처리한다.
    /// </summary>
    public async Task<List<MoverRow>> FetchPredefinedMoversAsync(string scrId, int count = 30, CancellationToken ct = default)
    {
        string url = $"https://query1.finance.yahoo.com/v1/finance/screener/predefined/saved?scrIds={scrId}&count={count}";
        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new PriceSourceException($"Yahoo 스크리너 응답 오류(HTTP {(int)resp.StatusCode}) — Alpaca 키를 설정하면 실시간 스크리너를 사용합니다.");
        }
        catch (PriceSourceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Yahoo 스크리너 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var rows = new List<MoverRow>();
        if (!doc.RootElement.TryGetProperty("finance", out var fin) ||
            !fin.TryGetProperty("result", out var resArr) || resArr.ValueKind != JsonValueKind.Array ||
            resArr.GetArrayLength() == 0 ||
            !resArr[0].TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
            return rows;

        foreach (var q in quotes.EnumerateArray())
        {
            string sym = q.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
            if (sym.Length == 0) continue;
            string name = q.TryGetProperty("shortName", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "";
            rows.Add(new MoverRow(rows.Count + 1, sym, name,
                (decimal)RawNum(q, "regularMarketPrice"),
                RawNum(q, "regularMarketChangePercent"),
                (long)RawNum(q, "regularMarketVolume")));
        }
        return rows;
    }

    /// <summary>스크리너 필드 값 — raw 숫자 또는 {raw,fmt} 객체 모두 지원.</summary>
    private static double RawNum(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("raw", out var r) && r.ValueKind == JsonValueKind.Number)
            return r.GetDouble();
        return 0;
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

    /// <summary>
    /// 접미사 없는 글로벌 티커(미국 등)의 일봉을 조회한다(반등/지표 계산용). 최근 range 구간, interval=1d.
    /// 실패·데이터 없음 시 빈 리스트.
    /// </summary>
    public async Task<List<Candle>> FetchDailyCandlesAsync(string symbol, string range = "6mo", CancellationToken ct = default)
    {
        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range={range}&interval=1d";
        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (resp.StatusCode is HttpStatusCode.NotFound) return new();
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Yahoo 일봉 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement.GetProperty("chart");
        if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null) return new();
        if (!root.TryGetProperty("result", out var resultArr) || resultArr.GetArrayLength() == 0) return new();

        var result = resultArr[0];
        if (!result.TryGetProperty("timestamp", out var ts) || ts.ValueKind != JsonValueKind.Array) return new();

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
            // 미국 거래일 그대로(ET). 지표는 순서만 중요하므로 UTC Date 사용.
            var date = DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime.Date;
            candles.Add(new Candle(date,
                GetDec(opens, i), GetDec(highs, i), GetDec(lows, i), GetDec(closes, i),
                vols[i].ValueKind == JsonValueKind.Number ? vols[i].GetInt64() : 0));
        }
        candles.Sort((a, b) => a.Date.CompareTo(b.Date));
        return candles;
    }

    private static decimal GetDec(JsonElement arr, int i)
        => arr[i].ValueKind == JsonValueKind.Number ? Math.Round(arr[i].GetDecimal(), 2) : 0m;

    private static long ToUnix(DateTime dt)
        => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
