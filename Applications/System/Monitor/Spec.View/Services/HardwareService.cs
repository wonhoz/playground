namespace SpecView.Services;

public class HardwareService
{
    public async Task<HardwareData> ScanAsync()
        => await Task.Run(Scan);

    private HardwareData Scan()
    {
        var data = new HardwareData();
        GetOsInfo(data);
        data.Cpu      = GetCpu();
        data.Memory   = GetMemory();
        data.Gpus     = GetGpus();
        data.Board    = GetMotherboard();
        data.Drives   = GetDrives();
        data.Networks = GetNetworks();
        return data;
    }

    // ── OS / 컴퓨터 정보 ─────────────────────────────────────────────

    private static void GetOsInfo(HardwareData data)
    {
        try
        {
            using var os = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in os.Get())
            {
                data.OsCaption = Get(obj, "Caption");
                data.OsVersion = Get(obj, "Version");
                break;
            }
        }
        catch { }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in cs.Get())
            {
                data.ComputerName = Get(obj, "DNSHostName");
                if (string.IsNullOrEmpty(data.ComputerName))
                    data.ComputerName = Get(obj, "Name");
                break;
            }
        }
        catch { }
    }

    // ── CPU ──────────────────────────────────────────────────────────

    private static CpuInfo? GetCpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var maxClock = 0.0;
                if (double.TryParse(Get(obj, "MaxClockSpeed"), out var mc)) maxClock = mc;

                int cores = 0, threads = 0;
                if (int.TryParse(Get(obj, "NumberOfCores"),          out var c)) cores   = c;
                if (int.TryParse(Get(obj, "NumberOfLogicalProcessors"), out var t)) threads = t;

                return new CpuInfo
                {
                    Name         = Get(obj, "Name"),
                    Manufacturer = Get(obj, "Manufacturer"),
                    Socket       = Get(obj, "SocketDesignation"),
                    Cores        = cores,
                    Threads      = threads,
                    MaxClockMHz  = maxClock,
                    L2Cache      = FormatCache(Get(obj, "L2CacheSize")),
                    L3Cache      = FormatCache(Get(obj, "L3CacheSize")),
                    Architecture = GetArchitectureName(Get(obj, "Architecture")),
                    Description  = Get(obj, "Description")
                };
            }
        }
        catch { }
        return null;
    }

    private static string FormatCache(string kbStr)
    {
        if (!uint.TryParse(kbStr, out var kb) || kb == 0) return "";
        return kb >= 1024 ? $"{kb / 1024} MB" : $"{kb} KB";
    }

    private static string GetArchitectureName(string code) => code switch
    {
        "0"  => "x86",
        "9"  => "x64",
        "12" => "ARM64",
        _    => code
    };

    // ── 메모리 ───────────────────────────────────────────────────────

    private static MemoryInfo GetMemory()
    {
        var info  = new MemoryInfo();
        var slots = new List<MemorySlot>();

        try
        {
            using var arr = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemoryArray");
            foreach (ManagementObject obj in arr.Get())
            {
                if (int.TryParse(Get(obj, "MemoryDevices"), out var n))
                    info.TotalSlots = n;
                break;
            }
        }
        catch { }

        try
        {
            using var mem = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in mem.Get())
            {
                ulong cap = 0;
                if (ulong.TryParse(Get(obj, "Capacity"), out var capVal)) cap = capVal;
                uint  spd = 0;
                if (uint.TryParse(Get(obj, "Speed"), out var spdVal)) spd = spdVal;

                var slot = new MemorySlot
                {
                    BankLabel     = Get(obj, "BankLabel"),
                    DeviceLocator = Get(obj, "DeviceLocator"),
                    CapacityBytes = cap,
                    SpeedMHz      = spd,
                    MemoryType    = GetMemoryTypeName(Get(obj, "SMBIOSMemoryType")),
                    Manufacturer  = Get(obj, "Manufacturer"),
                    PartNumber    = Get(obj, "PartNumber")?.Trim() ?? ""
                };
                slots.Add(slot);
                info.TotalBytes += cap;
                if (cap > 0) info.UsedSlots++;
                if (spd > info.MaxSpeedMHz) info.MaxSpeedMHz = spd;
            }
        }
        catch { }

        info.Slots = slots;
        if (info.TotalSlots == 0) info.TotalSlots = slots.Count;
        return info;
    }

    private static string GetMemoryTypeName(string code) => code switch
    {
        "20" => "DDR",
        "21" => "DDR2",
        "24" => "DDR3",
        "26" => "DDR4",
        "34" => "LPDDR4",
        "36" => "LPDDR5",
        "30" => "LPDDR3",
        "35" => "DDR5",
        _    => "DRAM"
    };

    // ── GPU ──────────────────────────────────────────────────────────

    private static List<GpuInfo> GetGpus()
    {
        var list = new List<GpuInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI%'");
            foreach (ManagementObject obj in searcher.Get())
            {
                ulong ram = 0;
                if (ulong.TryParse(Get(obj, "AdapterRAM"), out var r)) ram = r;
                uint refresh = 0;
                if (uint.TryParse(Get(obj, "CurrentRefreshRate"), out var rf)) refresh = rf;

                list.Add(new GpuInfo
                {
                    Name                 = Get(obj, "Name"),
                    AdapterRamBytes      = ram,
                    DriverVersion        = Get(obj, "DriverVersion"),
                    DriverDate           = FormatWmiDate(Get(obj, "DriverDate")),
                    CurrentRefreshRate   = refresh,
                    VideoModeDescription = Get(obj, "VideoModeDescription"),
                    AdapterCompatibility = Get(obj, "AdapterCompatibility")
                });
            }
        }
        catch { }
        return list;
    }

    // ── 마더보드 ─────────────────────────────────────────────────────

    private static MotherboardInfo GetMotherboard()
    {
        var info = new MotherboardInfo();
        try
        {
            using var bb = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in bb.Get())
            {
                info.Manufacturer = Get(obj, "Manufacturer");
                info.Product      = Get(obj, "Product");
                info.SerialNumber = Get(obj, "SerialNumber");
                break;
            }
        }
        catch { }

        try
        {
            using var bios = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (ManagementObject obj in bios.Get())
            {
                info.BiosVersion = Get(obj, "SMBIOSBIOSVersion");
                info.BiosDate    = FormatWmiDate(Get(obj, "ReleaseDate"));
                info.BiosMaker   = Get(obj, "Manufacturer");
                break;
            }
        }
        catch { }

        return info;
    }

    // ── 저장장치 ─────────────────────────────────────────────────────

    private static List<StorageItem> GetDrives()
    {
        var list = new List<StorageItem>();
        var smartMap = GetSmartStatus();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                ulong size = 0;
                if (ulong.TryParse(Get(obj, "Size"), out var s)) size = s;

                var model    = Get(obj, "Model");
                var hasSmart = smartMap.TryGetValue(model, out var smart);

                list.Add(new StorageItem
                {
                    Model         = model,
                    InterfaceType = Get(obj, "InterfaceType"),
                    SizeBytes     = size,
                    Status        = Get(obj, "Status"),
                    SmartStatus   = hasSmart ? smart.status : "알 수 없음",
                    SmartOk       = hasSmart ? smart.ok     : true,
                    MediaType     = Get(obj, "MediaType")
                });
            }
        }
        catch { }

        return list;
    }

    private static Dictionary<string, (string status, bool ok)> GetSmartStatus()
    {
        var map = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictStatus");
            foreach (ManagementObject obj in searcher.Get())
            {
                var instanceName = Get(obj, "InstanceName");
                var predict      = Get(obj, "PredictFailure").ToLowerInvariant() == "true";
                var reason       = Get(obj, "Reason");
                var status       = predict ? $"⚠ 오류 예측 (코드: {reason})" : "정상";
                // InstanceName에서 모델명 추출 어려우므로 전체 저장
                map[instanceName] = (status, !predict);
            }
        }
        catch { }
        return map;
    }

    // ── 네트워크 ─────────────────────────────────────────────────────

    private static List<NetworkItem> GetNetworks()
    {
        var list = new List<NetworkItem>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
            foreach (ManagementObject obj in searcher.Get())
            {
                var mac = Get(obj, "MACAddress");
                if (string.IsNullOrEmpty(mac)) continue;

                uint statusCode = 0;
                uint.TryParse(Get(obj, "NetConnectionStatus"), out statusCode);
                var isConnected = statusCode == 2;

                var speedStr = Get(obj, "Speed");
                var speedDisplay = "";
                if (ulong.TryParse(speedStr, out var speedBps))
                    speedDisplay = speedBps >= 1_000_000_000
                        ? $"{speedBps / 1_000_000_000.0:F0} Gbps"
                        : $"{speedBps / 1_000_000.0:F0} Mbps";

                list.Add(new NetworkItem
                {
                    Name             = Get(obj, "Name"),
                    Description      = Get(obj, "Description"),
                    MACAddress       = mac,
                    ConnectionStatus = GetNetStatusName(statusCode),
                    Speed            = speedDisplay,
                    IsConnected      = isConnected,
                    AdapterType      = Get(obj, "AdapterType")
                });
            }
        }
        catch { }
        return list;
    }

    private static string GetNetStatusName(uint code) => code switch
    {
        0  => "연결 끊김",
        1  => "연결 중",
        2  => "연결됨",
        3  => "연결 해제 중",
        4  => "하드웨어 없음",
        5  => "하드웨어 비활성",
        6  => "하드웨어 오작동",
        7  => "미디어 연결 끊김",
        8  => "인증 중",
        9  => "인증 성공",
        10 => "인증 실패",
        11 => "잘못된 주소",
        12 => "자격 증명 필요",
        _  => "알 수 없음"
    };

    // ── 공통 헬퍼 ────────────────────────────────────────────────────

    private static string Get(ManagementObject obj, string prop)
    {
        try { return obj[prop]?.ToString()?.Trim() ?? ""; }
        catch { return ""; }
    }

    private static string FormatWmiDate(string wmi)
    {
        // WMI 날짜: "20240115000000.000000+000"
        if (wmi.Length < 8) return wmi;
        try
        {
            var y = wmi[..4];
            var m = wmi[4..6];
            var d = wmi[6..8];
            return $"{y}-{m}-{d}";
        }
        catch { return wmi; }
    }
}
