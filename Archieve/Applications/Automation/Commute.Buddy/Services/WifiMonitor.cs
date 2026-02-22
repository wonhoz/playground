using System.Timers;

namespace CommuteBuddy.Services;

public class WifiMonitor : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private string _currentSsid = "";
    private bool   _disposed;

    public event Action<string>? SsidChanged;

    public string CurrentSsid => _currentSsid;

    public WifiMonitor()
    {
        _timer = new System.Timers.Timer(30_000); // 30초 폴링
        _timer.Elapsed += (_, _) => Poll();
    }

    public void Start()
    {
        _currentSsid = GetCurrentSsid(); // 초기 SSID (이벤트 없이)
        _timer.Start();
    }

    // 수동 즉시 체크
    public void CheckNow() => Poll();

    private void Poll()
    {
        var ssid = GetCurrentSsid();
        if (ssid == _currentSsid) return;
        _currentSsid = ssid;
        SsidChanged?.Invoke(ssid);
    }

    private static string GetCurrentSsid()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            // "    SSID                   : MyNetwork" 형식에서 추출
            var match = Regex.Match(output, @"^\s+SSID\s+:\s+(.+)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }
        catch { return ""; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
