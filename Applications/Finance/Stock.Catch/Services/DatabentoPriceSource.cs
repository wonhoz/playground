using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// Databento 시세 소스(미국 종목용). Historical HTTP API(<c>timeseries.get_range</c>)로 1분 OHLCV를
/// 조회해 최신 봉 종가를 현재가로, 전일(직전 세션) 종가 대비 등락율을 계산한다.
/// 인증은 API 키를 사용자명으로 하는 HTTP Basic. 데이터셋은 설정(기본 <c>DBEQ.BASIC</c>).
/// 가격은 <c>pretty_px=true</c>로 소수 달러를 받되, 혹시 고정소수(1e-9)로 오면 스케일을 자동 보정한다.
/// </summary>
public sealed class DatabentoPriceSource(AppConfig config, HttpClient http)
{
    private const string Base = "https://hist.databento.com/v0/timeseries.get_range";

    public async Task<Quote> FetchQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!config.HasDatabentoKey)
            throw new PriceSourceException("Databento API Key가 설정되지 않았습니다. 설정에서 입력하세요.");

        string sym = symbol.Trim().ToUpperInvariant();
        string dataset = string.IsNullOrWhiteSpace(config.DatabentoDataset) ? "DBEQ.BASIC" : config.DatabentoDataset.Trim();

        // 최신 가용 봉을 조회한다. historical은 데이터셋별로 가용 종료 시점이 다르므로(주말·지연),
        // end를 지금으로 요청하고 422(available_end 초과)면 응답이 알려준 available_end로 한 번 재시도한다.
        var (text, avail) = await GetRangeAsync(dataset, sym, DateTime.UtcNow, ct);
        if (avail is { } ae) (text, _) = await GetRangeAsync(dataset, sym, ae, ct);

        // 응답은 NDJSON(레코드당 1줄). 각 레코드에서 종가·이벤트 날짜를 뽑는다.
        var bars = new List<(DateTime day, decimal close)>();
        foreach (var line in text.Split('\n'))
        {
            var s = line.Trim();
            if (s.Length == 0 || s[0] != '{') continue;
            try
            {
                using var doc = JsonDocument.Parse(s);
                var root = doc.RootElement;
                if (!TryPrice(root, "close", out var close) || close <= 0) continue;
                DateTime day = TryTs(root, out var ts) ? ts.Date : DateTime.UtcNow.Date;
                bars.Add((day, close));
            }
            catch { /* 파싱 불가 라인 스킵 */ }
        }
        if (bars.Count == 0)
            throw new PriceSourceException($"Databento에서 종목 '{sym}' 시세를 찾지 못했습니다(티커/데이터셋을 확인하세요).");

        decimal price = bars[^1].close;
        var lastDay = bars[^1].day;
        // 직전 세션(다른 날짜) 마지막 종가 = 전일 종가.
        decimal prev = 0m;
        for (int i = bars.Count - 1; i >= 0; i--) if (bars[i].day != lastDay) { prev = bars[i].close; break; }
        decimal rate = prev > 0 ? Math.Round((price / prev - 1) * 100, 2) : 0m;
        return new Quote(sym, price, rate, DateTime.Now);
    }

    /// <summary>
    /// get_range 1회 호출. 성공이면 (본문, null), 422 available_end 초과면 (빈문자열, 재시도용 available_end).
    /// 그 외 오류는 예외.
    /// </summary>
    private async Task<(string body, DateTime? availableEnd)> GetRangeAsync(string dataset, string sym, DateTime end, CancellationToken ct)
    {
        var start = end.AddDays(-4);
        string url = $"{Base}?dataset={Uri.EscapeDataString(dataset)}&schema=ohlcv-1m&stype_in=raw_symbol" +
                     $"&symbols={Uri.EscapeDataString(sym)}&start={start:yyyy-MM-ddTHH:mm:ss}Z&end={end:yyyy-MM-ddTHH:mm:ss}Z" +
                     "&encoding=json&pretty_px=true&pretty_ts=true";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(config.DatabentoApiKey.Trim() + ":"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            using var resp = await http.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new PriceSourceException("Databento 호출 한도를 초과했습니다. 잠시 후 다시 시도하세요.");
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new PriceSourceException("Databento API Key가 올바르지 않거나 데이터셋 권한이 없습니다.");
            string body = await resp.Content.ReadAsStringAsync(ct);
            // 422: end가 가용 범위를 넘음 → available_end 파싱해 재시도 신호로 반환.
            if ((int)resp.StatusCode == 422 && body.Contains("available_end", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("detail", out var d) && d.TryGetProperty("payload", out var pl)
                        && pl.TryGetProperty("available_end", out var ave) && ave.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(ave.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ae))
                        return ("", ae);
                }
                catch { /* 파싱 실패 → 아래에서 일반 오류 처리 */ }
            }
            if (!resp.IsSuccessStatusCode)
                throw new PriceSourceException($"Databento 응답 오류({(int)resp.StatusCode}).");
            return (body, null);
        }
        catch (PriceSourceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PriceSourceException($"Databento 요청 실패: {ex.Message}");
        }
    }

    /// <summary>가격 필드 파싱: 소수 달러(pretty_px) 또는 고정소수(1e-9) 자동 판별.</summary>
    private static bool TryPrice(JsonElement root, string prop, out decimal price)
    {
        price = 0m;
        if (!root.TryGetProperty(prop, out var v)) return false;
        decimal raw;
        if (v.ValueKind == JsonValueKind.Number) { if (!v.TryGetDecimal(out raw)) return false; }
        else if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) raw = d;
        else return false;
        // 고정소수(나노달러)면 값이 비정상적으로 큼 → 1e9로 보정.
        price = raw > 1_000_000m ? raw / 1_000_000_000m : raw;
        return price > 0;
    }

    private static bool TryTs(JsonElement root, out DateTime ts)
    {
        ts = default;
        foreach (var key in new[] { "ts_event", "hd_ts_event" })
            if (root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out ts))
                return true;
        if (root.TryGetProperty("hd", out var hd) && hd.ValueKind == JsonValueKind.Object
            && hd.TryGetProperty("ts_event", out var hv) && hv.ValueKind == JsonValueKind.String
            && DateTime.TryParse(hv.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out ts))
            return true;
        return false;
    }
}
