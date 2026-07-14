using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

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

    /// <summary>
    /// 모니터링용 경량 분봉 조회: 최근 완성 분봉을 minBars개 이상 확보될 때까지만 페이징(30건 단위).
    /// 당일 전체를 훑는 <see cref="FetchTodayMinutesAsync"/>와 달리 폴링 주기에 맞춰 호출해도 유량 부담이 작다.
    /// </summary>
    public async Task<List<Candle>> FetchRecentMinutesAsync(string code, int minBars, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        var map = new SortedDictionary<DateTime, Candle>();
        string inputHour = "";  // 빈값=최근부터
        DateTime? prevOldest = null;

        for (int guard = 0; guard < 8 && map.Count < minBars; guard++)
        {
            List<Candle> page;
            try { page = await FetchMinutePageAsync(code, inputHour, ct); }
            catch (PriceSourceException) when (map.Count > 0) { break; }   // 유량 초과 등 → 수집분 사용
            if (page.Count == 0) break;
            foreach (var c in page) map[c.Date] = c;

            var oldest = page.Min(c => c.Date);
            if (prevOldest != null && oldest >= prevOldest.Value) break;   // 진전 없음
            prevOldest = oldest;
            if (oldest.Hour < 9 || (oldest.Hour == 9 && oldest.Minute == 0)) break;  // 장 시작 도달
            inputHour = oldest.AddMinutes(-1).ToString("HHmmss");
            await Task.Delay(160, ct);  // KIS 초당 호출 제한(유량) 완화
        }
        return map.Values.ToList();
    }

    /// <summary>
    /// 특정 일자의 1분봉 전체 조회 — inquire-time-dailychartprice(FHKST03010230).
    /// 15:30부터 역방향 페이징(페이지당 ~120건)하며, 응답이 요청일을 넘어 전일로 이어지므로
    /// 요청일 데이터만 수집하고 날짜 경계에서 중단한다. 과거 일자(휴장일 제외)를 지원한다.
    /// </summary>
    public async Task<List<Candle>> FetchDayMinutesAsync(string code, DateTime date, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        var map = new SortedDictionary<DateTime, Candle>();
        string inputHour = "153000";   // 정규장 마감부터 역방향
        DateTime? prevOldest = null;

        for (int guard = 0; guard < 8; guard++)   // 120봉 × 8페이지 ≈ 960분 → 하루치 충분
        {
            List<Candle> page;
            try
            {
                page = await FetchDayMinutePageAsync(code, date, inputHour, ct);
            }
            catch (PriceSourceException) when (map.Count > 0)
            {
                break;   // 유량 초과 등 → 수집분 사용
            }
            if (page.Count == 0) break;

            bool crossedDay = false;
            foreach (var c in page)
            {
                if (c.Date.Date != date.Date) { crossedDay = true; continue; }   // 전일로 넘어간 봉은 버림
                map[c.Date] = c;
            }
            if (crossedDay) break;   // 요청일 구간 완료

            var dayBars = page.Where(c => c.Date.Date == date.Date).ToList();
            if (dayBars.Count == 0) break;
            var oldest = dayBars.Min(c => c.Date);
            if (prevOldest != null && oldest >= prevOldest.Value) break;   // 진전 없음
            prevOldest = oldest;
            if (oldest.Hour < 9 || (oldest.Hour == 9 && oldest.Minute == 0)) break;   // 장 시작 도달
            inputHour = oldest.AddMinutes(-1).ToString("HHmmss");
            await Task.Delay(160, ct);   // KIS 초당 호출 제한(유량) 완화
        }

        if (map.Count == 0)
            throw new PriceSourceException(
                $"KIS에서 {date:yyyy-MM-dd} 분봉 데이터가 없습니다(휴장일이거나 KIS 보관 기간을 벗어났을 수 있습니다).");
        return map.Values.ToList();
    }

    /// <summary>일별 분봉 1페이지(최대 ~120건, date 일자의 inputHour 시각 이전) 조회.</summary>
    private async Task<List<Candle>> FetchDayMinutePageAsync(string code, DateTime date, string inputHour, CancellationToken ct)
    {
        string query =
            $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}" +
            $"&FID_INPUT_DATE_1={date:yyyyMMdd}&FID_INPUT_HOUR_1={inputHour}" +
            "&FID_PW_DATA_INCU_YN=Y&FID_FAKE_TICK_INCU_YN=";

        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/domestic-stock/v1/quotations/inquire-time-dailychartprice?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "FHKST03010230");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 일별분봉 응답 오류: {msg.Trim()}");
        }
        return ParseMinuteBars(root);
    }

    /// <summary>분봉 응답(output2)의 공통 파싱 — 당일 분봉(FHKST03010200)·일별 분봉(FHKST03010230) 공용.</summary>
    private static List<Candle> ParseMinuteBars(JsonElement root)
    {
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

    /// <summary>현재가(실시간) 조회 — inquire-price(FHKST01010100). 모니터링용.</summary>
    public async Task<Quote> FetchQuoteAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        // 시장 구분: J=KRX, NX=NXT, UN=통합(KRX+NXT). 통합/NXT면 장 마감 후 NXT 시간대 시세도 수신.
        string div = string.IsNullOrWhiteSpace(config.KisMarketDiv) ? "UN" : config.KisMarketDiv.Trim().ToUpperInvariant();
        string query = $"FID_COND_MRKT_DIV_CODE={div}&FID_INPUT_ISCD={code}";
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

    /// <summary>
    /// 국내 업종/지수 현재가 조회 — inquire-index-price(FHPUP02100000). 코스피 0001·코스닥 1001·코스피200 2001 등.
    /// output.bstp_nmix_prpr(지수 현재가)·bstp_nmix_prdy_ctrt(전일 대비율 %).
    /// </summary>
    public async Task<Quote> FetchIndexQuoteAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        string query = $"FID_COND_MRKT_DIV_CODE=U&FID_INPUT_ISCD={code.Trim()}";
        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/domestic-stock/v1/quotations/inquire-index-price?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "FHPUP02100000");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 지수 응답 오류: {msg.Trim()}");
        }
        var o = root.GetProperty("output");
        decimal idx = ParseDecimal(o, "bstp_nmix_prpr");
        if (idx <= 0)
            throw new PriceSourceException($"KIS 지수 데이터가 없습니다('{code}'). 지수 코드를 확인하세요(코스피 0001·코스닥 1001).");
        return new Quote(code, idx, ParseDecimal(o, "bstp_nmix_prdy_ctrt"), DateTime.Now);
    }

    /// <summary>
    /// 해외주식 현재가(실시간) 조회 — overseas-price/price(HHDFS00000300). 관심 종목(미국) 모니터링용.
    /// exchange: NAS(나스닥)/NYS(뉴욕)/AMS(아멕스·Arca). output.last(현재가)·output.rate(등락율%).
    /// </summary>
    public async Task<Quote> FetchOverseasQuoteAsync(string exchange, string symbol, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        string excd = string.IsNullOrWhiteSpace(exchange) ? "NAS" : exchange.Trim().ToUpperInvariant();
        string query = $"AUTH=&EXCD={excd}&SYMB={symbol.Trim().ToUpperInvariant()}";
        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/uapi/overseas-price/v1/quotations/price?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", "HHDFS00000300");
        req.Headers.Add("custtype", "P");

        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            throw new PriceSourceException($"KIS 해외 현재가 응답 오류: {msg.Trim()}");
        }
        var o = root.GetProperty("output");
        decimal last = ParseDecimal(o, "last");
        if (last <= 0)
            throw new PriceSourceException($"KIS 해외 현재가 데이터가 없습니다('{excd}:{symbol}'). 거래소 코드를 확인하세요.");
        return new Quote(symbol, last, ParseDecimal(o, "rate"), DateTime.Now);
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
        return ParseMinuteBars(root);
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

    // ───────────────────────── 실시간 수급·호가(즐겨찾기 상세 창) ─────────────────────────

    /// <summary>KIS GET 공용 — 토큰·헤더·rt_cd 체크. 반환 JsonDocument는 호출 측이 dispose.</summary>
    private async Task<JsonDocument> KisGetDocAsync(string path, string trId, string query, CancellationToken ct)
    {
        string token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{path}?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", config.AppKey);
        req.Headers.Add("appsecret", config.AppSecret);
        req.Headers.Add("tr_id", trId);
        req.Headers.Add("custtype", "P");
        using var resp = await http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = doc.RootElement.TryGetProperty("msg1", out var m) ? m.GetString() ?? text : text;
            doc.Dispose();
            throw new PriceSourceException($"KIS 응답 오류: {msg.Trim()}");
        }
        return doc;
    }

    /// <summary>
    /// 실시간 호가창(10단) — inquire-asking-price-exp-ccn(FHKST01010200). 장중에만 잔량이 채워진다.
    /// </summary>
    public async Task<MarketDepth> FetchMarketDepthAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다.");
        using var doc = await KisGetDocAsync(
            "/uapi/domestic-stock/v1/quotations/inquire-asking-price-exp-ccn", "FHKST01010200",
            $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}", ct);
        var o1 = doc.RootElement.GetProperty("output1");
        var asks = new List<AskBid>(10);
        var bids = new List<AskBid>(10);
        for (int i = 1; i <= 10; i++)
        {
            asks.Add(new AskBid(ParseDecimal(o1, $"askp{i}"), ParseLong(o1, $"askp_rsqn{i}")));
            bids.Add(new AskBid(ParseDecimal(o1, $"bidp{i}"), ParseLong(o1, $"bidp_rsqn{i}")));
        }
        decimal price = doc.RootElement.TryGetProperty("output2", out var o2) ? ParseDecimal(o2, "stck_prpr") : 0m;
        return new MarketDepth(asks, bids, ParseLong(o1, "total_askp_rsqn"), ParseLong(o1, "total_bidp_rsqn"), price);
    }

    /// <summary>
    /// 실시간 수급 스냅샷 — inquire-price(외인·프로그램 순매수·소진율·현재가) + inquire-investor(기관·개인 순매수)
    /// + inquire-ccnl(체결강도). 3콜 조합. 장 마감 시 일부 값은 0/전일 기준.
    /// </summary>
    public async Task<SupplyDemand> FetchSupplyDemandAsync(string code, CancellationToken ct = default)
    {
        if (!config.HasKisCredentials)
            throw new PriceSourceException("KIS APP KEY / APP SECRET이 설정되지 않았습니다.");

        decimal price = 0m, rate = 0m; long frgn = 0, pgtr = 0; double ehrt = 0;
        using (var d1 = await KisGetDocAsync("/uapi/domestic-stock/v1/quotations/inquire-price", "FHKST01010100",
            $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}", ct))
        {
            var o = d1.RootElement.GetProperty("output");
            price = ParseDecimal(o, "stck_prpr"); rate = ParseDecimal(o, "prdy_ctrt");
            frgn = ParseLong(o, "frgn_ntby_qty"); pgtr = ParseLong(o, "pgtr_ntby_qty");
            ehrt = (double)ParseDecimal(o, "hts_frgn_ehrt");
        }

        long orgn = 0, prsn = 0;
        try
        {
            using var d2 = await KisGetDocAsync("/uapi/domestic-stock/v1/quotations/inquire-investor", "FHKST01010900",
                $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}", ct);
            if (d2.RootElement.TryGetProperty("output", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            { orgn = ParseLong(arr[0], "orgn_ntby_qty"); prsn = ParseLong(arr[0], "prsn_ntby_qty"); }
        }
        catch { /* 투자자 집계 실패는 무시(기관/개인만 0) */ }

        double exec = 0;
        try
        {
            using var d3 = await KisGetDocAsync("/uapi/domestic-stock/v1/quotations/inquire-ccnl", "FHKST01010300",
                $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}", ct);
            if (d3.RootElement.TryGetProperty("output", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                exec = (double)ParseDecimal(arr[0], "tday_rltv");
        }
        catch { /* 체결강도 실패는 무시 */ }

        return new SupplyDemand(price, rate, frgn, orgn, prsn, pgtr, ehrt, exec);
    }
}
