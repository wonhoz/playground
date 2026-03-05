namespace NetScan.Services;

public static class PortScanService
{
    // 자주 사용하는 포트 목록 (기본)
    public static readonly int[] CommonPorts =
    [
        21, 22, 23, 25, 53, 80, 110, 139, 143, 443,
        445, 554, 587, 631, 993, 995, 1080, 1194, 1723,
        3306, 3389, 5900, 8080, 8443, 8888, 9090
    ];

    public static readonly Dictionary<int, string> PortNames = new()
    {
        [21]   = "FTP",
        [22]   = "SSH",
        [23]   = "Telnet",
        [25]   = "SMTP",
        [53]   = "DNS",
        [80]   = "HTTP",
        [110]  = "POP3",
        [139]  = "NetBIOS",
        [143]  = "IMAP",
        [443]  = "HTTPS",
        [445]  = "SMB",
        [554]  = "RTSP",
        [587]  = "SMTPS",
        [631]  = "IPP",
        [993]  = "IMAPS",
        [995]  = "POP3S",
        [1080] = "SOCKS",
        [1194] = "OpenVPN",
        [1723] = "PPTP",
        [3306] = "MySQL",
        [3389] = "RDP",
        [5900] = "VNC",
        [8080] = "HTTP-Alt",
        [8443] = "HTTPS-Alt",
        [8888] = "HTTP-Dev",
        [9090] = "HTTP-Dev2"
    };

    /// <summary>포트 목록을 병렬로 스캔해 열린 포트 반환</summary>
    public static async Task<List<int>> ScanAsync(
        string ip,
        IEnumerable<int>? ports = null,
        int timeoutMs = 800,
        CancellationToken ct = default)
    {
        var targetPorts = (ports ?? CommonPorts).ToArray();
        var open        = new ConcurrentBag<int>();
        var sem         = new SemaphoreSlim(20, 20);

        var tasks = targetPorts.Select(async port =>
        {
            await sem.WaitAsync(ct);
            try
            {
                using var tcp = new TcpClient();
                var conn = tcp.ConnectAsync(ip, port, ct).AsTask();
                if (await Task.WhenAny(conn, Task.Delay(timeoutMs, ct)) == conn && !conn.IsFaulted)
                    open.Add(port);
            }
            catch { }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks);
        return [.. open.OrderBy(p => p)];
    }

    public static string GetPortName(int port)
        => PortNames.TryGetValue(port, out var name) ? name : port.ToString();
}
