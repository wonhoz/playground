using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MockServer.Services;

public class MockServerService
{
    private WebApplication?        _app;
    private volatile List<MockRoute> _activeRoutes = [];
    private readonly Lock          _lock = new();

    public Action<RequestLog>? OnRequest;
    public bool IsRunning => _app != null;

    public void ApplyRoutes(IEnumerable<MockRoute> routes)
    {
        lock (_lock)
        {
            _activeRoutes = routes.Where(r => r.Enabled).ToList();
        }
    }

    public async Task StartAsync(int port)
    {
        if (_app != null) return;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        // Catch-all 터미널 미들웨어 (Use 오버로드 모호성 방지 → Run 사용)
        _app.Run(async context =>
        {
            var method = context.Request.Method.ToUpper();
            var path   = context.Request.Path.Value ?? "/";

            // CORS 헤더 — 모든 응답에 추가
            context.Response.Headers["Access-Control-Allow-Origin"]  = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, Accept";

            // OPTIONS 프리플라이트 즉시 204 반환
            if (method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return;
            }

            List<MockRoute> snapshot;
            lock (_lock) { snapshot = _activeRoutes; }

            var route = snapshot.FirstOrDefault(r =>
                r.Method.Equals(method, StringComparison.OrdinalIgnoreCase) &&
                MatchPath(r.Path, path));

            var sw = Stopwatch.StartNew();
            if (route != null)
            {
                if (route.Delay > 0) await Task.Delay(route.Delay);
                context.Response.StatusCode  = route.Status;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(route.Response.Trim());
                sw.Stop();
                OnRequest?.Invoke(new RequestLog
                {
                    Method  = method,
                    Path    = path,
                    Status  = route.Status,
                    Matched = true,
                    DelayMs = sw.ElapsedMilliseconds
                });
            }
            else
            {
                context.Response.StatusCode  = 404;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"error\":\"Not Found\"}");
                sw.Stop();
                OnRequest?.Invoke(new RequestLog
                {
                    Method  = method,
                    Path    = path,
                    Status  = 404,
                    Matched = false,
                    DelayMs = sw.ElapsedMilliseconds
                });
            }
        });

        await _app.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    private static bool MatchPath(string pattern, string actual)
    {
        if (pattern == actual) return true;
        if (pattern.EndsWith("*"))
            return actual.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return pattern.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }
}
