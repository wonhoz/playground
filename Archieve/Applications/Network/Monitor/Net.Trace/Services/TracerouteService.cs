namespace Net.Trace.Services;

public class TracerouteService
{
    public async IAsyncEnumerable<HopInfo> TraceAsync(
        string target, int maxHops = 30, int timeout = 3000,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        IPAddress? address = null;
        string? resolveError = null;
        try
        {
            if (!IPAddress.TryParse(target, out address))
            {
                var entry = await Dns.GetHostEntryAsync(target, ct);
                address = entry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                          ?? entry.AddressList.First();
            }
        }
        catch (Exception ex) { resolveError = ex.Message; }

        if (resolveError != null)
        {
            yield return new HopInfo { HopNumber = 0, IsTimeout = true, Country = $"오류: {resolveError}" };
            yield break;
        }

        string destStr = address!.ToString();

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            ct.ThrowIfCancellationRequested();
            var hop = await ProbeHopAsync(address, ttl, timeout);
            yield return hop;
            if (!hop.IsTimeout && hop.Ip == destStr) yield break;
        }
    }

    static async Task<HopInfo> ProbeHopAsync(IPAddress target, int ttl, int timeout)
    {
        var rtts   = new List<double>();
        string? hopIp = null;
        int lost = 0;
        const int probes = 3;

        for (int i = 0; i < probes; i++)
        {
            try
            {
                using var ping = new Ping();
                var opts  = new PingOptions(ttl, dontFragment: true);
                var reply = await ping.SendPingAsync(target, timeout, new byte[32], opts);

                if (reply.Status is IPStatus.TtlExpired or IPStatus.Success)
                {
                    hopIp ??= reply.Address.ToString();
                    rtts.Add(reply.RoundtripTime);
                }
                else lost++;
            }
            catch { lost++; }
        }

        var hop = new HopInfo
        {
            HopNumber = ttl,
            Ip        = hopIp,
            Sent      = probes,
            Lost      = lost,
            IsTimeout = hopIp == null,
        };

        if (rtts.Count > 0)
        {
            hop.RttMin = rtts.Min();
            hop.RttMax = rtts.Max();
            hop.RttAvg = rtts.Average();
        }

        // Async DNS reverse lookup (best effort)
        if (hopIp != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var entry = await Dns.GetHostEntryAsync(hopIp);
                    hop.Hostname = entry.HostName;
                }
                catch { /* ignored */ }
            });
        }

        return hop;
    }
}
