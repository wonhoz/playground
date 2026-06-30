using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

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

        // 최신 체결가: latestTrade.p, 없으면 dailyBar.c 폴백.
        decimal price = SubDec(root, "latestTrade", "p");
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
}
