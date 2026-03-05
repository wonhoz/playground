namespace NetScan.Services;

/// <summary>등록된 기기들을 주기적으로 핑해 IsOnline / PingMs / PingHistory 업데이트</summary>
public class PingMonitor : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly ObservableCollection<NetworkDevice> _devices;
    private bool _running;

    public event Action<NetworkDevice>? PingUpdated;

    public PingMonitor(ObservableCollection<NetworkDevice> devices, int intervalMs = 5000)
    {
        _devices = devices;
        _timer   = new System.Timers.Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await TickAsync();
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _timer.Start();
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
    }

    private async Task TickAsync()
    {
        // 스냅샷으로 작업 (컬렉션 변경 방지)
        NetworkDevice[] snapshot;
        lock (_devices) { snapshot = [.. _devices]; }

        var sem = new SemaphoreSlim(30, 30);
        var tasks = snapshot.Select(async dev =>
        {
            await sem.WaitAsync();
            try
            {
                using var ping  = new Ping();
                var reply = await ping.SendPingAsync(dev.IpAddress, 2000);
                bool online = reply.Status == IPStatus.Success;
                long ms     = online ? reply.RoundtripTime : -1;

                dev.IsOnline = online;
                dev.PingMs   = ms;
                dev.AddPingSample(ms);
                if (online) dev.LastSeen = DateTime.Now;

                PingUpdated?.Invoke(dev);
            }
            catch { }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
