using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace LinkVault.Services;

/// <summary>
/// 브라우저 확장 연동용 로컬 HTTP 서버.
/// GET/POST http://localhost:34567/add?url=...&title=...
/// </summary>
public class ApiServerService : IDisposable
{
    public const int Port = 34567;
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public event Action<string, string>? BookmarkAddRequested; // (url, title)

    public void Start()
    {
        if (_listener.IsListening) return;
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        try
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _ = ListenAsync(_cts.Token);
        }
        catch
        {
            // 포트 충돌 등 — 조용히 무시 (확장 연동 없이도 앱 동작)
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }

            _ = Task.Run(() => HandleRequest(ctx), ct);
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            var query = HttpUtility.ParseQueryString(req.Url?.Query ?? "");
            var url   = query["url"]   ?? "";
            var title = query["title"] ?? "";

            // POST body 도 지원
            if (req.HttpMethod == "POST" && req.HasEntityBody)
            {
                using var sr = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = sr.ReadToEnd();
                var parsed = HttpUtility.ParseQueryString(body);
                if (string.IsNullOrEmpty(url))   url   = parsed["url"]   ?? "";
                if (string.IsNullOrEmpty(title)) title = parsed["title"] ?? "";
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                BookmarkAddRequested?.Invoke(url, title);
                WriteResponse(res, 200, """{"ok":true}""");
            }
            else
            {
                WriteResponse(res, 400, """{"error":"url required"}""");
            }
        }
        catch { /* 개별 요청 오류 무시 */ }
        finally { ctx.Response.Close(); }
    }

    private static void WriteResponse(HttpListenerResponse res, int status, string json)
    {
        res.StatusCode = status;
        res.ContentType = "application/json; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_listener.IsListening) _listener.Stop();
    }
}
