using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 다음(Daum) 금융 차트 API 기반 무인증 데이터 소스.
/// 종료일(to) + 개수(limit) 방식이라 긴 기간은 종료일을 과거로 당기며 페이징한다.
/// Referer 헤더가 없으면 차단되므로 요청마다 finance.daum.net Referer를 붙인다.
/// 다음도 KRX 원천 시세라 종가/고가/저가는 거래소 공식값과 일치한다.
/// </summary>
public sealed class DaumPriceSource(HttpClient http) : IPriceSource
{
    private const int PageLimit = 500; // 1회 최대 봉 수(약 2년치)

    public SourceKind Kind => SourceKind.Daum;
    public string DisplayName => "다음 금융";
    public bool RequiresApiKey => false;
    public override string ToString() => DisplayName;

    public async Task<StockSeries> FetchAsync(string code, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var map = new SortedDictionary<DateTime, Candle>();
        DateTime cursor = to;
        int guard = 0; // 무한루프 방지(최대 ~40페이지)

        while (cursor >= from && guard++ < 40)
        {
            var page = await FetchPageAsync(code, cursor, ct);
            if (page.Count == 0) break;

            DateTime oldest = cursor;
            foreach (var c in page)
            {
                if (c.Date < from || c.Date > to) continue;
                map[c.Date] = c;
                if (c.Date < oldest) oldest = c.Date;
            }

            if (oldest >= cursor) break; // 더 과거로 진행 불가
            cursor = oldest.AddDays(-1);
        }

        if (map.Count == 0)
            throw new PriceSourceException($"다음 금융에서 종목 '{code}'의 기간 내 데이터가 없습니다. 종목코드를 확인하세요.");

        return new StockSeries(code, string.Empty, string.Empty, Kind, map.Values.ToList());
    }

    /// <summary>지정 종료일(to) 기준 과거 방향 limit개 봉 1페이지 조회.</summary>
    private async Task<List<Candle>> FetchPageAsync(string code, DateTime to, CancellationToken ct)
    {
        string toParam = Uri.EscapeDataString($"{to:yyyy-MM-dd} 00:00:00");
        string url = $"https://finance.daum.net/api/charts/A{code}/days" +
                     $"?limit={PageLimit}&to={toParam}&adjusted=true";

        string text;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Referrer = new Uri($"https://finance.daum.net/quotes/A{code}");
            using var resp = await http.SendAsync(req, ct);
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"다음 금융 요청 실패: {ex.Message}");
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<Candle>();
        foreach (var d in arr.EnumerateArray())
        {
            string dateStr = d.TryGetProperty("date", out var de) ? de.GetString() ?? "" : "";
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) continue;
            decimal close = GetDec(d, "tradePrice");
            if (close <= 0) continue;
            list.Add(new Candle(
                date,
                GetDec(d, "openingPrice"),
                GetDec(d, "highPrice"),
                GetDec(d, "lowPrice"),
                close,
                (long)GetDec(d, "candleAccTradeVolume")));
        }
        return list;
    }

    private static decimal GetDec(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
}
