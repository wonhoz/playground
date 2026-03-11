using System.Diagnostics;

namespace VpnCast.Services;

/// <summary>
/// Windows 방화벽 규칙으로 킬 스위치 구현.
/// VPN 연결이 끊기면 모든 인터넷 트래픽 차단.
/// </summary>
public static class KillSwitchService
{
    private const string RulePrefix = "VpnCast_KillSwitch";

    public static async Task EnableAsync()
    {
        // 모든 아웃바운드 차단 (기존 규칙 위에 추가)
        await RunNetshAsync($"advfirewall firewall add rule name=\"{RulePrefix}_Block\" " +
            "protocol=any dir=out action=block");

        // WireGuard 기본 포트(51820 UDP) 허용
        await RunNetshAsync($"advfirewall firewall add rule name=\"{RulePrefix}_AllowWG\" " +
            "protocol=UDP dir=out action=allow remoteport=51820");

        // 루프백 허용
        await RunNetshAsync($"advfirewall firewall add rule name=\"{RulePrefix}_AllowLoopback\" " +
            "protocol=any dir=out action=allow remoteip=127.0.0.1/8");

        // DNS 허용 (VPN DNS)
        await RunNetshAsync($"advfirewall firewall add rule name=\"{RulePrefix}_AllowDNS\" " +
            "protocol=UDP dir=out action=allow remoteport=53");
    }

    public static async Task DisableAsync()
    {
        await RunNetshAsync($"advfirewall firewall delete rule name=\"{RulePrefix}_Block\"");
        await RunNetshAsync($"advfirewall firewall delete rule name=\"{RulePrefix}_AllowWG\"");
        await RunNetshAsync($"advfirewall firewall delete rule name=\"{RulePrefix}_AllowLoopback\"");
        await RunNetshAsync($"advfirewall firewall delete rule name=\"{RulePrefix}_AllowDNS\"");
    }

    private static async Task RunNetshAsync(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };
        try
        {
            var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();
        }
        catch { }
    }
}
