using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace SiegeLab.Services;

public sealed record HealthSample(DateTime At, bool Up, double LatencyMs, int Status);

public sealed record HealthSnapshot(
    long Checks,
    long UpCount,
    double UptimePercent,
    double LastLatencyMs,
    double AvgLatencyMs,
    int ConsecutiveFailures,
    bool CurrentlyUp,
    int LastStatus,
    IReadOnlyList<HealthSample> Recent);

/// <summary>엔드포인트 주기 헬스체크(가용성·지연 모니터).</summary>
public sealed class HealthMonitor
{
    private readonly HttpClient _http;
    private readonly List<HealthSample> _samples = new();
    private long _checks, _up;
    private double _latSum;
    private int _consecFail;

    public HealthMonitor()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };
        _http = new HttpClient(handler);
    }

    public async Task RunAsync(string url, int intervalSec, int timeoutMs,
        IProgress<HealthSnapshot> progress, CancellationToken ct)
    {
        intervalSec = Math.Clamp(intervalSec, 1, 3600);
        while (!ct.IsCancellationRequested)
        {
            var sample = await CheckOnce(url, timeoutMs, ct);
            Record(sample);
            progress.Report(Build());

            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<HealthSample> CheckOnce(string url, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        var sw = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();
            int code = (int)resp.StatusCode;
            bool up = code is >= 200 and < 400;
            return new HealthSample(DateTime.Now, up, sw.Elapsed.TotalMilliseconds, code);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            sw.Stop();
            return new HealthSample(DateTime.Now, false, sw.Elapsed.TotalMilliseconds, 0);
        }
    }

    private void Record(HealthSample s)
    {
        _checks++;
        if (s.Up) { _up++; _consecFail = 0; }
        else _consecFail++;
        _latSum += s.LatencyMs;
        _samples.Add(s);
        if (_samples.Count > 120) _samples.RemoveAt(0);
    }

    private HealthSnapshot Build()
    {
        var last = _samples.Count > 0 ? _samples[^1] : null;
        return new HealthSnapshot(
            _checks, _up,
            _checks > 0 ? _up * 100.0 / _checks : 0,
            last?.LatencyMs ?? 0,
            _checks > 0 ? _latSum / _checks : 0,
            _consecFail,
            last?.Up ?? false,
            last?.Status ?? 0,
            _samples.ToArray());
    }

    public void Reset()
    {
        _samples.Clear();
        _checks = _up = 0;
        _latSum = 0;
        _consecFail = 0;
    }
}
