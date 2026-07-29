using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SiegeLab.Services;

public sealed class LoginTestConfig
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "POST";
    /// <summary>{user}/{pass} 치환. 예: username={user}&password={pass}</summary>
    public string BodyTemplate { get; set; } = "username={user}&password={pass}";
    public string ContentType { get; set; } = "application/x-www-form-urlencoded";
    /// <summary>기본은 존재하지 않는 테스트 계정 — 실계정 잠금 방지.</summary>
    public string Username { get; set; } = "siege-test-nouser";
    public int Attempts { get; set; } = 50;
    public int DelayMs { get; set; } = 200;         // 시도 간 간격
    public int TimeoutMs { get; set; } = 10_000;
    /// <summary>성공 판정: 이 상태코드면 로그인 성공으로 간주(경고).</summary>
    public string SuccessStatuses { get; set; } = "200,302";
    /// <summary>실패 응답 본문에 포함되리라 기대되는 문자열(선택).</summary>
    public string FailureContains { get; set; } = "";
}

public sealed record LoginSnapshot(
    int Attempt,
    int Total,
    int LastStatus,
    double LastLatencyMs,
    double AvgLatencyMs,
    bool BlockingDetected,
    int BlockedAtAttempt,
    string BlockReason,
    bool SuspiciousSuccess,
    IReadOnlyDictionary<string, int> StatusDist,
    string Verdict);

/// <summary>
/// 로그인 방어(무차별 대입 저항성) 테스터.
/// 의도적으로 틀린 자격증명을 반복 전송하여 레이트리밋/계정 잠금/429 등
/// 방어 기제가 작동하는지 확인한다. 실제 인증 돌파를 시도하지 않는다.
/// </summary>
public sealed class LoginResilienceTester
{
    private readonly HttpClient _http;

    private static readonly string[] LockoutKeywords =
    [
        "too many", "rate limit", "locked", "lockout", "blocked", "try again later",
        "잠시 후", "차단", "잠금", "제한", "너무 많",
    ];

    public LoginResilienceTester()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<LoginSnapshot> RunAsync(LoginTestConfig cfg,
        IProgress<LoginSnapshot> progress, CancellationToken ct)
    {
        int attempts = Math.Clamp(cfg.Attempts, 1, 5000);
        var successSet = ParseStatuses(cfg.SuccessStatuses);
        var statusDist = new Dictionary<string, int>();
        double latSum = 0;
        bool blocking = false;
        int blockedAt = 0;
        string blockReason = "";
        bool suspicious = false;

        // 초기 기준 지연(첫 5회 평균)으로 타르핏 감지
        var earlyLatencies = new List<double>();

        for (int i = 1; i <= attempts && !ct.IsCancellationRequested; i++)
        {
            var (status, latency, body) = await AttemptOnce(cfg, i, ct);
            latSum += latency;
            string bucket = status == 0 ? "err" : $"{status}";
            statusDist[bucket] = statusDist.GetValueOrDefault(bucket) + 1;

            if (i <= 5) earlyLatencies.Add(latency);

            // 방어 감지
            if (!blocking)
            {
                if (status == 429)
                { blocking = true; blockedAt = i; blockReason = "HTTP 429 Too Many Requests"; }
                else if (status == 403)
                { blocking = true; blockedAt = i; blockReason = "HTTP 403 Forbidden (차단 추정)"; }
                else if (LockoutKeywords.Any(k => body.Contains(k, StringComparison.OrdinalIgnoreCase)))
                { blocking = true; blockedAt = i; blockReason = "응답 본문에 잠금/제한 메시지"; }
                else if (earlyLatencies.Count == 5 && i > 10)
                {
                    double baseline = earlyLatencies.Average();
                    if (baseline > 0 && latency > baseline * 4 && latency > 1000)
                    { blocking = true; blockedAt = i; blockReason = "응답 지연 급증(타르핏 추정)"; }
                }
            }

            // 의심스러운 성공(틀린 비번인데 성공 코드)
            if (successSet.Contains(status)) suspicious = true;

            progress.Report(new LoginSnapshot(
                i, attempts, status, latency, latSum / i, blocking, blockedAt, blockReason,
                suspicious, new Dictionary<string, int>(statusDist),
                BuildVerdict(blocking, blockedAt, blockReason, suspicious, i)));

            if (cfg.DelayMs > 0)
            {
                try { await Task.Delay(cfg.DelayMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        int done = statusDist.Values.Sum();
        return new LoginSnapshot(done, attempts,
            0, 0, done > 0 ? latSum / done : 0, blocking, blockedAt, blockReason, suspicious,
            statusDist, BuildVerdict(blocking, blockedAt, blockReason, suspicious, done));
    }

    private async Task<(int status, double latency, string body)> AttemptOnce(
        LoginTestConfig cfg, int n, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(cfg.TimeoutMs);
        var sw = Stopwatch.StartNew();
        try
        {
            // 매 시도마다 다른 틀린 비밀번호 — 실제 인증은 결코 성공하지 않음
            string wrongPass = $"WrongPw!{n}_{Guid.NewGuid():N}"[..24];
            string payload = cfg.BodyTemplate
                .Replace("{user}", Uri.EscapeDataString(cfg.Username))
                .Replace("{pass}", Uri.EscapeDataString(wrongPass));

            using var req = new HttpRequestMessage(new HttpMethod(cfg.Method), cfg.Url);
            if (cfg.Method is not "GET" and not "HEAD")
                req.Content = new StringContent(payload, Encoding.UTF8, cfg.ContentType);

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();
            return ((int)resp.StatusCode, sw.Elapsed.TotalMilliseconds,
                    body.Length > 8192 ? body[..8192] : body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            sw.Stop();
            return (0, sw.Elapsed.TotalMilliseconds, "");
        }
    }

    private static string BuildVerdict(bool blocking, int at, string reason,
        bool suspicious, int done)
    {
        if (suspicious)
            return "⚠ 위험: 틀린 자격증명인데 성공 응답이 관찰됨 — 인증 로직 점검 필요";
        if (blocking)
            return $"✅ 방어 감지: {at}회째부터 차단 ({reason})";
        return $"⚠ 방어 미감지: {done}회 연속 실패 시도가 동일하게 처리됨 — 레이트리밋/계정 잠금 도입 권장";
    }

    private static HashSet<int> ParseStatuses(string s)
    {
        var set = new HashSet<int>();
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var code)) set.Add(code);
        return set;
    }
}
