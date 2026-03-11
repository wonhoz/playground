using System.Diagnostics;
using System.ServiceProcess;
using VpnCast.Models;

namespace VpnCast.Services;

public static class TunnelService
{
    private static readonly string WireGuardExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "WireGuard", "wireguard.exe");

    private static readonly string OpenVpnExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "OpenVPN", "bin", "openvpn.exe");

    public static bool WireGuardAvailable => File.Exists(WireGuardExe);
    public static bool OpenVpnAvailable   => File.Exists(OpenVpnExe);

    // ── 연결 상태 조회 ──────────────────────────────────────────────
    public static TunnelStatus GetStatus(TunnelProfile profile)
    {
        if (profile.Type == TunnelType.WireGuard)
        {
            try
            {
                using var sc = new ServiceController($"WireGuardTunnel${profile.Name}");
                return sc.Status == ServiceControllerStatus.Running
                    ? TunnelStatus.Connected
                    : TunnelStatus.Disconnected;
            }
            catch { return TunnelStatus.Disconnected; }
        }
        // OpenVPN: openvpn.exe 프로세스 실행 여부 확인
        return Process.GetProcessesByName("openvpn").Length > 0
            ? TunnelStatus.Connected
            : TunnelStatus.Disconnected;
    }

    // ── 연결 ────────────────────────────────────────────────────────
    public static async Task<(bool success, string? error)> ConnectAsync(TunnelProfile profile)
    {
        try
        {
            if (profile.Type == TunnelType.WireGuard)
            {
                if (!WireGuardAvailable)
                    return (false, "WireGuard가 설치되지 않았습니다.\nhttps://www.wireguard.com 에서 설치하세요.");

                if (!File.Exists(profile.ConfigPath))
                    return (false, $"설정 파일을 찾을 수 없습니다:\n{profile.ConfigPath}");

                // WireGuard 터널 서비스 설치 (관리자 권한 필요)
                await RunElevatedAsync(WireGuardExe, $"/installtunnel \"{profile.ConfigPath}\"");
                await Task.Delay(2000);

                // 서비스 시작 확인
                try
                {
                    using var sc = new ServiceController($"WireGuardTunnel${profile.Name}");
                    if (sc.Status != ServiceControllerStatus.Running)
                        sc.Start();
                }
                catch { }

                return (true, null);
            }
            else // OpenVPN
            {
                if (!OpenVpnAvailable)
                    return (false, "OpenVPN이 설치되지 않았습니다.\nhttps://www.openvpn.net 에서 설치하세요.");

                if (!File.Exists(profile.ConfigPath))
                    return (false, $"설정 파일을 찾을 수 없습니다:\n{profile.ConfigPath}");

                var psi = new ProcessStartInfo(OpenVpnExe, $"--config \"{profile.ConfigPath}\"")
                {
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
                return (true, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── 연결 해제 ────────────────────────────────────────────────────
    public static async Task<(bool success, string? error)> DisconnectAsync(TunnelProfile profile)
    {
        try
        {
            if (profile.Type == TunnelType.WireGuard)
            {
                if (!WireGuardAvailable)
                    return (false, "WireGuard가 설치되지 않았습니다.");

                // 서비스 중지
                try
                {
                    using var sc = new ServiceController($"WireGuardTunnel${profile.Name}");
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                }
                catch { }

                // 터널 서비스 제거
                await RunElevatedAsync(WireGuardExe, $"/uninstalltunnel \"{profile.Name}\"");
                return (true, null);
            }
            else // OpenVPN
            {
                var procs = Process.GetProcessesByName("openvpn");
                foreach (var proc in procs)
                {
                    try { proc.Kill(); } catch { }
                }
                return (true, null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task RunElevatedAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi);
        if (proc != null) await proc.WaitForExitAsync();
    }

    // ── 설정 파일에서 서버 주소 추출 ──────────────────────────────────
    public static string? ExtractServerAddress(string configPath, TunnelType type)
    {
        try
        {
            var lines = File.ReadAllLines(configPath);
            if (type == TunnelType.WireGuard)
            {
                var endpoint = lines.FirstOrDefault(l =>
                    l.TrimStart().StartsWith("Endpoint", StringComparison.OrdinalIgnoreCase));
                return endpoint?.Split('=').LastOrDefault()?.Trim().Split(':').FirstOrDefault();
            }
            else
            {
                var remote = lines.FirstOrDefault(l =>
                    l.TrimStart().StartsWith("remote ", StringComparison.OrdinalIgnoreCase));
                return remote?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            }
        }
        catch { return null; }
    }
}
