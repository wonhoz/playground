using System.Diagnostics;
using PortWatch.Models;

namespace PortWatch.Services;

public static class PortScanService
{
    public static async Task<List<PortEntry>> ScanAsync()
    {
        var psi = new ProcessStartInfo("netstat", "-ano")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc is null) return [];
        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        var entries = new List<PortEntry>();
        var procCache = new Dictionary<int, (string name, string path)>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("TCP") && !line.StartsWith("UDP")) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            var proto = parts[0];
            var localFull  = parts[1];
            var remoteFull = parts[2];

            string state;
            int pid;
            if (proto == "TCP" && parts.Length >= 5)
            {
                state = parts[3];
                if (!int.TryParse(parts[4], out pid)) continue;
            }
            else // UDP
            {
                state = "";
                if (!int.TryParse(parts[^1], out pid)) continue;
            }

            var localPort = ExtractPort(localFull);
            if (localPort == 0) continue;

            if (!procCache.TryGetValue(pid, out var info))
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    string path = "";
                    try { path = p.MainModule?.FileName ?? ""; } catch { }
                    info = (p.ProcessName, path);
                }
                catch { info = ("System", ""); }
                procCache[pid] = info;
            }

            entries.Add(new PortEntry
            {
                Protocol    = proto,
                LocalPort   = localPort,
                LocalAddr   = StripPort(localFull),
                RemoteAddr  = remoteFull,
                State       = state,
                Pid         = pid,
                ProcessName = info.name,
                ProcessPath = info.path
            });
        }

        return [.. entries.OrderBy(e => e.LocalPort)];
    }

    private static int ExtractPort(string addr)
    {
        var idx = addr.LastIndexOf(':');
        return idx >= 0 && int.TryParse(addr[(idx + 1)..], out var p) ? p : 0;
    }

    private static string StripPort(string addr)
    {
        var idx = addr.LastIndexOf(':');
        return idx >= 0 ? addr[..idx] : addr;
    }
}
