using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Stock.Watch.Models;

namespace Stock.Watch.Services;

/// <summary>KIS API 호출 실패 시 throw. 사용자 표시용 한글 메시지를 담는다.</summary>
public sealed class KisApiException(string message) : Exception(message);

/// <summary>
/// 한국투자증권(KIS) Open API REST 클라이언트.
/// OAuth 토큰 발급/캐싱, 현재가(inquire-price), 기간별 시세(inquire-daily-itemchartprice) 조회를 제공.
/// 외부 NuGet 의존 없이 <see cref="HttpClient"/>만 사용한다.
/// </summary>
public sealed class KisApiClient : IDisposable
{
    private const string RealBase = "https://openapi.koreainvestment.com:9443";
    private const string MockBase = "https://openapivts.koreainvestment.com:29443";

    private readonly AppConfig _config;
    private readonly Action _saveConfig;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KisApiClient(AppConfig config, Action saveConfig)
    {
        _config = config;
        _saveConfig = saveConfig;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string BaseUrl => _config.UseMockServer ? MockBase : RealBase;

    /// <summary>실시간 WebSocket 접속 URL(실전 21000 / 모의 31000).</summary>
    public string WebSocketUrl => _config.UseMockServer
        ? "ws://ops.koreainvestment.com:31000"
        : "ws://ops.koreainvestment.com:21000";

    // ────────────────────────────── OAuth 토큰 ──────────────────────────────
    private async Task<string> EnsureTokenAsync(CancellationToken ct)
    {
        if (!_config.HasCredentials)
            throw new KisApiException("APP KEY / APP SECRET이 설정되지 않았습니다. 설정에서 입력하세요.");

        if (!string.IsNullOrEmpty(_config.CachedToken) && DateTime.Now < _config.TokenExpiresAt.AddMinutes(-10))
            return _config.CachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // 락 획득 후 재확인(다른 호출이 갱신했을 수 있음)
            if (!string.IsNullOrEmpty(_config.CachedToken) && DateTime.Now < _config.TokenExpiresAt.AddMinutes(-10))
                return _config.CachedToken;

            var body = JsonSerializer.Serialize(new
            {
                grant_type = "client_credentials",
                appkey = _config.AppKey,
                appsecret = _config.AppSecret
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth2/tokenP")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenEl))
            {
                string msg = root.TryGetProperty("error_description", out var e) ? e.GetString() ?? text : text;
                throw new KisApiException($"토큰 발급 실패: {msg}");
            }

            _config.CachedToken = tokenEl.GetString() ?? string.Empty;
            int expiresIn = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 86400;
            _config.TokenExpiresAt = DateTime.Now.AddSeconds(expiresIn);
            _saveConfig();
            return _config.CachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // ──────────────────────── WebSocket approval_key ────────────────────────
    /// <summary>실시간 WebSocket 접속용 approval_key 발급(약 24h 캐시). OAuth 토큰과 별개.</summary>
    public async Task<string> GetApprovalKeyAsync(CancellationToken ct = default)
    {
        if (!_config.HasCredentials)
            throw new KisApiException("APP KEY / APP SECRET이 설정되지 않았습니다.");

        if (!string.IsNullOrEmpty(_config.CachedApprovalKey) && DateTime.Now < _config.ApprovalExpiresAt)
            return _config.CachedApprovalKey;

        var body = JsonSerializer.Serialize(new
        {
            grant_type = "client_credentials",
            appkey = _config.AppKey,
            secretkey = _config.AppSecret   // approval 엔드포인트는 'secretkey' 필드명 사용
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth2/Approval")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("approval_key", out var keyEl) || keyEl.GetString() is not { Length: > 0 } key)
            throw new KisApiException($"approval_key 발급 실패: {text}");

        _config.CachedApprovalKey = key;
        _config.ApprovalExpiresAt = DateTime.Now.AddHours(12);
        _saveConfig();
        return key;
    }

    private async Task<HttpRequestMessage> BuildGetAsync(string path, string trId, string query, CancellationToken ct)
    {
        string token = await EnsureTokenAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{path}?{query}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("appkey", _config.AppKey);
        req.Headers.Add("appsecret", _config.AppSecret);
        req.Headers.Add("tr_id", trId);
        req.Headers.Add("custtype", "P");
        return req;
    }

    // ────────────────────────────── 현재가 ──────────────────────────────
    public async Task<Quote> GetQuoteAsync(string code, CancellationToken ct = default)
    {
        string query = $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}";
        using var req = await BuildGetAsync("/uapi/domestic-stock/v1/quotations/inquire-price", "FHKST01010100", query, ct);
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        EnsureSuccess(root, text);

        var o = root.GetProperty("output");
        return new Quote(
            code,
            ParseDecimal(o, "stck_prpr"),
            ParseDecimal(o, "prdy_vrss"),
            ParseDecimal(o, "prdy_ctrt"),
            ParseLong(o, "acml_vol"),
            DateTime.Now);
    }

    // ────────────────────────────── 기간별 일봉 ──────────────────────────────
    /// <summary>최근 일봉 캔들을 과거→현재 순서로 반환. KIS는 1회 최대 100봉.</summary>
    public async Task<List<Candle>> GetDailyCandlesAsync(string code, int count = 100, CancellationToken ct = default)
    {
        var end = DateTime.Today;
        // 영업일 누락 대비 여유롭게 달력일을 넓게 잡는다.
        var start = end.AddDays(-(int)(count * 1.8) - 10);
        string query =
            $"FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD={code}" +
            $"&FID_INPUT_DATE_1={start:yyyyMMdd}&FID_INPUT_DATE_2={end:yyyyMMdd}" +
            $"&FID_PERIOD_DIV_CODE=D&FID_ORG_ADJ_PRC=0";

        using var req = await BuildGetAsync("/uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice", "FHKST03010100", query, ct);
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        EnsureSuccess(root, text);

        var list = new List<Candle>();
        if (root.TryGetProperty("output2", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in arr.EnumerateArray())
            {
                string dateStr = b.TryGetProperty("stck_bsop_date", out var d) ? d.GetString() ?? "" : "";
                if (dateStr.Length != 8) continue;
                if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dt))
                    continue;
                // 빈 봉(거래정지 등) 방어
                decimal close = ParseDecimal(b, "stck_clpr");
                if (close <= 0) continue;
                list.Add(new Candle(dt,
                    ParseDecimal(b, "stck_oprc"),
                    ParseDecimal(b, "stck_hgpr"),
                    ParseDecimal(b, "stck_lwpr"),
                    close,
                    ParseLong(b, "acml_vol")));
            }
        }

        // KIS는 최신→과거 내림차순 반환 → 과거→현재로 뒤집는다.
        list.Reverse();
        if (list.Count > count) list = list.Skip(list.Count - count).ToList();
        return list;
    }

    // ────────────────────────────── 헬퍼 ──────────────────────────────
    private static void EnsureSuccess(JsonElement root, string raw)
    {
        if (root.TryGetProperty("rt_cd", out var rt) && rt.GetString() != "0")
        {
            string msg = root.TryGetProperty("msg1", out var m) ? m.GetString() ?? raw : raw;
            throw new KisApiException($"KIS 응답 오류: {msg.Trim()}");
        }
    }

    private static decimal ParseDecimal(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && decimal.TryParse(v.GetString(), out var d) ? d : 0m;

    private static long ParseLong(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && long.TryParse(v.GetString(), out var l) ? l : 0L;

    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
    }
}
