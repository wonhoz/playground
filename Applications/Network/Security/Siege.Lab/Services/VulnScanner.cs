using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using SiegeLab.Models;

namespace SiegeLab.Services;

public sealed class VulnScanConfig
{
    public bool ProbeSensitiveFiles { get; set; } = true;
    public bool ProbeAdminConsoles  { get; set; } = true;
    public bool ProbeBackdoors      { get; set; } = true;   // 웹셸/백도어 흔적
    public bool ProbeDirListing     { get; set; } = true;
    public bool ProbeVerboseErrors  { get; set; } = true;
    public bool ProbeTrace          { get; set; } = true;
    public bool ProbeDefaultCreds   { get; set; } = false;  // 공격적 — 기본 비활성
    public int DelayMs { get; set; } = 150;                 // 요청 간 지연(부하 완화)
}

/// <summary>
/// 능동 취약점/노출 스캐너. 정찰 등급의 비파괴 요청만 수행한다.
/// (파일·경로 존재 확인, 안전한 GET, HTTP Basic 기본자격 확인)
/// 인젝션·파괴적 페이로드는 전송하지 않는다.
/// </summary>
public sealed class VulnScanner
{
    private readonly HttpClient _http;

    public VulnScanner()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SiegeLab/1.0 (+authorized-scan)");
    }

    // 노출 시 위험한 민감 파일/경로
    private static readonly (string path, Severity sev)[] SensitiveFiles =
    [
        ("/.git/HEAD", Severity.Critical), ("/.git/config", Severity.Critical),
        ("/.env", Severity.Critical), ("/.env.local", Severity.Critical),
        ("/.env.production", Severity.Critical), ("/appsettings.json", Severity.High),
        ("/web.config", Severity.High), ("/.htaccess", Severity.Medium),
        ("/config.php.bak", Severity.High), ("/backup.zip", Severity.High),
        ("/backup.sql", Severity.Critical), ("/db.sql", Severity.Critical),
        ("/dump.sql", Severity.Critical), ("/database.sql", Severity.Critical),
        ("/.svn/entries", Severity.High), ("/WEB-INF/web.xml", Severity.High),
        ("/.DS_Store", Severity.Low), ("/composer.json", Severity.Low),
        ("/package.json", Severity.Low), ("/phpinfo.php", Severity.High),
        ("/info.php", Severity.High), ("/server-status", Severity.Medium),
        ("/server-info", Severity.Medium), ("/id_rsa", Severity.Critical),
        ("/.aws/credentials", Severity.Critical),
    ];

    // 인증 없이 노출되면 위험한 관리/디버그 콘솔
    private static readonly (string path, Severity sev)[] AdminConsoles =
    [
        ("/admin", Severity.Low), ("/administrator", Severity.Low),
        ("/admin/login", Severity.Low), ("/phpmyadmin/", Severity.High),
        ("/pma/", Severity.High), ("/adminer.php", Severity.High),
        ("/manager/html", Severity.High), ("/actuator", Severity.Medium),
        ("/actuator/env", Severity.Critical), ("/actuator/health", Severity.Low),
        ("/actuator/heapdump", Severity.Critical), ("/console", Severity.Medium),
        ("/debug", Severity.Medium), ("/_debug", Severity.Medium),
        ("/swagger", Severity.Low), ("/swagger-ui.html", Severity.Low),
        ("/api-docs", Severity.Low), ("/graphql", Severity.Low),
        ("/wp-admin/", Severity.Low), ("/wp-login.php", Severity.Low),
        ("/.well-known/security.txt", Severity.Info),
    ];

    // 웹셸/백도어로 흔히 쓰이는 파일명
    private static readonly string[] BackdoorNames =
    [
        "/shell.php", "/c99.php", "/r57.php", "/wso.php", "/b374k.php",
        "/cmd.php", "/backdoor.php", "/webshell.php", "/eval.php", "/up.php",
        "/upload.php", "/x.php", "/1.php", "/0.php", "/a.php",
        "/shell.jsp", "/cmd.jsp", "/shell.aspx", "/cmd.aspx", "/tunnel.php",
    ];

    private static readonly Regex ErrorSignatures = new(
        @"(stack trace|Traceback \(most recent|at System\.|Exception in|Fatal error|" +
        @"Warning: .*on line|SQLSTATE|ORA-\d{4,}|You have an error in your SQL syntax|" +
        @"System\.Data\.SqlClient|org\.apache\.|javax\.servlet|Microsoft\.AspNetCore)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<List<SecurityFinding>> RunAsync(string baseUrl, VulnScanConfig cfg,
        IProgress<string> log, CancellationToken ct)
    {
        var findings = new List<SecurityFinding>();
        var baseUri = new Uri(baseUrl);
        string root = baseUri.GetLeftPart(UriPartial.Authority);

        if (cfg.ProbeSensitiveFiles)
        {
            log.Report("민감 파일 노출 점검 중…");
            foreach (var (path, sev) in SensitiveFiles)
            {
                ct.ThrowIfCancellationRequested();
                await ProbeFileAsync(root + path, sev, FindingCategory.Exposure,
                    "민감 파일 노출", "해당 파일에 대한 외부 접근을 차단하세요.", findings, cfg, ct);
            }
        }

        if (cfg.ProbeAdminConsoles)
        {
            log.Report("관리·디버그 콘솔 노출 점검 중…");
            foreach (var (path, sev) in AdminConsoles)
            {
                ct.ThrowIfCancellationRequested();
                await ProbeConsoleAsync(root + path, sev, findings, cfg, ct);
            }
        }

        if (cfg.ProbeBackdoors)
        {
            log.Report("웹셸/백도어 흔적 점검 중…");
            foreach (var path in BackdoorNames)
            {
                ct.ThrowIfCancellationRequested();
                await ProbeBackdoorAsync(root + path, findings, cfg, ct);
            }
        }

        if (cfg.ProbeDirListing)
        {
            log.Report("디렉터리 리스팅 점검 중…");
            foreach (var dir in new[] { "/", "/images/", "/uploads/", "/files/", "/backup/", "/assets/" })
            {
                ct.ThrowIfCancellationRequested();
                await ProbeDirListingAsync(root + dir, findings, cfg, ct);
            }
        }

        if (cfg.ProbeVerboseErrors)
        {
            log.Report("상세 오류 노출 점검 중…");
            await ProbeVerboseErrorAsync(root, findings, cfg, ct);
        }

        if (cfg.ProbeTrace)
        {
            log.Report("TRACE 메서드(XST) 점검 중…");
            await ProbeTraceAsync(root, findings, ct);
        }

        if (cfg.ProbeDefaultCreds)
        {
            log.Report("기본 자격증명 점검 중… (공격적)");
            await ProbeDefaultCredsAsync(root, findings, cfg, ct);
        }

        if (findings.Count == 0)
            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Exposure, Severity = Severity.Ok,
                Title = "노출·백도어 흔적 없음",
                Detail = "점검한 경로에서 노출 파일·백도어·관리 콘솔이 발견되지 않았습니다.",
                Recommendation = "정기적으로 재점검하세요."
            });

        return findings;
    }

    private async Task<HttpResponseMessage?> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            return await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }

    private async Task Gentle(VulnScanConfig cfg, CancellationToken ct)
    {
        if (cfg.DelayMs > 0) await Task.Delay(cfg.DelayMs, ct);
    }

    private async Task ProbeFileAsync(string url, Severity sev, FindingCategory cat,
        string title, string rec, List<SecurityFinding> findings, VulnScanConfig cfg,
        CancellationToken ct)
    {
        var resp = await GetAsync(url, ct);
        await Gentle(cfg, ct);
        if (resp == null) return;
        using (resp)
        {
            int code = (int)resp.StatusCode;
            if (code == 200)
            {
                var body = await SafeBody(resp, ct);
                // 소프트 404(200 + 로그인/에러 페이지) 완화: 내용 길이 확인
                findings.Add(new SecurityFinding
                {
                    Category = cat, Severity = sev,
                    Title = $"{title}: {new Uri(url).AbsolutePath}",
                    Detail = $"HTTP 200 · {body.Length:N0} bytes 로 접근 가능",
                    Recommendation = rec
                });
            }
            else if (code is 401 or 403)
            {
                findings.Add(new SecurityFinding
                {
                    Category = cat, Severity = Severity.Info,
                    Title = $"경로 존재(보호됨): {new Uri(url).AbsolutePath}",
                    Detail = $"HTTP {code} — 파일이 존재하나 접근이 차단됨",
                    Recommendation = "가능하면 경로 자체를 노출하지 마세요(404 처리)."
                });
            }
        }
    }

    private async Task ProbeConsoleAsync(string url, Severity sev,
        List<SecurityFinding> findings, VulnScanConfig cfg, CancellationToken ct)
    {
        var resp = await GetAsync(url, ct);
        await Gentle(cfg, ct);
        if (resp == null) return;
        using (resp)
        {
            int code = (int)resp.StatusCode;
            if (code == 200)
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Exposure, Severity = sev,
                    Title = $"관리/디버그 콘솔 접근 가능: {new Uri(url).AbsolutePath}",
                    Detail = "HTTP 200 — 인증 없이 접근됩니다.",
                    Recommendation = "인증·IP 제한을 적용하거나 외부 접근을 차단하세요."
                });
            else if (code is 401 or 403)
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Exposure, Severity = Severity.Info,
                    Title = $"콘솔 존재(보호됨): {new Uri(url).AbsolutePath}",
                    Detail = $"HTTP {code}",
                    Recommendation = "노출 최소화를 위해 관리 경로를 표준 포트/도메인 밖으로 분리하세요."
                });
        }
    }

    private async Task ProbeBackdoorAsync(string url, List<SecurityFinding> findings,
        VulnScanConfig cfg, CancellationToken ct)
    {
        var resp = await GetAsync(url, ct);
        await Gentle(cfg, ct);
        if (resp == null) return;
        using (resp)
        {
            if ((int)resp.StatusCode == 200)
            {
                var body = await SafeBody(resp, ct);
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Exposure, Severity = Severity.Critical,
                    Title = $"웹셸/백도어 의심 파일: {new Uri(url).AbsolutePath}",
                    Detail = $"HTTP 200 · {body.Length:N0} bytes — 알려진 백도어 파일명이 응답합니다.",
                    Recommendation = "즉시 파일을 격리·삭제하고 서버 무결성·침해 여부를 점검하세요."
                });
            }
        }
    }

    private async Task ProbeDirListingAsync(string url, List<SecurityFinding> findings,
        VulnScanConfig cfg, CancellationToken ct)
    {
        var resp = await GetAsync(url, ct);
        await Gentle(cfg, ct);
        if (resp == null) return;
        using (resp)
        {
            if ((int)resp.StatusCode != 200) return;
            var body = await SafeBody(resp, ct);
            if (body.Contains("Index of /", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Directory listing for", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Exposure, Severity = Severity.Medium,
                    Title = $"디렉터리 리스팅 노출: {new Uri(url).AbsolutePath}",
                    Detail = "자동 디렉터리 인덱스가 활성화되어 파일 목록이 노출됩니다.",
                    Recommendation = "웹서버의 디렉터리 인덱싱(autoindex/Options Indexes)을 비활성화하세요."
                });
            }
        }
    }

    private async Task ProbeVerboseErrorAsync(string root, List<SecurityFinding> findings,
        VulnScanConfig cfg, CancellationToken ct)
    {
        var url = root + "/siege-lab-does-not-exist-" + Guid.NewGuid().ToString("N")[..8];
        var resp = await GetAsync(url, ct);
        await Gentle(cfg, ct);
        if (resp == null) return;
        using (resp)
        {
            var body = await SafeBody(resp, ct);
            if (ErrorSignatures.IsMatch(body))
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Disclosure, Severity = Severity.High,
                    Title = "상세 오류/스택 트레이스 노출",
                    Detail = "존재하지 않는 경로 요청에 스택 트레이스·프레임워크 내부 정보가 응답에 포함됩니다.",
                    Recommendation = "운영 환경에서 상세 오류를 끄고 커스텀 오류 페이지를 사용하세요."
                });
        }
    }

    private async Task ProbeTraceAsync(string root, List<SecurityFinding> findings,
        CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod("TRACE"), root);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            if ((int)resp.StatusCode == 200)
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Methods, Severity = Severity.Medium,
                    Title = "TRACE 메서드 활성(XST 위험)",
                    Detail = "HTTP TRACE가 200으로 응답합니다. Cross-Site Tracing에 악용될 수 있습니다.",
                    Recommendation = "웹서버/프록시에서 TRACE 메서드를 비활성화하세요."
                });
        }
        catch { /* 무시 */ }
    }

    // 공격적 옵션: HTTP Basic 인증 엔드포인트에 대한 기본 자격증명 확인(비파괴 GET)
    private static readonly (string u, string p)[] DefaultCreds =
    [
        ("admin", "admin"), ("admin", "password"), ("admin", "admin123"),
        ("admin", "1234"), ("root", "root"), ("tomcat", "tomcat"),
    ];

    private async Task ProbeDefaultCredsAsync(string root, List<SecurityFinding> findings,
        VulnScanConfig cfg, CancellationToken ct)
    {
        // 401 Basic 을 반환하는 대표 경로 탐색
        foreach (var path in new[] { "/", "/admin", "/manager/html" })
        {
            var probe = await GetAsync(root + path, ct);
            await Gentle(cfg, ct);
            if (probe == null) continue;
            bool basic;
            using (probe)
            {
                basic = (int)probe.StatusCode == 401 &&
                        probe.Headers.WwwAuthenticate.Any(h =>
                            h.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase));
            }
            if (!basic) continue;

            foreach (var (u, p) in DefaultCreds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, root + path);
                    var token = Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes($"{u}:{p}"));
                    req.Headers.TryAddWithoutValidation("Authorization", "Basic " + token);
                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    await Gentle(cfg, ct);
                    if ((int)resp.StatusCode is >= 200 and < 300)
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = FindingCategory.Exposure, Severity = Severity.Critical,
                            Title = $"기본 자격증명 로그인 성공: {u}/{p}",
                            Detail = $"{path} 에서 기본 계정으로 인증에 성공했습니다.",
                            Recommendation = "즉시 강력한 자격증명으로 변경하고 기본 계정을 비활성화하세요."
                        });
                        return;
                    }
                }
                catch { /* 무시 */ }
            }
        }
    }

    private static async Task<string> SafeBody(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            int take = Math.Min(bytes.Length, 64 * 1024);
            return System.Text.Encoding.UTF8.GetString(bytes, 0, take);
        }
        catch { return ""; }
    }
}
