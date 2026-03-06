using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServeCast.Models;

namespace ServeCast.Services;

/// <summary>ASP.NET Core Kestrel 기반 로컬 HTTP/HTTPS 서버</summary>
public class ServerService
{
    private readonly ServerConfig          _config;
    private readonly Action<RequestLog>    _onRequest;
    private IHost?                         _host;

    public bool IsRunning => _host is not null;

    public ServerService(ServerConfig config, Action<RequestLog> onRequest)
    {
        _config    = config;
        _onRequest = onRequest;
    }

    public async Task StartAsync()
    {
        _host = Host.CreateDefaultBuilder([])
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHostDefaults(web =>
            {
                // ── Kestrel 포트·프로토콜 설정 ──────────────────────────
                web.UseKestrel(opts =>
                {
                    if (_config.UseHttps)
                    {
                        var cert = CertService.GetOrCreate();
                        opts.ListenAnyIP(_config.Port, lo => lo.UseHttps(cert));
                    }
                    else
                    {
                        opts.ListenAnyIP(_config.Port);
                    }
                });

                // ── CORS 서비스 등록 ─────────────────────────────────────
                if (_config.EnableCors)
                    web.ConfigureServices(s => s.AddCors());

                web.Configure(ConfigureApp);
            })
            .Build();

        await _host.StartAsync();
    }

    private void ConfigureApp(IApplicationBuilder app)
    {
        // ── 1. 요청 로깅 (최상단) ────────────────────────────────────────
        app.Use(async (ctx, next) =>
        {
            var log = new RequestLog
            {
                Method = ctx.Request.Method,
                Path   = ctx.Request.Path.Value ?? "/"
            };
            var sw           = Stopwatch.StartNew();
            var originalBody = ctx.Response.Body;

            using var counting = new CountingStream(originalBody);
            ctx.Response.Body  = counting;
            try
            {
                await next(ctx);
            }
            finally
            {
                sw.Stop();
                log.StatusCode    = ctx.Response.StatusCode;
                log.ElapsedMs     = sw.ElapsedMilliseconds;
                log.BytesSent     = counting.BytesWritten;
                ctx.Response.Body = originalBody;
                _onRequest(log);
            }
        });

        // ── 2. CORS ───────────────────────────────────────────────────────
        if (_config.EnableCors)
        {
            app.UseCors(policy =>
            {
                var origins = _config.CorsOrigins
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (origins is ["*"])
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                else
                    policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
            });
        }

        // ── 3. Basic Auth ─────────────────────────────────────────────────
        if (_config.EnableAuth && !string.IsNullOrEmpty(_config.AuthPassword))
        {
            app.Use(async (ctx, next) =>
            {
                if (!ctx.Request.Headers.TryGetValue("Authorization", out var header))
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ServeCast\"";
                    return;
                }

                try
                {
                    var raw     = header.ToString();
                    var b64     = raw.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
                                      ? raw[6..] : raw;
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                    var colon   = decoded.IndexOf(':');
                    var user    = colon >= 0 ? decoded[..colon]      : decoded;
                    var pass    = colon >= 0 ? decoded[(colon + 1)..] : "";

                    if (user == _config.AuthUsername && pass == _config.AuthPassword)
                    {
                        await next(ctx);
                        return;
                    }
                }
                catch { /* Base64 디코딩 실패 */ }

                ctx.Response.StatusCode = 401;
                ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ServeCast\"";
            });
        }

        // ── 4. 정적 파일 서빙 ─────────────────────────────────────────────
        var fp           = new PhysicalFileProvider(_config.FolderPath);
        var contentTypes = new FileExtensionContentTypeProvider();

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fp });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider          = fp,
            ContentTypeProvider   = contentTypes,
            ServeUnknownFileTypes = true,
            DefaultContentType    = "application/octet-stream"
        });

        // ── 5a. 디렉터리 브라우저 ─────────────────────────────────────────
        if (!_config.SpaMode && _config.ShowDirectory)
        {
            app.UseDirectoryBrowser(new DirectoryBrowserOptions { FileProvider = fp });
        }

        // ── 5b. SPA 폴백 (404 → index.html) ──────────────────────────────
        if (_config.SpaMode)
        {
            app.Run(async ctx =>
            {
                var index = fp.GetFileInfo("index.html");
                if (index.Exists)
                {
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await ctx.Response.SendFileAsync(index);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync("index.html not found");
                }
            });
        }
    }

    public async Task StopAsync()
    {
        if (_host is null) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _host.StopAsync(cts.Token);
            _host.Dispose();
        }
        finally
        {
            _host = null;
        }
    }

    /// <summary>LAN IP 주소 가져오기 (UDP 트릭)</summary>
    public static string GetLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 65530);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    // ── 헬퍼: 응답 바이트 수 측정 스트림 ────────────────────────────────
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead  => inner.CanRead;
        public override bool CanSeek  => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length   => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);

        public override int Read(byte[] buffer, int offset, int count)
            => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            BytesWritten += count;
            inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            BytesWritten += count;
            await inner.WriteAsync(buffer, offset, count, ct);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            BytesWritten += buffer.Length;
            await inner.WriteAsync(buffer, ct);
        }

        protected override void Dispose(bool disposing)
        {
            // inner 스트림은 ASP.NET Core 소유 — Dispose 하지 않음
            base.Dispose(disposing);
        }
    }
}
