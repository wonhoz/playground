namespace NetScan.Services;

public class ScanService
{
    private CancellationTokenSource? _cts;
    public bool IsScanning { get; private set; }

    public event Action<NetworkDevice>? DeviceFound;
    public event Action<int, int>?      ProgressChanged; // (done, total)
    public event Action?                ScanCompleted;

    // ── 스캔 시작 ────────────────────────────────────────────────────
    public async Task StartScanAsync(string cidr)
    {
        if (IsScanning) return;
        IsScanning = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var ips = NetworkService.GetIpRange(cidr);
            int total = ips.Count;
            int done  = 0;

            // 1단계: 병렬 Ping (SemaphoreSlim 50 동시)
            var sem     = new SemaphoreSlim(50, 50);
            var online  = new ConcurrentDictionary<string, long>(); // IP → 응답시간(ms)

            var pingTasks = ips.Select(async ip =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success)
                        online[ip] = reply.RoundtripTime;
                }
                catch { }
                finally
                {
                    sem.Release();
                    Interlocked.Increment(ref done);
                    ProgressChanged?.Invoke(done, total);
                }
            }).ToList();

            await Task.WhenAll(pingTasks);
            if (ct.IsCancellationRequested) return;

            // 2단계: ARP 테이블 수집
            var arpTable = await NetworkService.GetArpTableAsync();

            // 3단계: 온라인 기기마다 정보 보강 후 알림
            var enrichTasks = online.Select(async kv =>
            {
                if (ct.IsCancellationRequested) return;

                var ip  = kv.Key;
                var ms  = kv.Value;
                var mac = arpTable.TryGetValue(ip, out var m) ? m : "";

                var vendor = OuiService.Lookup(mac);
                var icon   = OuiService.GetDeviceIcon(vendor);
                var host   = await NetworkService.ResolveHostnameAsync(ip);

                var device = new NetworkDevice
                {
                    IpAddress  = ip,
                    MacAddress = mac,
                    Hostname   = host,
                    Vendor     = vendor,
                    DeviceType = icon,
                    IsOnline   = true,
                    PingMs     = ms,
                    LastSeen   = DateTime.Now
                };
                device.AddPingSample(ms);
                DeviceFound?.Invoke(device);
            });

            await Task.WhenAll(enrichTasks);
        }
        finally
        {
            IsScanning = false;
            ScanCompleted?.Invoke();
        }
    }

    public void StopScan()
    {
        _cts?.Cancel();
        IsScanning = false;
    }
}
