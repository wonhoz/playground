using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using SiegeLab.Models;

namespace SiegeLab.Services;

/// <summary>
/// 비침습 보안 점검. 소수의 정상 요청만 보내 TLS·헤더·쿠키·정보 노출·CORS·메서드를 진단한다.
/// 공격 페이로드를 보내지 않으며 서버 상태를 변경하지 않는다.
/// </summary>
public sealed class SecurityAuditor
{
    private readonly HttpClient _http;

    public SecurityAuditor()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SiegeLab/1.0 (+authorized-audit)");
    }

    public async Task<List<SecurityFinding>> RunAsync(string url, IProgress<string> log,
                                                      CancellationToken ct)
    {
        var findings = new List<SecurityFinding>();
        var uri = new Uri(url);

        // 1) TLS/인증서 ------------------------------------------------
        if (uri.Scheme == "https")
        {
            log.Report("TLS/인증서 점검 중…");
            await InspectTlsAsync(uri, findings, ct);
        }
        else
        {
            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Tls,
                Severity = Severity.High,
                Title = "평문 HTTP 사용",
                Detail = "대상이 HTTPS가 아닌 HTTP로 서비스됩니다. 전송 구간 도청·변조에 취약합니다.",
                Recommendation = "전 구간 HTTPS 적용 및 HTTP→HTTPS 강제 리다이렉트를 설정하세요."
            });
        }

        // 2) 기본 응답 헤더 수집 ---------------------------------------
        log.Report("응답 헤더 점검 중…");
        HttpResponseMessage? resp = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            AuditHeaders(resp, uri, findings);
            AuditCookies(resp, uri, findings);
            AuditDisclosure(resp, findings);
        }
        catch (Exception ex)
        {
            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Availability,
                Severity = Severity.Medium,
                Title = "기본 요청 실패",
                Detail = $"GET {url} 요청이 실패했습니다: {ex.Message}",
                Recommendation = "대상 URL과 네트워크 접근성을 확인하세요."
            });
        }

        // 3) 리다이렉트 체인 ------------------------------------------
        log.Report("리다이렉트 체인 추적 중…");
        await AuditRedirectAsync(url, findings, ct);

        // 4) CORS -----------------------------------------------------
        log.Report("CORS 정책 점검 중…");
        await AuditCorsAsync(url, findings, ct);

        // 5) HTTP 메서드(OPTIONS/TRACE) --------------------------------
        log.Report("허용 메서드 점검 중…");
        await AuditMethodsAsync(url, findings, ct);

        resp?.Dispose();
        return findings;
    }

    // ── TLS ──────────────────────────────────────────────────────
    private static async Task InspectTlsAsync(Uri uri, List<SecurityFinding> findings,
                                              CancellationToken ct)
    {
        try
        {
            int port = uri.Port > 0 ? uri.Port : 443;
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(uri.Host, port, ct);

            X509Certificate2? cert = null;
            using var ssl = new SslStream(tcp.GetStream(), false,
                (_, c, _, _) =>
                {
                    if (c != null) cert = new X509Certificate2(c);
                    return true; // 검증은 별도 판단, 연결 자체는 허용
                });

            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = uri.Host
            }, ct);

            // 프로토콜 버전
            var proto = ssl.SslProtocol;
            var protoName = proto.ToString();
            bool weak = protoName.Contains("Tls11") || protoName.Contains("Tls10") ||
                        protoName.Contains("Ssl");
            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Tls,
                Severity = weak ? Severity.High : Severity.Ok,
                Title = $"TLS 프로토콜: {protoName}",
                Detail = weak
                    ? "구형 TLS/SSL 프로토콜이 협상되었습니다."
                    : "최신 TLS 프로토콜로 협상되었습니다.",
                Recommendation = weak
                    ? "TLS 1.2 이상만 허용하고 TLS 1.0/1.1 및 SSLv3를 비활성화하세요."
                    : "현행 유지."
            });

            if (cert != null)
            {
                var daysLeft = (cert.NotAfter - DateTime.Now).TotalDays;
                Severity sev = daysLeft < 0 ? Severity.Critical
                             : daysLeft < 14 ? Severity.High
                             : daysLeft < 30 ? Severity.Medium : Severity.Ok;
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Tls,
                    Severity = sev,
                    Title = daysLeft < 0
                        ? "인증서 만료됨"
                        : $"인증서 만료까지 {daysLeft:F0}일",
                    Detail = $"주체: {cert.Subject}\n발급자: {cert.Issuer}\n" +
                             $"유효기간: {cert.NotBefore:yyyy-MM-dd} ~ {cert.NotAfter:yyyy-MM-dd}",
                    Recommendation = daysLeft < 30
                        ? "인증서를 갱신하고 자동 갱신을 설정하세요."
                        : "현행 유지."
                });

                // 서명 알고리즘
                var sig = cert.SignatureAlgorithm.FriendlyName ?? "";
                if (sig.Contains("sha1", StringComparison.OrdinalIgnoreCase) ||
                    sig.Contains("md5", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new SecurityFinding
                    {
                        Category = FindingCategory.Tls,
                        Severity = Severity.High,
                        Title = $"약한 인증서 서명: {sig}",
                        Detail = "SHA-1/MD5 서명 인증서는 위조 위험이 있습니다.",
                        Recommendation = "SHA-256 이상 서명 인증서로 교체하세요."
                    });
                }
            }
        }
        catch (Exception ex)
        {
            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Tls,
                Severity = Severity.Medium,
                Title = "TLS 핸드셰이크 확인 실패",
                Detail = ex.Message,
                Recommendation = "포트 개방 및 인증서 구성을 확인하세요."
            });
        }
    }

    // ── 보안 헤더 ────────────────────────────────────────────────
    private static void AuditHeaders(HttpResponseMessage resp, Uri uri,
                                     List<SecurityFinding> findings)
    {
        bool Has(string name) =>
            resp.Headers.Contains(name) || resp.Content.Headers.Contains(name);
        string? Get(string name)
        {
            if (resp.Headers.TryGetValues(name, out var v)) return string.Join(", ", v);
            if (resp.Content.Headers.TryGetValues(name, out var v2)) return string.Join(", ", v2);
            return null;
        }

        void Check(string header, Severity sev, string missTitle, string rec)
        {
            if (Has(header))
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Headers, Severity = Severity.Ok,
                    Title = $"{header} 설정됨", Detail = Get(header) ?? "", Recommendation = "현행 유지."
                });
            else
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Headers, Severity = sev,
                    Title = missTitle, Detail = $"{header} 헤더가 없습니다.", Recommendation = rec
                });
        }

        if (uri.Scheme == "https")
            Check("Strict-Transport-Security", Severity.High, "HSTS 미설정",
                "Strict-Transport-Security: max-age=31536000; includeSubDomains 를 설정하세요.");

        Check("Content-Security-Policy", Severity.High, "CSP 미설정",
            "XSS·데이터 인젝션 완화를 위해 Content-Security-Policy를 설정하세요.");
        Check("X-Content-Type-Options", Severity.Medium, "X-Content-Type-Options 미설정",
            "X-Content-Type-Options: nosniff 를 설정하세요.");
        Check("X-Frame-Options", Severity.Medium, "X-Frame-Options 미설정 (클릭재킹)",
            "X-Frame-Options: DENY 또는 CSP frame-ancestors 를 설정하세요.");
        Check("Referrer-Policy", Severity.Low, "Referrer-Policy 미설정",
            "Referrer-Policy: strict-origin-when-cross-origin 를 권장합니다.");
        Check("Permissions-Policy", Severity.Low, "Permissions-Policy 미설정",
            "불필요한 브라우저 기능을 제한하는 Permissions-Policy를 설정하세요.");
    }

    // ── 쿠키 ─────────────────────────────────────────────────────
    private static void AuditCookies(HttpResponseMessage resp, Uri uri,
                                     List<SecurityFinding> findings)
    {
        if (!resp.Headers.TryGetValues("Set-Cookie", out var cookies)) return;
        foreach (var c in cookies)
        {
            var name = c.Split('=')[0].Trim();
            bool secure   = c.Contains("Secure", StringComparison.OrdinalIgnoreCase);
            bool httpOnly = c.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase);
            bool sameSite = c.Contains("SameSite", StringComparison.OrdinalIgnoreCase);

            var missing = new List<string>();
            if (uri.Scheme == "https" && !secure) missing.Add("Secure");
            if (!httpOnly) missing.Add("HttpOnly");
            if (!sameSite) missing.Add("SameSite");

            findings.Add(new SecurityFinding
            {
                Category = FindingCategory.Cookies,
                Severity = missing.Count == 0 ? Severity.Ok
                         : missing.Contains("HttpOnly") ? Severity.Medium : Severity.Low,
                Title = missing.Count == 0
                    ? $"쿠키 '{name}' 플래그 양호"
                    : $"쿠키 '{name}' 누락 플래그: {string.Join(", ", missing)}",
                Detail = c,
                Recommendation = missing.Count == 0
                    ? "현행 유지."
                    : $"{string.Join(", ", missing)} 플래그를 추가하세요."
            });
        }
    }

    // ── 정보 노출 ────────────────────────────────────────────────
    private static void AuditDisclosure(HttpResponseMessage resp, List<SecurityFinding> findings)
    {
        void Leak(string header)
        {
            if (resp.Headers.TryGetValues(header, out var v))
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Disclosure,
                    Severity = Severity.Low,
                    Title = $"{header} 노출: {string.Join(", ", v)}",
                    Detail = "서버·프레임워크 버전이 노출되면 공격 표적화에 이용됩니다.",
                    Recommendation = $"{header} 헤더를 제거하거나 값을 숨기세요."
                });
        }
        Leak("Server");
        Leak("X-Powered-By");
        Leak("X-AspNet-Version");
        Leak("X-AspNetMvc-Version");
        Leak("X-Generator");
    }

    // ── 리다이렉트 체인 ──────────────────────────────────────────
    private async Task AuditRedirectAsync(string url, List<SecurityFinding> findings,
                                          CancellationToken ct)
    {
        try
        {
            var current = url;
            for (int i = 0; i < 5; i++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, current);
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)resp.StatusCode is < 300 or >= 400) break;
                var loc = resp.Headers.Location?.ToString();
                if (loc == null) break;
                var next = new Uri(new Uri(current), loc).ToString();

                if (current.StartsWith("https", StringComparison.OrdinalIgnoreCase) &&
                    next.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new SecurityFinding
                    {
                        Category = FindingCategory.Tls, Severity = Severity.High,
                        Title = "HTTPS→HTTP 다운그레이드 리다이렉트",
                        Detail = $"{current} → {next}",
                        Recommendation = "리다이렉트가 평문 HTTP로 내려가지 않도록 하세요."
                    });
                }
                current = next;
            }
        }
        catch { /* 무시 */ }
    }

    // ── CORS ─────────────────────────────────────────────────────
    private async Task AuditCorsAsync(string url, List<SecurityFinding> findings,
                                      CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Origin", "https://siege-lab.example.com");
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.Headers.TryGetValues("Access-Control-Allow-Origin", out var v))
            {
                var acao = string.Join(", ", v);
                bool wildcard = acao.Trim() == "*";
                bool reflected = acao.Contains("siege-lab.example.com");
                bool creds = resp.Headers.TryGetValues("Access-Control-Allow-Credentials", out var cv)
                             && cv.Any(x => x.Equals("true", StringComparison.OrdinalIgnoreCase));

                Severity sev = (reflected && creds) ? Severity.High
                             : wildcard ? Severity.Medium : Severity.Ok;
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Cors, Severity = sev,
                    Title = reflected ? "CORS Origin 반사(임의 오리진 허용)"
                          : wildcard ? "CORS 와일드카드(*) 허용" : "CORS 제한적",
                    Detail = $"Access-Control-Allow-Origin: {acao}" +
                             (creds ? " / Allow-Credentials: true" : ""),
                    Recommendation = sev == Severity.Ok
                        ? "현행 유지."
                        : "허용 오리진을 명시적 화이트리스트로 제한하고, 자격증명 허용 시 와일드카드/반사를 금지하세요."
                });
            }
        }
        catch { /* 무시 */ }
    }

    // ── HTTP 메서드 ──────────────────────────────────────────────
    private async Task AuditMethodsAsync(string url, List<SecurityFinding> findings,
                                         CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Options, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.Content.Headers.Allow.Count > 0 || resp.Headers.Contains("Allow"))
            {
                var allow = resp.Content.Headers.Allow.Count > 0
                    ? string.Join(", ", resp.Content.Headers.Allow)
                    : string.Join(", ", resp.Headers.GetValues("Allow"));
                bool risky = allow.Contains("TRACE", StringComparison.OrdinalIgnoreCase) ||
                             allow.Contains("PUT", StringComparison.OrdinalIgnoreCase) ||
                             allow.Contains("DELETE", StringComparison.OrdinalIgnoreCase);
                findings.Add(new SecurityFinding
                {
                    Category = FindingCategory.Methods,
                    Severity = risky ? Severity.Medium : Severity.Ok,
                    Title = $"허용 메서드: {allow}",
                    Detail = risky ? "쓰기/추적 계열 메서드가 노출되어 있습니다." : "표준 메서드만 노출됩니다.",
                    Recommendation = risky
                        ? "TRACE는 비활성화하고 PUT/DELETE는 인증·인가로 보호하세요."
                        : "현행 유지."
                });
            }
        }
        catch { /* 무시 */ }
    }

    /// <summary>발견 목록으로 0~100 보안 점수 산정.</summary>
    public static (int score, string grade) Score(IEnumerable<SecurityFinding> findings)
    {
        int penalty = findings.Sum(f => f.Severity.Weight());
        int score = Math.Clamp(100 - penalty, 0, 100);
        string grade = score >= 90 ? "A" : score >= 80 ? "B" : score >= 70 ? "C"
                     : score >= 60 ? "D" : score >= 40 ? "E" : "F";
        return (score, grade);
    }
}
