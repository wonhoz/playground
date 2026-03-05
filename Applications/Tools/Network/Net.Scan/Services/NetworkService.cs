namespace NetScan.Services;

/// <summary>어댑터 정보 + 서브넷 감지 + arp -a 파싱</summary>
public static class NetworkService
{
    // ── 어댑터 / 게이트웨이 ──────────────────────────────────────────
    public record AdapterInfo(string Name, IPAddress LocalIp, string Gateway, string Subnet);

    public static List<AdapterInfo> GetActiveAdapters()
    {
        var result = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                                         or NetworkInterfaceType.Tunnel) continue;

            var ipProps = nic.GetIPProperties();
            var gateway = ipProps.GatewayAddresses
                                 .Select(g => g.Address)
                                 .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (gateway == null) continue;

            var unicast = ipProps.UnicastAddresses
                                 .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                                                   && !IPAddress.IsLoopback(u.Address));
            if (unicast == null) continue;

            var subnet = GetSubnetRange(unicast.Address, unicast.IPv4Mask);
            result.Add(new AdapterInfo(nic.Name, unicast.Address, gateway.ToString(), subnet));
        }
        return result;
    }

    /// <summary>192.168.1.1/24 형태 CIDR 문자열 반환</summary>
    private static string GetSubnetRange(IPAddress ip, IPAddress mask)
    {
        var ipBytes   = ip.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var netBytes  = ipBytes.Select((b, i) => (byte)(b & maskBytes[i])).ToArray();
        int prefix    = maskBytes.Select(b => Convert.ToString(b, 2).Count(c => c == '1')).Sum();
        return $"{new IPAddress(netBytes)}/{prefix}";
    }

    // ── IP 범위 열거 ─────────────────────────────────────────────────
    public static List<string> GetIpRange(string cidr)
    {
        var parts  = cidr.Split('/');
        if (parts.Length != 2) return [];

        var baseIp = IPAddress.Parse(parts[0]).GetAddressBytes();
        int prefix = int.Parse(parts[1]);
        int hostBits = 32 - prefix;
        int count    = (int)Math.Pow(2, hostBits) - 2; // 브로드캐스트 + 네트워크 제외
        if (count <= 0) return [];

        uint baseUint = (uint)(baseIp[0] << 24 | baseIp[1] << 16 | baseIp[2] << 8 | baseIp[3]);
        uint mask     = 0xFFFFFFFF << hostBits;
        uint network  = baseUint & mask;

        var list = new List<string>(count);
        for (uint i = 1; i <= (uint)count; i++)
        {
            uint addr = network + i;
            list.Add($"{(addr >> 24) & 0xFF}.{(addr >> 16) & 0xFF}.{(addr >> 8) & 0xFF}.{addr & 0xFF}");
        }
        return list;
    }

    // ── arp -a 파싱 ──────────────────────────────────────────────────
    /// <returns>IP → MAC 딕셔너리 (MAC은 xx-xx-xx-xx-xx-xx 소문자)</returns>
    public static async Task<Dictionary<string, string>> GetArpTableAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("arp", "-a")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            // 예: "  192.168.1.1          aa-bb-cc-dd-ee-ff     동적"
            var lineRe = new System.Text.RegularExpressions.Regex(
                @"(\d{1,3}(?:\.\d{1,3}){3})\s+([\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2})");

            foreach (System.Text.RegularExpressions.Match m in lineRe.Matches(output))
            {
                var ip  = m.Groups[1].Value;
                var mac = m.Groups[2].Value.Replace(':', '-').ToLowerInvariant();
                // ff-ff-ff-ff-ff-ff (브로드캐스트) 제외
                if (mac != "ff-ff-ff-ff-ff-ff")
                    result[ip] = mac;
            }
        }
        catch { /* ARP 실패 시 빈 테이블 반환 */ }
        return result;
    }

    // ── DNS 역방향 조회 ──────────────────────────────────────────────
    public static async Task<string> ResolveHostnameAsync(string ip)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ip).WaitAsync(TimeSpan.FromSeconds(2));
            return entry.HostName;
        }
        catch { return ""; }
    }
}
