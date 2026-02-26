using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MockDesk.Models;

namespace MockDesk.Services;

public class MockServerService
{
    private WebApplication?           _app;
    private readonly ConcurrentQueue<MockEndpoint> _endpoints = new();
    public  Action<RequestLogEntry>?   OnRequest;

    public bool IsRunning => _app != null;

    public void SetEndpoints(IEnumerable<MockEndpoint> endpoints)
    {
        while (_endpoints.TryDequeue(out _)) { }
        foreach (var ep in endpoints) _endpoints.Enqueue(ep);
    }

    public async Task StartAsync(int port)
    {
        if (_app != null) return;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.ClearProviders(); // 콘솔 로그 억제

        _app = builder.Build();

        // Catch-all 터미널 미들웨어
        _app.Run(async context =>
        {
            var method = context.Request.Method.ToUpper();
            var path   = context.Request.Path.Value ?? "/";

            var ep = _endpoints
                .Where(e => e.Enabled)
                .FirstOrDefault(e =>
                    e.Method.Equals(method, StringComparison.OrdinalIgnoreCase) &&
                    MatchPath(e.Path, path));

            var sw = Stopwatch.StartNew();
            if (ep != null)
            {
                if (ep.DelayMs > 0) await Task.Delay(ep.DelayMs);
                context.Response.StatusCode  = ep.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(ep.ResponseBody);
                sw.Stop();
                OnRequest?.Invoke(new RequestLogEntry
                {
                    Method     = method,
                    Path       = path,
                    StatusCode = ep.StatusCode,
                    Matched    = true,
                    DelayMs    = sw.ElapsedMilliseconds + ep.DelayMs
                });
            }
            else
            {
                context.Response.StatusCode  = 404;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"error\":\"Not Found\"}");
                sw.Stop();
                OnRequest?.Invoke(new RequestLogEntry
                {
                    Method     = method,
                    Path       = path,
                    StatusCode = 404,
                    Matched    = false,
                    DelayMs    = sw.ElapsedMilliseconds
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
        // 정확 일치 또는 와일드카드 (*) 지원
        if (pattern == actual) return true;
        if (pattern.EndsWith("*"))
            return actual.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return pattern.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }

    public static string ExportJson(IEnumerable<MockEndpoint> endpoints)
        => JsonSerializer.Serialize(endpoints, new JsonSerializerOptions { WriteIndented = true });

    public static List<MockEndpoint> ImportJson(string json)
        => JsonSerializer.Deserialize<List<MockEndpoint>>(json) ?? [];
}
