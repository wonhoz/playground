using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SignalFlow.Server;

public class FlowServer
{
    private WebApplication? _app;

    // SSE 연결 목록: clientId → HttpResponse
    private readonly ConcurrentDictionary<string, HttpResponse> _sseClients = new();

    public bool IsRunning => _app != null;

    // 이벤트 수신 콜백 (UI 업데이트용)
    public Action<string>? OnLog;

    public async Task StartAsync(int port)
    {
        if (_app != null) return;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();

        _app = builder.Build();

        // ── GET / → WebClient.html ──────────────────────────────────
        _app.MapGet("/", async context =>
        {
            var asm  = typeof(FlowServer).Assembly;
            var name = asm.GetManifestResourceNames()
                          .FirstOrDefault(n => n.EndsWith("WebClient.html"));
            if (name == null)
            {
                context.Response.StatusCode = 404;
                return;
            }
            await using var stream = asm.GetManifestResourceStream(name)!;
            using var reader       = new StreamReader(stream);
            var html               = await reader.ReadToEndAsync();
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });

        // ── SignalR Hub ──────────────────────────────────────────────
        _app.MapHub<EventHub>("/hub");

        // ── GET /sse → SSE 스트림 ─────────────────────────────────────
        _app.MapGet("/sse", async context =>
        {
            context.Response.Headers["Content-Type"]  = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"]    = "keep-alive";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";

            var clientId = Guid.NewGuid().ToString("N");
            _sseClients[clientId] = context.Response;

            OnLog?.Invoke($"SSE 연결: {clientId[..8]}");

            // 연결 유지 — CancellationToken 대기
            var ct = context.RequestAborted;
            try
            {
                await context.Response.WriteAsync($"data: {{\"connected\":true}}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _sseClients.TryRemove(clientId, out _);
                OnLog?.Invoke($"SSE 해제: {clientId[..8]}");
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
        _sseClients.Clear();
    }

    public async Task PublishAsync(ServerEvent evt)
    {
        if (_app is null) return;

        var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // 1) SignalR 브로드캐스트
        var hub = _app.Services.GetRequiredService<IHubContext<EventHub>>();
        await hub.Clients.All.SendAsync("Event", evt);

        // 2) SSE 클라이언트 브로드캐스트
        var dead = new List<string>();
        foreach (var (id, resp) in _sseClients)
        {
            try
            {
                await resp.WriteAsync($"data: {json}\n\n");
                await resp.Body.FlushAsync();
            }
            catch
            {
                dead.Add(id);
            }
        }
        foreach (var id in dead) _sseClients.TryRemove(id, out _);
    }
}
