namespace NetScan.Services;

/// <summary>IEEE OUI (Organizationally Unique Identifier) 조회 서비스.</summary>
public static class OuiService
{
    // ── OUI 데이터베이스 (MAC 첫 3바이트 → 제조사) ───────────────
    private static readonly Dictionary<string, string> _db = new(StringComparer.OrdinalIgnoreCase)
    {
        // Apple
        ["00:0A:27"]="Apple", ["00:0A:95"]="Apple", ["00:11:24"]="Apple",
        ["00:14:51"]="Apple", ["00:16:CB"]="Apple", ["00:17:F2"]="Apple",
        ["00:1B:63"]="Apple", ["00:1C:B3"]="Apple", ["00:1D:4F"]="Apple",
        ["00:1E:52"]="Apple", ["00:1F:5B"]="Apple", ["00:21:E9"]="Apple",
        ["00:22:41"]="Apple", ["00:23:12"]="Apple", ["00:23:6C"]="Apple",
        ["00:25:00"]="Apple", ["00:25:4B"]="Apple", ["00:25:BC"]="Apple",
        ["04:0C:CE"]="Apple", ["04:26:65"]="Apple", ["04:DB:56"]="Apple",
        ["04:F7:E4"]="Apple", ["08:6D:41"]="Apple", ["0C:3E:9F"]="Apple",
        ["0C:74:C2"]="Apple", ["0C:77:1A"]="Apple", ["10:40:F3"]="Apple",
        ["18:65:90"]="Apple", ["1C:91:48"]="Apple", ["20:78:F0"]="Apple",
        ["24:A0:74"]="Apple", ["28:CF:DA"]="Apple", ["2C:61:F6"]="Apple",
        ["34:36:3B"]="Apple", ["38:C9:86"]="Apple", ["3C:07:54"]="Apple",
        ["40:D3:2D"]="Apple", ["44:D8:84"]="Apple", ["48:43:7C"]="Apple",
        ["4C:57:CA"]="Apple", ["58:B0:35"]="Apple", ["5C:F5:DA"]="Apple",
        ["60:03:08"]="Apple", ["60:F8:1D"]="Apple", ["64:A3:CB"]="Apple",
        ["6C:40:08"]="Apple", ["70:11:24"]="Apple", ["74:E1:B6"]="Apple",
        ["78:4F:43"]="Apple", ["7C:C3:A1"]="Apple", ["80:82:23"]="Apple",
        ["84:85:06"]="Apple", ["88:E9:FE"]="Apple", ["8C:7B:9D"]="Apple",
        ["90:B2:1F"]="Apple", ["94:BF:2D"]="Apple", ["98:9E:63"]="Apple",
        ["A4:CF:99"]="Apple", ["A8:66:7F"]="Apple", ["AC:87:A3"]="Apple",
        ["B0:19:C6"]="Apple", ["B4:F0:AB"]="Apple", ["B8:09:8A"]="Apple",
        ["BC:52:B7"]="Apple", ["C0:9F:42"]="Apple", ["C4:B3:01"]="Apple",
        ["C8:E0:EB"]="Apple", ["CC:29:F5"]="Apple", ["D0:03:4B"]="Apple",
        ["D4:61:9D"]="Apple", ["D8:00:4D"]="Apple", ["DC:2B:2A"]="Apple",
        ["E0:AC:CB"]="Apple", ["E4:CE:8F"]="Apple", ["E8:04:0B"]="Apple",
        ["EC:85:2F"]="Apple", ["F0:18:98"]="Apple", ["F4:1B:A1"]="Apple",
        ["F8:62:14"]="Apple", ["FC:25:3F"]="Apple",
        // Samsung
        ["00:15:99"]="Samsung", ["00:16:32"]="Samsung", ["00:17:C9"]="Samsung",
        ["00:21:19"]="Samsung", ["00:23:39"]="Samsung", ["00:24:90"]="Samsung",
        ["08:EC:A9"]="Samsung", ["10:D5:42"]="Samsung", ["20:64:32"]="Samsung",
        ["30:19:66"]="Samsung", ["38:AA:3C"]="Samsung", ["40:0E:85"]="Samsung",
        ["50:01:BB"]="Samsung", ["50:F5:20"]="Samsung", ["54:88:0E"]="Samsung",
        ["78:25:AD"]="Samsung", ["80:65:6D"]="Samsung", ["8C:C8:CD"]="Samsung",
        ["94:35:0A"]="Samsung", ["CC:07:AB"]="Samsung", ["D0:22:BE"]="Samsung",
        ["F4:42:8F"]="Samsung", ["FC:A1:3E"]="Samsung",
        // Cisco
        ["00:00:0C"]="Cisco",  ["00:01:42"]="Cisco",  ["00:01:43"]="Cisco",
        ["00:06:7C"]="Cisco",  ["00:0B:BE"]="Cisco",  ["00:0D:65"]="Cisco",
        ["00:0F:23"]="Cisco",  ["00:0F:90"]="Cisco",  ["00:12:01"]="Cisco",
        ["00:13:80"]="Cisco",  ["00:14:A9"]="Cisco",  ["00:15:2B"]="Cisco",
        ["00:16:C7"]="Cisco",  ["00:17:94"]="Cisco",  ["00:18:19"]="Cisco",
        ["00:19:AA"]="Cisco",  ["00:1A:2F"]="Cisco",  ["00:1A:6C"]="Cisco",
        ["00:1B:54"]="Cisco",  ["00:1C:10"]="Cisco",  ["00:1D:45"]="Cisco",
        ["00:1E:BD"]="Cisco",  ["00:1F:26"]="Cisco",  ["00:21:A0"]="Cisco",
        ["00:22:BD"]="Cisco",  ["00:23:EA"]="Cisco",  ["00:24:13"]="Cisco",
        ["00:25:45"]="Cisco",  ["00:26:CB"]="Cisco",
        // Intel
        ["00:02:B3"]="Intel",  ["00:03:47"]="Intel",  ["00:04:23"]="Intel",
        ["00:07:E9"]="Intel",  ["00:0E:0C"]="Intel",  ["00:0E:35"]="Intel",
        ["00:11:11"]="Intel",  ["00:13:02"]="Intel",  ["00:13:20"]="Intel",
        ["00:15:00"]="Intel",  ["00:16:36"]="Intel",  ["00:16:76"]="Intel",
        ["00:16:EA"]="Intel",  ["00:18:DE"]="Intel",  ["00:19:D1"]="Intel",
        ["00:1B:21"]="Intel",  ["00:1C:BF"]="Intel",  ["00:1D:E0"]="Intel",
        ["00:1E:64"]="Intel",  ["00:1E:65"]="Intel",  ["00:21:6A"]="Intel",
        ["00:22:FB"]="Intel",  ["00:23:14"]="Intel",  ["00:24:D7"]="Intel",
        ["00:27:10"]="Intel",  ["10:02:B5"]="Intel",  ["28:D2:44"]="Intel",
        ["38:DE:AD"]="Intel",  ["48:45:20"]="Intel",  ["54:27:1E"]="Intel",
        ["60:67:20"]="Intel",  ["68:17:29"]="Intel",  ["7C:76:35"]="Intel",
        ["8C:EC:4B"]="Intel",  ["94:65:9C"]="Intel",  ["A0:36:9F"]="Intel",
        ["AC:FD:CE"]="Intel",  ["B0:35:9F"]="Intel",  ["B4:96:91"]="Intel",
        ["C4:D9:87"]="Intel",  ["D4:BE:D9"]="Intel",  ["E4:70:B8"]="Intel",
        ["F8:63:3F"]="Intel",
        // TP-Link
        ["50:C7:BF"]="TP-Link", ["54:AF:97"]="TP-Link", ["60:E3:27"]="TP-Link",
        ["C4:6E:1F"]="TP-Link", ["14:CC:20"]="TP-Link", ["18:A6:F7"]="TP-Link",
        ["20:F3:A3"]="TP-Link", ["24:69:68"]="TP-Link", ["30:B5:C2"]="TP-Link",
        ["3C:84:6A"]="TP-Link", ["50:FA:84"]="TP-Link", ["6C:5A:B0"]="TP-Link",
        ["74:DA:38"]="TP-Link", ["98:DA:C4"]="TP-Link", ["A0:F3:C1"]="TP-Link",
        ["AC:84:C6"]="TP-Link", ["B0:48:7A"]="TP-Link", ["D8:0D:17"]="TP-Link",
        ["E0:28:6D"]="TP-Link", ["EC:08:6B"]="TP-Link", ["F4:F2:6D"]="TP-Link",
        // ASUS
        ["00:0E:A6"]="ASUS", ["00:11:2F"]="ASUS", ["00:13:D4"]="ASUS",
        ["00:15:F2"]="ASUS", ["00:17:31"]="ASUS", ["00:18:F3"]="ASUS",
        ["00:1A:92"]="ASUS", ["00:1D:60"]="ASUS", ["00:1E:8C"]="ASUS",
        ["10:BF:48"]="ASUS",  ["14:DA:E9"]="ASUS", ["1C:87:2C"]="ASUS",
        ["20:CF:30"]="ASUS",  ["30:85:A9"]="ASUS", ["34:97:F6"]="ASUS",
        ["50:46:5D"]="ASUS",  ["54:04:A6"]="ASUS", ["60:45:CB"]="ASUS",
        ["70:4D:7B"]="ASUS",  ["74:D0:2B"]="ASUS", ["88:D7:F6"]="ASUS",
        ["90:E6:BA"]="ASUS",  ["AC:9E:17"]="ASUS", ["BC:EE:7B"]="ASUS",
        ["E0:3F:49"]="ASUS",  ["E8:9A:8F"]="ASUS", ["F8:32:E4"]="ASUS",
        // Huawei
        ["00:18:82"]="Huawei", ["00:1E:10"]="Huawei", ["00:25:9E"]="Huawei",
        ["04:BD:70"]="Huawei", ["04:C0:6F"]="Huawei", ["04:F9:38"]="Huawei",
        ["0C:37:DC"]="Huawei", ["10:1B:54"]="Huawei", ["14:B9:68"]="Huawei",
        ["20:08:ED"]="Huawei", ["28:31:52"]="Huawei", ["34:6B:D3"]="Huawei",
        ["40:CB:C0"]="Huawei", ["48:00:31"]="Huawei", ["48:46:FB"]="Huawei",
        ["54:25:EA"]="Huawei", ["5C:C3:07"]="Huawei", ["60:DE:44"]="Huawei",
        ["6C:8D:C1"]="Huawei", ["70:72:CF"]="Huawei", ["78:1D:BA"]="Huawei",
        ["80:38:BC"]="Huawei", ["8C:34:FD"]="Huawei", ["90:17:AC"]="Huawei",
        ["9C:28:EF"]="Huawei", ["A4:99:47"]="Huawei", ["AC:E2:15"]="Huawei",
        ["B4:15:13"]="Huawei", ["BC:25:E0"]="Huawei", ["C8:14:79"]="Huawei",
        ["D4:6A:A8"]="Huawei", ["E0:19:1D"]="Huawei", ["E4:A7:C5"]="Huawei",
        ["EC:23:3D"]="Huawei", ["F4:CB:A2"]="Huawei", ["FC:3F:DB"]="Huawei",
        // Netgear
        ["00:09:5B"]="Netgear", ["00:0F:B5"]="Netgear", ["00:14:6C"]="Netgear",
        ["00:18:4D"]="Netgear", ["00:1B:2F"]="Netgear", ["00:1E:2A"]="Netgear",
        ["00:22:3F"]="Netgear", ["00:24:B2"]="Netgear", ["00:26:F2"]="Netgear",
        ["20:0C:C8"]="Netgear", ["28:C6:8E"]="Netgear", ["2C:B0:5D"]="Netgear",
        ["30:46:9A"]="Netgear", ["44:94:FC"]="Netgear", ["6C:B0:CE"]="Netgear",
        ["A0:21:B7"]="Netgear", ["A0:40:A0"]="Netgear", ["C0:3F:0E"]="Netgear",
        // Raspberry Pi
        ["B8:27:EB"]="Raspberry Pi", ["DC:A6:32"]="Raspberry Pi",
        ["E4:5F:01"]="Raspberry Pi", ["D8:3A:DD"]="Raspberry Pi",
        ["2C:CF:67"]="Raspberry Pi",
        // Microsoft
        ["00:15:5D"]="Microsoft (Hyper-V)", ["00:17:FB"]="Microsoft",
        ["00:1D:D8"]="Microsoft",           ["28:18:78"]="Microsoft",
        ["00:50:F2"]="Microsoft",           ["30:59:B7"]="Microsoft",
        ["58:82:A8"]="Microsoft",           ["60:45:BD"]="Microsoft",
        ["7C:1E:52"]="Microsoft",           ["98:5F:D3"]="Microsoft",
        ["DC:53:60"]="Microsoft",           ["E0:D5:5E"]="Microsoft",
        ["00:03:FF"]="Microsoft",
        // VMware / Hypervisors
        ["00:0C:29"]="VMware", ["00:50:56"]="VMware", ["00:05:69"]="VMware",
        ["08:00:27"]="VirtualBox", ["52:54:00"]="QEMU/KVM",
        // Dell
        ["00:06:5B"]="Dell", ["00:08:74"]="Dell", ["00:0B:DB"]="Dell",
        ["00:0D:56"]="Dell", ["00:11:43"]="Dell", ["00:12:3F"]="Dell",
        ["00:13:72"]="Dell", ["00:14:22"]="Dell", ["00:15:C5"]="Dell",
        ["00:16:F0"]="Dell", ["00:18:8B"]="Dell", ["00:19:B9"]="Dell",
        ["00:1A:A0"]="Dell", ["00:1C:23"]="Dell", ["00:1D:09"]="Dell",
        ["00:1E:4F"]="Dell", ["14:18:77"]="Dell", ["18:03:73"]="Dell",
        ["24:B6:FD"]="Dell", ["28:F1:0E"]="Dell", ["34:17:EB"]="Dell",
        ["44:A8:42"]="Dell", ["54:9F:35"]="Dell", ["78:2B:CB"]="Dell",
        // HP
        ["00:01:E6"]="HP", ["00:02:A5"]="HP", ["00:0E:7F"]="HP",
        ["00:10:83"]="HP", ["00:11:0A"]="HP", ["00:12:79"]="HP",
        ["00:13:21"]="HP", ["00:14:38"]="HP", ["00:15:60"]="HP",
        ["00:16:35"]="HP", ["00:17:08"]="HP", ["00:18:71"]="HP",
        ["00:19:BB"]="HP", ["00:1A:4B"]="HP", ["00:1B:78"]="HP",
        ["00:1C:C4"]="HP", ["00:1D:BE"]="HP", ["00:1E:0B"]="HP",
        ["18:A9:05"]="HP", ["1C:C1:DE"]="HP", ["28:92:4A"]="HP",
        ["3C:D9:2B"]="HP", ["40:B0:34"]="HP", ["58:20:B1"]="HP",
        ["70:5A:0F"]="HP", ["94:57:A5"]="HP", ["A0:1D:48"]="HP",
        ["C4:34:6B"]="HP", ["D4:C9:EF"]="HP", ["E0:07:1B"]="HP",
        // LG
        ["00:1E:75"]="LG", ["00:1F:6B"]="LG", ["00:26:E2"]="LG",
        ["1C:99:4C"]="LG", ["50:55:27"]="LG", ["A0:39:F7"]="LG",
        ["B8:AD:3E"]="LG", ["C4:36:6C"]="LG", ["CC:2D:83"]="LG",
        // Xiaomi
        ["00:9E:C8"]="Xiaomi", ["04:CF:8C"]="Xiaomi", ["08:21:EF"]="Xiaomi",
        ["10:2A:B3"]="Xiaomi", ["14:F6:5A"]="Xiaomi", ["18:59:36"]="Xiaomi",
        ["20:82:C0"]="Xiaomi", ["28:6C:07"]="Xiaomi", ["34:80:B3"]="Xiaomi",
        ["38:A4:ED"]="Xiaomi", ["58:44:98"]="Xiaomi", ["64:09:80"]="Xiaomi",
        ["64:B4:73"]="Xiaomi", ["68:DF:DD"]="Xiaomi", ["78:11:DC"]="Xiaomi",
        ["8C:BE:BE"]="Xiaomi", ["9C:99:A0"]="Xiaomi", ["A0:86:C6"]="Xiaomi",
        ["B0:E2:35"]="Xiaomi", ["F0:B4:29"]="Xiaomi", ["F4:8B:32"]="Xiaomi",
        // Amazon
        ["00:BB:3A"]="Amazon Echo", ["0C:47:C9"]="Amazon", ["34:D2:70"]="Amazon",
        ["40:B4:CD"]="Amazon", ["44:65:0D"]="Amazon", ["68:37:E9"]="Amazon",
        ["6C:56:97"]="Amazon", ["74:75:48"]="Amazon", ["84:D6:D0"]="Amazon",
        ["AC:63:BE"]="Amazon", ["B4:7C:9C"]="Amazon", ["F0:4F:7C"]="Amazon",
        ["F0:81:73"]="Amazon", ["FC:65:DE"]="Amazon",
        // Google
        ["00:1A:11"]="Google",    ["08:9E:08"]="Google",  ["1C:F2:9A"]="Google",
        ["48:D6:D5"]="Google",    ["54:60:09"]="Google",  ["6C:AD:F8"]="Google",
        ["7C:2E:BD"]="Google",    ["A4:77:33"]="Google",  ["D8:6C:63"]="Google",
        ["F4:F5:D8"]="Google",    ["F8:8F:CA"]="Google",
        // D-Link
        ["00:05:5D"]="D-Link", ["00:0D:88"]="D-Link", ["00:0F:3D"]="D-Link",
        ["00:11:95"]="D-Link", ["00:13:46"]="D-Link", ["00:15:E9"]="D-Link",
        ["00:17:9A"]="D-Link", ["00:19:5B"]="D-Link", ["00:1B:11"]="D-Link",
        ["00:1C:F0"]="D-Link", ["00:1E:58"]="D-Link", ["00:21:91"]="D-Link",
        ["1C:7E:E5"]="D-Link", ["28:10:7B"]="D-Link", ["34:08:04"]="D-Link",
        ["78:54:2E"]="D-Link", ["90:94:E4"]="D-Link", ["C8:BE:19"]="D-Link",
        ["F0:7D:68"]="D-Link",
        // Sony
        ["00:01:4A"]="Sony", ["00:04:1F"]="Sony", ["00:13:A9"]="Sony",
        ["00:1A:80"]="Sony", ["00:1D:BA"]="Sony", ["00:24:BE"]="Sony",
        ["04:A7:B7"]="Sony", ["1C:AB:A7"]="Sony", ["40:B8:37"]="Sony",
        ["70:35:09"]="Sony", ["AC:9B:0A"]="Sony", ["E0:62:20"]="Sony",
        // Realtek
        ["00:E0:4C"]="Realtek",
        // Linksys
        ["00:06:25"]="Linksys", ["00:0C:41"]="Linksys", ["00:0F:66"]="Linksys",
        ["00:12:17"]="Linksys", ["00:13:10"]="Linksys", ["00:14:BF"]="Linksys",
        ["00:16:B6"]="Linksys", ["00:18:39"]="Linksys", ["00:1A:70"]="Linksys",
        ["00:1C:10"]="Linksys", ["00:1E:E5"]="Linksys", ["00:21:29"]="Linksys",
        // Broadcom
        ["00:10:18"]="Broadcom", ["00:90:4B"]="Broadcom",
    };

    /// <summary>MAC 주소에서 제조사를 조회합니다.</summary>
    public static string Lookup(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length < 8) return "알 수 없음";
        var oui = mac[..8].ToUpperInvariant().Replace('-', ':');
        return _db.TryGetValue(oui, out var vendor) ? vendor : "알 수 없음";
    }

    /// <summary>제조사 이름으로 기기 타입 이모지를 추론합니다.</summary>
    public static string GetDeviceIcon(string vendor)
    {
        var v = vendor.ToLowerInvariant();
        if (v.Contains("apple"))   return "📱";
        if (v.Contains("samsung")) return "📱";
        if (v.Contains("xiaomi"))  return "📱";
        if (v.Contains("lg"))      return "📱";
        if (v.Contains("sony"))    return "📱";
        if (v.Contains("huawei"))  return "📱";
        if (v.Contains("tp-link")) return "🌐";
        if (v.Contains("cisco"))   return "🌐";
        if (v.Contains("netgear")) return "🌐";
        if (v.Contains("d-link"))  return "🌐";
        if (v.Contains("asus"))    return "🌐";
        if (v.Contains("linksys")) return "🌐";
        if (v.Contains("buffalo")) return "🌐";
        if (v.Contains("raspberry")) return "🍓";
        if (v.Contains("vmware") || v.Contains("virtualbox") || v.Contains("qemu")) return "🖥";
        if (v.Contains("amazon")) return "🔊";
        if (v.Contains("google")) return "🔵";
        return "💻";
    }
}
