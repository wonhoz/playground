using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 네이버 금융 차트 API(siseJson.naver) 기반 무인증 데이터 소스.
/// 응답은 표준 JSON이 아닌 작은따옴표/헤더 혼합 텍스트라 정규식으로 행을 추출한다.
/// </summary>
public sealed partial class NaverPriceSource(HttpClient http) : IPriceSource
{
    public SourceKind Kind => SourceKind.Naver;
    public string DisplayName => "네이버 금융";
    public bool RequiresApiKey => false;
    public override string ToString() => DisplayName;

    // ["20240102", 53000, 53400, 52600, 52600, 17142847, 53.34]
    [GeneratedRegex(@"\[""(\d{8})"",\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*(\d+)")]
    private static partial Regex RowRegex();

    public async Task<StockSeries> FetchAsync(string code, DateTime from, DateTime to, CancellationToken ct = default)
    {
        string url = "https://api.finance.naver.com/siseJson.naver" +
                     $"?symbol={code}&requestType=1" +
                     $"&startTime={from:yyyyMMdd}&endTime={to:yyyyMMdd}&timeframe=day";

        string text;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Referrer = new Uri("https://finance.naver.com/");
            using var resp = await http.SendAsync(req, ct);
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"네이버 금융 요청 실패: {ex.Message}");
        }

        var candles = new List<Candle>();
        foreach (Match m in RowRegex().Matches(text))
        {
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) continue;
            candles.Add(new Candle(
                date,
                ParseDec(m.Groups[2].Value),
                ParseDec(m.Groups[3].Value),
                ParseDec(m.Groups[4].Value),
                ParseDec(m.Groups[5].Value),
                long.TryParse(m.Groups[6].Value, out var v) ? v : 0));
        }

        if (candles.Count == 0)
            throw new PriceSourceException($"종목 '{code}'의 데이터가 없습니다. 종목코드/기간을 확인하세요.");

        candles.Sort((a, b) => a.Date.CompareTo(b.Date));
        return new StockSeries(code, string.Empty, string.Empty, Kind, candles);
    }

    private static decimal ParseDec(string s)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
