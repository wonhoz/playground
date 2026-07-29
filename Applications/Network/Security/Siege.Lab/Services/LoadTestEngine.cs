using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using SiegeLab.Models;

namespace SiegeLab.Services;

/// <summary>부하 테스트 실시간 스냅샷(UI 바인딩용 불변 값).</summary>
public sealed record LoadSnapshot(
    double ElapsedSec,
    long Total,
    long Success,
    long Failed,
    double CurrentRps,
    double AvgRps,
    double LatMin,
    double LatAvg,
    double LatP50,
    double LatP90,
    double LatP95,
    double LatP99,
    double LatMax,
    double MbPerSec,
    int ActiveWorkers,
    IReadOnlyDictionary<string, long> StatusDist,
    IReadOnlyDictionary<string, long> ErrorDist);

/// <summary>비동기 HTTP 부하 생성기.</summary>
public sealed class LoadTestEngine
{
    private readonly HttpClient _http;

    private long _total, _success, _failed, _bytes;
    private long _windowCount;           // 최근 1초 창 카운트
    private readonly object _lock = new();
    private readonly List<double> _lat = new(capacity: 1 << 16);
    private readonly Dictionary<string, long> _status = new();
    private readonly Dictionary<string, long> _errors = new();
    private int _activeWorkers;
    private long _lastWindowTotal;

    public LoadTestEngine()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer  = SafetyGate.MaxConcurrency,
            AllowAutoRedirect        = false,
            AutomaticDecompression   = DecompressionMethods.All,
            UseCookies               = false
        };
        _http = new HttpClient(handler);
        // 개별 요청 타임아웃은 CancellationToken으로 제어하므로 전체는 크게.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task RunAsync(LoadTestConfig cfg, IProgress<LoadSnapshot> progress,
                               CancellationToken ct)
    {
        int concurrency = SafetyGate.ClampConcurrency(cfg.Concurrency);
        int duration    = SafetyGate.ClampDuration(cfg.DurationSeconds);
        int maxRps      = SafetyGate.ClampRps(cfg.MaxRps);
        int rampUp      = Math.Clamp(cfg.RampUpSeconds, 0, duration);

        var rate = maxRps > 0 ? new RateLimiter(maxRps) : null;

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runCts.CancelAfter(TimeSpan.FromSeconds(duration));
        var token = runCts.Token;

        var sw = Stopwatch.StartNew();

        // 1초마다 스냅샷 보고
        var reporter = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    progress.Report(BuildSnapshot(sw.Elapsed.TotalSeconds));
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        // 워커 램프업: 각 워커를 시차 기동
        var workers = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            int delayMs = rampUp > 0 ? (int)((long)rampUp * 1000 * i / concurrency) : 0;
            workers[i] = WorkerLoop(cfg, rate, delayMs, token);
        }

        try { await Task.WhenAll(workers); }
        catch (OperationCanceledException) { }

        sw.Stop();
        try { await reporter; } catch { }

        // 최종 스냅샷
        progress.Report(BuildSnapshot(sw.Elapsed.TotalSeconds));
    }

    private async Task WorkerLoop(LoadTestConfig cfg, RateLimiter? rate, int startDelayMs,
                                  CancellationToken token)
    {
        if (startDelayMs > 0)
        {
            try { await Task.Delay(startDelayMs, token); }
            catch (OperationCanceledException) { return; }
        }

        Interlocked.Increment(ref _activeWorkers);
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (rate != null)
                {
                    try { await rate.WaitAsync(token); }
                    catch (OperationCanceledException) { break; }
                }
                await SendOnce(cfg, token);
            }
        }
        finally { Interlocked.Decrement(ref _activeWorkers); }
    }

    private async Task SendOnce(LoadTestConfig cfg, CancellationToken token)
    {
        using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        reqCts.CancelAfter(cfg.TimeoutMs);

        var sw = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod(cfg.Method), cfg.Url);
            if (!string.IsNullOrEmpty(cfg.Body) &&
                cfg.Method is not "GET" and not "HEAD")
            {
                req.Content = new StringContent(cfg.Body, Encoding.UTF8, cfg.ContentType);
            }
            ApplyHeader(req, cfg.ExtraHeader);

            using var resp = await _http.SendAsync(
                req, HttpCompletionOption.ResponseHeadersRead, reqCts.Token);

            long len = 0;
            var bytes = await resp.Content.ReadAsByteArrayAsync(reqCts.Token);
            len = bytes.LongLength;

            sw.Stop();
            RecordSuccess(sw.Elapsed.TotalMilliseconds, (int)resp.StatusCode, len);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            sw.Stop();
            RecordError("timeout");
        }
        catch (OperationCanceledException)
        {
            // 전체 종료 — 카운트하지 않음
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            RecordError(ex.HttpRequestError.ToString());
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordError(ex.GetType().Name);
        }
    }

    private static void ApplyHeader(HttpRequestMessage req, string extra)
    {
        if (string.IsNullOrWhiteSpace(extra)) return;
        foreach (var line in extra.Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var name = line[..idx].Trim();
            var val  = line[(idx + 1)..].Trim();
            if (name.Length == 0) continue;
            try { req.Headers.TryAddWithoutValidation(name, val); } catch { }
        }
    }

    private void RecordSuccess(double ms, int status, long bytes)
    {
        Interlocked.Increment(ref _total);
        Interlocked.Increment(ref _success);
        Interlocked.Increment(ref _windowCount);
        Interlocked.Add(ref _bytes, bytes);
        string bucket = $"{status / 100}xx ({status})";
        lock (_lock)
        {
            if (_lat.Count < 2_000_000) _lat.Add(ms);
            _status[bucket] = _status.GetValueOrDefault(bucket) + 1;
        }
    }

    private void RecordError(string kind)
    {
        Interlocked.Increment(ref _total);
        Interlocked.Increment(ref _failed);
        Interlocked.Increment(ref _windowCount);
        lock (_lock)
        {
            _errors[kind] = _errors.GetValueOrDefault(kind) + 1;
        }
    }

    private LoadSnapshot BuildSnapshot(double elapsed)
    {
        double[] snapshot;
        Dictionary<string, long> status, errors;
        lock (_lock)
        {
            snapshot = _lat.ToArray();
            status = new(_status);
            errors = new(_errors);
        }

        long total   = Interlocked.Read(ref _total);
        long success = Interlocked.Read(ref _success);
        long failed  = Interlocked.Read(ref _failed);
        long bytes   = Interlocked.Read(ref _bytes);

        long windowNow = Interlocked.Read(ref _windowCount);
        double curRps = windowNow - _lastWindowTotal;   // 최근 ~1초
        _lastWindowTotal = windowNow;

        Array.Sort(snapshot);
        double Pct(double p) => Percentile(snapshot, p);

        double avg = snapshot.Length > 0 ? snapshot.Average() : 0;
        double min = snapshot.Length > 0 ? snapshot[0] : 0;
        double max = snapshot.Length > 0 ? snapshot[^1] : 0;
        double mbps = elapsed > 0 ? bytes / 1024.0 / 1024.0 / elapsed : 0;
        double avgRps = elapsed > 0 ? total / elapsed : 0;

        return new LoadSnapshot(
            elapsed, total, success, failed, curRps, avgRps,
            min, avg, Pct(50), Pct(90), Pct(95), Pct(99), max,
            mbps, Volatile.Read(ref _activeWorkers), status, errors);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        double rank = p / 100.0 * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        double frac = rank - lo;
        return sorted[lo] * (1 - frac) + sorted[hi] * frac;
    }

    /// <summary>단순 토큰 페이싱 레이트 리미터(전역 RPS 상한).</summary>
    private sealed class RateLimiter(int rps)
    {
        private readonly double _intervalTicks = Stopwatch.Frequency / (double)rps;
        private long _next = Stopwatch.GetTimestamp();
        private readonly object _l = new();

        public async Task WaitAsync(CancellationToken ct)
        {
            long slot;
            lock (_l)
            {
                long now = Stopwatch.GetTimestamp();
                long baseline = Math.Max(now, _next);
                slot = baseline;
                _next = baseline + (long)_intervalTicks;
            }
            long waitTicks = slot - Stopwatch.GetTimestamp();
            if (waitTicks > 0)
            {
                double ms = waitTicks * 1000.0 / Stopwatch.Frequency;
                if (ms >= 1) await Task.Delay((int)ms, ct);
            }
        }
    }
}
