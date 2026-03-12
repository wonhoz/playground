using System.Diagnostics;
using System.Net.NetworkInformation;
using DnsFlip.Models;

namespace DnsFlip.Services;

public static class DnsService
{
    public static List<string> GetActiveAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                        or NetworkInterfaceType.Wireless80211
                        or NetworkInterfaceType.GigabitEthernet)
            .Select(n => n.Name)
            .ToList();
    }

    public static string GetCurrentDns(string adapterName)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == adapterName);
        if (nic == null) return "N/A";

        var props = nic.GetIPProperties();
        var dns = props.DnsAddresses
            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(a => a.ToString())
            .ToList();

        return dns.Count > 0 ? string.Join(", ", dns) : "DHCP";
    }

    public static async Task<(bool Success, string? Error)> ApplyPresetAsync(string adapterName, DnsPreset preset)
    {
        try
        {
            if (string.IsNullOrEmpty(preset.Primary))
            {
                // DHCP mode
                await RunNetshAsync($"interface ip set dns name=\"{adapterName}\" source=dhcp");
            }
            else
            {
                await RunNetshAsync($"interface ip set dns name=\"{adapterName}\" static {preset.Primary}");
                if (!string.IsNullOrEmpty(preset.Secondary))
                    await RunNetshAsync($"interface ip add dns name=\"{adapterName}\" {preset.Secondary} index=2");
            }

            // Flush DNS cache
            await RunNetshAsync("interface ip delete dnscache");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(long Ms, bool Success)> PingDnsAsync(string dnsServer)
    {
        if (string.IsNullOrEmpty(dnsServer)) return (-1, false);

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(dnsServer, 3000);
            return reply.Status == IPStatus.Success
                ? (reply.RoundtripTime, true)
                : (-1, false);
        }
        catch
        {
            return (-1, false);
        }
    }

    public static bool IsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    // UI 차단 방지: WaitForExitAsync()로 비동기 대기 (기존 동기 WaitForExit 최대 10s 블로킹 제거)
    private static async Task RunNetshAsync(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi) ?? throw new Exception("netsh 실행 실패");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync();
            throw new Exception($"netsh 오류 (코드 {proc.ExitCode}): {err}");
        }
    }
}
