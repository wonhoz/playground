using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 한국투자증권(KIS) Open API REST 데이터 소스. OAuth 토큰 발급/캐싱 후
/// 기간별 일봉(inquire-daily-itemchartprice)을 조회한다. KIS는 1회 최대 100봉이라
/// 긴 기간은 종료일을 과거로 당겨가며 페이징한다. 유일하게 API 키가 필요한 소스.
/// </summary>
public sealed class KisPriceSource(AppConfig config, Action saveConfig, HttpClient http) : IPriceSource
{
    private const string RealBase = "https://openapi.koreainvestment.com:9443";
    private const string MockBase = "https://openapivts.koreainvestment.com:29443";

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public SourceKind Kind => SourceKind.Kis;
    public string DisplayName => "한국투자증권 OpenAPI";
    public bool RequiresApiKey => true;
    public override string ToString() => DisplayName;

    private string BaseUrl => config.UseMockServer ? MockBase : RealBase;

    public async Task<StockSeries> FetchAsync(string code, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        // 날짜→Candle 맵으로 중복 제거하며 과거로 페이징.
        var map = new SortedDictionary<DateTime, Candle>();
        DateTime cursor = to;
        int guard = 0; // 무한루프 방지(최대 ~60회 = 약 24년)

        while (cursor >= from && guard++ < 60)
        {
            var page = await FetchPageAsync(code, from, cursor, 'D', ct);
            if (page.Count == 0) break;

            DateTime oldest = cursor;
            foreach (var c in page)
            {
                if (c.Date < from || c.Date > to) continue;
                map[c.Date] = c;
                if (c.Date < oldest) oldest = c.Date;
            }

            // 더 과거로 진행할 수 없으면 종료.
            if (oldest >= cursor) break;
            cursor = oldest.AddDays(-1);
        }

        if (map.Count == 0)
            throw new PriceSourceException($"KIS에서 종목 '{code}'의 기간 내 데이터가 없습니다.");

        return new StockSeries(code, string.Empty, string.Empty, Kind, map.Values.ToList());
    }

    /// <summary>
    /// 차트용: 지정 기간을 봉주기(D=일/W=주/M=월)로 1회(최대 100봉) 조회해 과거→현재 정렬 반환.
    /// 주/월봉은 100봉이면 충분(주 2년·월 8년)하므로 페이징하지 않는다.
    /// </summary>
    public async Task<List<Candle>> FetchChartAsync(string code, DateTime from, DateTime to, char period, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");
        var list = await FetchPageAsync(code, from, to, period, ct);
        if (list.Count == 0)
            throw new PriceSourceException($"KIS에서 종목 '{code}'의 차트 데이터가 없습니다.");
        list.Sort((a, b) => a.Date.CompareTo(b.Date));
        return list;
    }

    /// <summary>
    /// 차트용: 당일 1분봉 전체를 과거→현재 순서로 조회(inquire-time-itemchartprice, 30건씩 페이징).
    /// KIS는 분봉을 당일치만 제공한다. 5·15·30·60분봉은 호출 측에서 집계한다.
    /// </summary>
    public async Task<List<Candle>> FetchTodayMinutesAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        var map = new SortedDictionary<DateTime, Candle>();
        string inputHour = "";  // 빈값=최근부터
        DateTime? prevOldest = null;

        for (int guard = 0; guard < 16; guard++)  // 30×16≈480분 → 당일+α 충분
        {
            List<Candle> page;
            try
            {
                page = await FetchMinutePageAsync(code, inputHour, ct);
            }
            catch (PriceSourceException) when (map.Count > 0)
            {
                break;  // 유량(초당 호출) 초과 등 → 그때까지 수집분으로 표시
            }
            if (page.Count == 0) break;
            foreach (var c in page) map[c.Date] = c;

            var oldest = page.Min(c => c.Date);
            if (prevOldest != null && oldest >= prevOldest.Value) break;  // 진전 없음
            prevOldest = oldest;
            if (oldest.Hour < 9 || (oldest.Hour == 9 && oldest.Minute == 0)) break;  // 장 시작 도달
            inputHour = oldest.AddMinutes(-1).ToString("HHmmss");
            await Task.Delay(160, ct);  // KIS 초당 호출 제한(유량) 완화
        }

        if (map.Count == 0)
            throw new PriceSourceException("KIS 당일 분봉 데이터가 없습니다(장 시간 외이거나 휴장일 수 있습니다).");
        return map.Values.ToList();
    }

    /// <summary>현재가(실시간) 조회 — inquire-price(FHKST01010100). 모니터링용.</summary>
    public async Task<Quote> FetchQuoteAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        string query = $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}";
        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/domestic-stock/v1/quotations/inquire-price?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "FHKST01010100");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 현재가 응답 오류: {msg.Trim()}");
        }
        var o = root.GetProperty("output");
        return new Quote(code, ParseDecimal(o, "stck_prpr"), ParseDecimal(o, "prdy_ctrt"), DateTime.Now);
    }

    /// <summary>당일 분봉 1페이지(최대 30건, inputHour 시각 이전) 조회.</summary>
    private async Task<List<Candle>> FetchMinutePageAsync(string code, string inputHour, CancellationToken ct)
    {
        string query =
            $"FID_ETC_CLS_CODE=&FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}" +
            $"&FID_INPUT_HOUR_1={inputHour}&FID_PW_DATA_INCU_YN=Y";

        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/domestic-stock/v1/quotations/inquire-time-itemchartprice?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "FHKST03010200");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 분봉 응답 오류: {msg.Trim()}");
        }

        var list = new List<Candle>();
        if (root.TryGetProperty("output2", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in arr.EnumerateArray())
            {
                string d = b.TryGetProperty("stck_bsop_date", out var de) ? de.GetString() ?? "" : "";
                string h = b.TryGetProperty("stck_cntg_hour", out var he) ? he.GetString() ?? "" : "";
                if (d.Length != 8 || h.Length != 6) continue;
                if (!DateTime.TryParseExact(d + h, "yyyyMMddHHmmss", null,
                        System.Globalization.DateTimeStyles.None, out var dt)) continue;
                decimal close = ParseDecimal(b, "stck_prpr");
                if (close <= 0) continue;
                list.Add(new Candle(dt,
                    ParseDecimal(b, "stck_oprc"), ParseDecimal(b, "stck_hgpr"),
                    ParseDecimal(b, "stck_lwpr"), close, ParseLong(b, "cntg_vol")));
            }
        }
        return list;
    }

    /// <summary>지정 [start, end] 구간을 봉주기(period)로 1페이지(최대 100봉) 조회.</summary>
    private async Task<List<Candle>> FetchPageAsync(string code, DateTime start, DateTime end, char period, CancellationToken ct)
    {
        string query =
            $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}" +
            $"&FID_INPUT_DATE_1={start:yyyyMMdd}&FID_INPUT_DATE_2={end:yyyyMMdd}" +
            $"&FID_PERIOD_DIV_CODE={period}&FID_ORG_ADJ_PRC=0";

        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "FHKST03010100");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 응답 오류: {msg.Trim()}");
        }

        var list = new List<Candle>();
        if (root.TryGetProperty("output2", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in arr.EnumerateArray())
            {
                string dateStr = b.TryGetProperty("stck_bsop_date", out var d) ? d.GetString() ?? "" : "";
                if (dateStr.Length != 8 ||
                    !DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dt))
                    continue;
                decimal close = ParseDecimal(b, "stck_clpr");
                if (close <= 0) continue; // 빈 봉 방어
                list.Add(new Candle(dt,
                    ParseDecimal(b, "stck_oprc"),
                    ParseDecimal(b, "stck_hgpr"),
                    ParseDecimal(b, "stck_lwpr"),
                    close,
                    ParseLong(b, "acml_vol")));
            }
        }
        return list;
    }

    // ────────────────────────────── OAuth 토큰 ──────────────────────────────
    private async Task<string> EnsureTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(config.CachedToken) && DateTime.Now < config.TokenExpiresAt.AddMinutes(-10))
            return config.CachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrEmpty(config.CachedToken) && DateTime.Now < config.TokenExpiresAt.AddMinutes(-10))
                return config.CachedToken;

            var body = JsonSerializer.Serialize(new
            {
                grant_type = "client_credentials",
                appkey = config.AppKey,
                appsecret = config.AppSecret
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth2/tokenP")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var resp = await http.SendAsync(req, ct);
            string text = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenEl))
            {
                string msg = root.TryGetProperty("error_description", out var e) ? e.GetString() ?? text : text;
                throw new PriceSourceException($"KIS 토큰 발급 실패: {msg}");
            }

            config.CachedToken = tokenEl.GetString() ?? string.Empty;
            int expiresIn = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 86400;
            config.TokenExpiresAt = DateTime.Now.AddSeconds(expiresIn);
            saveConfig();
            return config.CachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static decimal ParseDecimal(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && decimal.TryParse(v.GetString(), out var d) ? d : 0m;

    private static long ParseLong(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && long.TryParse(v.GetString(), out var l) ? l : 0L;
}
