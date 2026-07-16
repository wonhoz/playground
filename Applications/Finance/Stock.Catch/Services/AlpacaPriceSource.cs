using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// Alpaca 실시간 시세 소스(미국 종목용). 무료 키는 IEX 피드를 사용한다. snapshot 엔드포인트 1회 호출로
/// 최신 체결가(latestTrade.p)와 전일 봉 종가(prevDailyBar.c)를 받아 등락율을 계산한다.
/// </summary>
public sealed class AlpacaPriceSource(AppConfig config, HttpClient http)
{
    public async Task<Quote> FetchQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!config.HasAlpacaKeys)
            throw new PriceSourceException("Alpaca API Key ID/Secret이 설정되지 않았습니다. 설정에서 입력하세요.");

        string sym = symbol.Trim().ToUpperInvariant();
        string url = $"https://data.alpaca.markets/v2/stocks/{Uri.EscapeDataString(sym)}/snapshot?feed=iex";

        string text;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("APCA-API-KEY-ID", config.AlpacaApiKeyId);
            req.Headers.Add("APCA-API-SECRET-KEY", config.AlpacaApiSecret);
            using var resp = await http.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new PriceSourceException("Alpaca 호출 한도를 초과했습니다. 잠시 후 다시 시도하세요.");
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new PriceSourceException("Alpaca API Key ID/Secret이 올바르지 않습니다.");
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (PriceSourceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Alpaca 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        // 최신 체결가: latestTrade.p(세션 무관 마지막 체결). 프리/애프터엔 IEX 체결이 드물 수 있어
        // 호가(latestQuote) 중간값으로 보완하고, 그래도 없으면 당일 정규봉 종가로 폴백.
        decimal price = SubDec(root, "latestTrade", "p");
        if (price <= 0)
        {
            decimal ask = SubDec(root, "latestQuote", "ap");
            decimal bid = SubDec(root, "latestQuote", "bp");
            if (ask > 0 && bid > 0) price = Math.Round((ask + bid) / 2, 2);
            else if (ask > 0) price = ask;
            else if (bid > 0) price = bid;
        }
        if (price <= 0) price = SubDec(root, "dailyBar", "c");
        if (price <= 0)
            throw new PriceSourceException($"Alpaca에서 종목 '{sym}' 시세를 찾지 못했습니다(티커를 확인하세요).");

        // 전일 종가: prevDailyBar.c (장중 등락율 기준)
        decimal prev = SubDec(root, "prevDailyBar", "c");
        decimal rate = prev > 0 ? Math.Round((price / prev - 1) * 100, 2) : 0m;
        return new Quote(sym, price, rate, DateTime.Now);
    }

    private static decimal SubDec(JsonElement root, string obj, string prop)
        => root.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object
           && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal() : 0m;

    // ───────────────────────── 급등락 전광판(미국 · 실시간 스크리너) ─────────────────────────

    /// <summary>
    /// 미국 급등/급락 상위 — Alpaca Screener movers(v1beta1 · 실시간). 정규장 지연이 있는 Yahoo와 달리
    /// 실시간 기준 등락률 순위를 준다. gainers/losers 배열에서 해당 방향만 추린다.
    /// </summary>
    public async Task<List<MoverRow>> FetchMoversAsync(bool gainers, int top = 30, CancellationToken ct = default)
    {
        using var doc = await GetDocAsync($"https://data.alpaca.markets/v1beta1/screener/stocks/movers?top={top}", ct);
        var rows = new List<MoverRow>();
        if (!doc.RootElement.TryGetProperty(gainers ? "gainers" : "losers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return rows;
        foreach (var o in arr.EnumerateArray())
        {
            string sym = o.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
            if (sym.Length == 0) continue;
            decimal price = o.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0m;
            double chg = o.TryGetProperty("percent_change", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
            rows.Add(new MoverRow(rows.Count + 1, sym, "", price, chg, 0));
        }
        return rows;
    }

    /// <summary>
    /// 미국 최다 거래 상위 — Alpaca Screener most-actives(by=volume · 실시간) + 스냅샷 멀티 조회 1회로
    /// 현재가·등락률을 보강한다(스크리너 응답엔 거래량·건수만 있음).
    /// </summary>
    public async Task<List<MoverRow>> FetchMostActivesAsync(int top = 30, CancellationToken ct = default)
    {
        using var doc = await GetDocAsync($"https://data.alpaca.markets/v1beta1/screener/stocks/most-actives?by=volume&top={top}", ct);
        var actives = new List<(string Sym, long Vol)>();
        if (doc.RootElement.TryGetProperty("most_actives", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var o in arr.EnumerateArray())
            {
                string sym = o.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                long vol = o.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
                if (sym.Length > 0) actives.Add((sym, vol));
            }
        if (actives.Count == 0) return new List<MoverRow>();

        // 스냅샷 멀티 조회(1콜): 현재가(latestTrade.p)·전일 종가(prevDailyBar.c) → 등락률.
        var quotes = new Dictionary<string, (decimal Price, double Chg)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string symbols = string.Join(",", actives.Select(a => Uri.EscapeDataString(a.Sym)));
            using var snap = await GetDocAsync($"https://data.alpaca.markets/v2/stocks/snapshots?symbols={symbols}&feed=iex", ct);
            foreach (var prop in snap.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                decimal price = SubDec(prop.Value, "latestTrade", "p");
                if (price <= 0) price = SubDec(prop.Value, "dailyBar", "c");
                decimal prev = SubDec(prop.Value, "prevDailyBar", "c");
                quotes[prop.Name] = (price, prev > 0 && price > 0 ? (double)Math.Round((price / prev - 1) * 100, 2) : 0);
            }
        }
        catch { /* 스냅샷 보강 실패 시 거래량만 표시 */ }

        return actives.Select((a, i) =>
        {
            var q = quotes.GetValueOrDefault(a.Sym);
            return new MoverRow(i + 1, a.Sym, "", q.Price, q.Chg, a.Vol);
        }).ToList();
    }

    /// <summary>Alpaca GET 공용 — 키 헤더·상태 코드 검사. 반환 JsonDocument는 호출 측이 dispose.</summary>
    private async Task<JsonDocument> GetDocAsync(string url, CancellationToken ct)
    {
        if (!config.HasAlpacaKeys)
            throw new PriceSourceException("Alpaca API Key ID/Secret이 설정되지 않았습니다. 설정에서 입력하세요.");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("APCA-API-KEY-ID", config.AlpacaApiKeyId);
        req.Headers.Add("APCA-API-SECRET-KEY", config.AlpacaApiSecret);
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            throw new PriceSourceException("Alpaca 호출 한도를 초과했습니다. 잠시 후 다시 시도하세요.");
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new PriceSourceException("Alpaca API Key ID/Secret이 올바르지 않습니다.");
        string text = await resp.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(text);
    }
}
