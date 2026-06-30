using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// Finnhub 실시간 시세 소스(미국 종목용). 무료 API 키로 quote 엔드포인트를 호출한다.
/// 응답: c(현재가)·dp(전일 대비 등락율%)·pc(전일 종가). 관심 종목 폴링 전용.
/// </summary>
public sealed class FinnhubPriceSource(AppConfig config, HttpClient http)
{
    public async Task<Quote> FetchQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!config.HasFinnhubKey)
            throw new PriceSourceException("Finnhub API Key가 설정되지 않았습니다. 설정에서 입력하세요.");

        string sym = symbol.Trim().ToUpperInvariant();
        string url = $"https://finnhub.io/api/v1/quote?symbol={Uri.EscapeDataString(sym)}&token={config.FinnhubApiKey}";

        string text;
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new PriceSourceException("Finnhub 호출 한도를 초과했습니다(무료 60회/분). 잠시 후 다시 시도하세요.");
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new PriceSourceException("Finnhub API Key가 올바르지 않습니다.");
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (PriceSourceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Finnhub 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        decimal price = Dec(root, "c");
        if (price <= 0)
            throw new PriceSourceException($"Finnhub에서 종목 '{sym}' 시세를 찾지 못했습니다(티커를 확인하세요).");
        decimal rate = Dec(root, "dp"); // 전일 대비 등락율(%)
        return new Quote(sym, price, rate, DateTime.Now);
    }

    private static decimal Dec(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
}
