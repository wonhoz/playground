using Microsoft.Win32;

namespace SpecReport.Services;

public class SystemInfoCollector
{
    public SystemReport Collect()
    {
        var report = new SystemReport
        {
            ComputerName = Environment.MachineName,
            UserName     = Environment.UserName,
            CollectedAt  = DateTime.Now
        };

        report.Cpu                          = CollectCpu();
        (report.RamSlots, report.TotalRamBytes) = CollectRam();
        report.Gpus                         = CollectGpus();
        report.Drives                       = CollectDrives();
        report.Os                           = CollectOs();
        report.NetworkAdapters              = CollectNetwork();
        report.Software                     = CollectSoftware();
        report.Security                     = CollectSecurity();

        return report;
    }

    // ── CPU ──────────────────────────────────────────────
    private CpuInfo CollectCpu()
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,Architecture,SocketDesignation,Manufacturer FROM Win32_Processor");
            foreach (ManagementObject mo in s.Get())
            {
                return new CpuInfo
                {
                    Name          = mo["Name"]?.ToString()?.Trim() ?? "",
                    Manufacturer  = mo["Manufacturer"]?.ToString()?.Trim() ?? "",
                    PhysicalCores = Convert.ToInt32(mo["NumberOfCores"] ?? 0),
                    LogicalCores  = Convert.ToInt32(mo["NumberOfLogicalProcessors"] ?? 0),
                    MaxClockGHz   = Math.Round(Convert.ToDouble(mo["MaxClockSpeed"] ?? 0) / 1000.0, 2),
                    Architecture  = ArchName(Convert.ToInt32(mo["Architecture"] ?? 0)),
                    Socket        = mo["SocketDesignation"]?.ToString() ?? ""
                };
            }
        }
        catch { }
        return new CpuInfo { Name = "수집 실패" };
    }

    private static string ArchName(int arch) => arch switch
    {
        0  => "x86",
        5  => "ARM",
        9  => "x64",
        12 => "ARM64",
        _  => $"Unknown"
    };

    // ── RAM ──────────────────────────────────────────────
    private (List<RamSlotInfo> slots, long total) CollectRam()
    {
        var slots = new List<RamSlotInfo>();
        long total = 0;
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Capacity,Speed,Manufacturer,SMBIOSMemoryType,DeviceLocator FROM Win32_PhysicalMemory");
            foreach (ManagementObject mo in s.Get())
            {
                long cap = Convert.ToInt64(mo["Capacity"] ?? 0L);
                total += cap;
                slots.Add(new RamSlotInfo
                {
                    Slot         = mo["DeviceLocator"]?.ToString() ?? "",
                    CapacityBytes= cap,
                    SpeedMHz     = Convert.ToInt32(mo["Speed"] ?? 0),
                    Manufacturer = (mo["Manufacturer"]?.ToString() ?? "").Trim(),
                    MemoryType   = RamTypeName(Convert.ToInt32(mo["SMBIOSMemoryType"] ?? 0))
                });
            }
        }
        catch { }
        return (slots, total);
    }

    private static string RamTypeName(int t) => t switch
    {
        26 => "DDR4",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        _  => "DDR"
    };

    // ── GPU ──────────────────────────────────────────────
    private List<GpuInfo> CollectGpus()
    {
        var list = new List<GpuInfo>();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name,AdapterRAM,DriverVersion,DriverDate,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject mo in s.Get())
            {
                var name = mo["Name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                // DriverDate: WMI CIM_DATETIME 형식 → 날짜 파싱
                var driverDateRaw = mo["DriverDate"]?.ToString() ?? "";
                var driverDate    = ParseWmiDate(driverDateRaw);

                list.Add(new GpuInfo
                {
                    Name          = name.Trim(),
                    VramBytes     = Convert.ToInt64(mo["AdapterRAM"] ?? 0L),
                    DriverVersion = mo["DriverVersion"]?.ToString() ?? "",
                    DriverDate    = driverDate,
                    CurrentWidth  = Convert.ToInt32(mo["CurrentHorizontalResolution"] ?? 0),
                    CurrentHeight = Convert.ToInt32(mo["CurrentVerticalResolution"] ?? 0),
                    RefreshRate   = Convert.ToInt32(mo["CurrentRefreshRate"] ?? 0)
                });
            }
        }
        catch { }
        return list;
    }

    // ── 스토리지 ──────────────────────────────────────────
    private List<StorageDriveInfo> CollectDrives()
    {
        var list = new List<StorageDriveInfo>();

        // 물리 디스크 타입 맵 (모델명 → 타입)
        var diskModelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT FriendlyName,MediaType,BusType FROM MSFT_PhysicalDisk"));
            foreach (ManagementObject mo in s.Get())
            {
                var friendlyName = mo["FriendlyName"]?.ToString() ?? "";
                var mediaType    = Convert.ToInt32(mo["MediaType"] ?? 0);
                var busType      = Convert.ToInt32(mo["BusType"]   ?? 0);
                var typeName = (mediaType, busType) switch
                {
                    (4, 17) => "NVMe SSD",
                    (4, _)  => "SSD",
                    (3, _)  => "HDD",
                    _       => GuessTypeFromName(friendlyName)
                };
                if (!string.IsNullOrEmpty(friendlyName))
                    diskModelMap[friendlyName] = typeName;
            }
        }
        catch { }

        // Win32_LogicalDisk (로컬 드라이브)
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT DeviceID,VolumeName,FileSystem,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (ManagementObject mo in s.Get())
            {
                var letter = mo["DeviceID"]?.ToString() ?? "";
                var model  = GetPhysicalModelForLetter(letter);
                var media  = diskModelMap.TryGetValue(model, out var t) ? t : GuessTypeFromName(model);

                list.Add(new StorageDriveInfo
                {
                    DriveLetter = letter,
                    Label       = mo["VolumeName"]?.ToString() ?? "",
                    FileSystem  = mo["FileSystem"]?.ToString() ?? "",
                    TotalBytes  = Convert.ToInt64(mo["Size"]      ?? 0L),
                    FreeBytes   = Convert.ToInt64(mo["FreeSpace"] ?? 0L),
                    Model       = model,
                    MediaType   = media
                });
            }
        }
        catch { }

        return list;
    }

    private string GetPhysicalModelForLetter(string driveLetter)
    {
        try
        {
            // Win32_LogicalDisk → Win32_LogicalDiskToPartition → Win32_DiskDriveToDiskPartition → Win32_DiskDrive
            using var s = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} " +
                $"WHERE AssocClass=Win32_LogicalDiskToPartition");
            foreach (ManagementObject part in s.Get())
            {
                using var s2 = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} " +
                    $"WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                foreach (ManagementObject disk in s2.Get())
                    return disk["Model"]?.ToString() ?? "";
            }
        }
        catch { }
        return "";
    }

    private static string GuessTypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var n = name.ToUpperInvariant();
        if (n.Contains("NVME") || n.Contains("PCIE"))   return "NVMe SSD";
        if (n.Contains("SSD") || n.Contains("SOLID"))   return "SSD";
        if (n.Contains("HDD") || n.Contains("HARD"))    return "HDD";
        return "Unknown";
    }

    // ── OS ───────────────────────────────────────────────
    private OsInfo CollectOs()
    {
        var info = new OsInfo();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Caption,Version,BuildNumber,OSArchitecture,InstallDate,LastBootUpTime,RegisteredUser FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in s.Get())
            {
                info.Caption         = mo["Caption"]?.ToString()?.Trim() ?? "";
                info.Version         = mo["Version"]?.ToString() ?? "";
                info.BuildNumber     = mo["BuildNumber"]?.ToString() ?? "";
                info.Architecture    = mo["OSArchitecture"]?.ToString() ?? "";
                info.RegisteredOwner = mo["RegisteredUser"]?.ToString() ?? "";
                info.InstallDate     = ParseWmiDateTime(mo["InstallDate"]?.ToString());
                info.LastBoot        = ParseWmiDateTime(mo["LastBootUpTime"]?.ToString());
                break;
            }
        }
        catch { }

        info.DotNetVersion     = GetDotNetVersion();
        info.WindowsUpdateDate = GetLastWindowsUpdateDate();
        return info;
    }

    private static string GetDotNetVersion()
    {
        // .NET 5+ 감지: 실행 중 런타임 버전 사용
        var ver = System.Environment.Version;
        return $".NET {ver.Major}.{ver.Minor}.{ver.Build}";
    }

    private static string GetLastWindowsUpdateDate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
            var date = key?.GetValue("LastSuccessTime")?.ToString();
            return date ?? "알 수 없음";
        }
        catch { return "알 수 없음"; }
    }

    // ── 네트워크 ─────────────────────────────────────────
    private List<NetAdapterInfo> CollectNetwork()
    {
        var list = new List<NetAdapterInfo>();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Description,IPAddress,MACAddress,DNSServerSearchOrder,Speed FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True");
            foreach (ManagementObject mo in s.Get())
            {
                var desc = mo["Description"]?.ToString() ?? "";
                var ips  = (mo["IPAddress"] as string[])?.ToList() ?? [];
                var dns  = (mo["DNSServerSearchOrder"] as string[])?.ToList() ?? [];
                var mac  = mo["MACAddress"]?.ToString() ?? "";
                var spd  = Convert.ToUInt64(mo["Speed"] ?? 0UL);

                list.Add(new NetAdapterInfo
                {
                    Name        = desc,
                    Description = desc,
                    IpAddresses = ips,
                    MacAddress  = mac,
                    DnsServers  = dns,
                    Speed       = FormatSpeed(spd),
                    IsWireless  = desc.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                                  desc.Contains("Wi-Fi",    StringComparison.OrdinalIgnoreCase) ||
                                  desc.Contains("WLAN",     StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch { }
        return list;
    }

    private static string FormatSpeed(ulong bps) => bps switch
    {
        >= 10_000_000_000UL => $"{bps / 1_000_000_000} Gbps",
        >= 1_000_000_000UL  => $"{bps / 1_000_000_000} Gbps",
        >= 1_000_000UL      => $"{bps / 1_000_000} Mbps",
        0UL                 => "알 수 없음",
        _                   => $"{bps} bps"
    };

    // ── 설치 소프트웨어 ──────────────────────────────────
    private List<InstalledApp> CollectSoftware()
    {
        var dict = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var regPath in paths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var sk = key.OpenSubKey(sub);
                    if (sk == null) continue;

                    var name = sk.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (sk.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (sk.GetValue("ParentKeyName") is string pk && !string.IsNullOrEmpty(pk)) continue;

                    var app = new InstalledApp
                    {
                        Name        = name.Trim(),
                        Version     = sk.GetValue("DisplayVersion")?.ToString() ?? "",
                        Publisher   = sk.GetValue("Publisher")?.ToString()?.Trim() ?? "",
                        InstallDate = sk.GetValue("InstallDate")?.ToString() ?? ""
                    };
                    dict[name] = app;
                }
            }
            catch { }
        }

        // 현재 사용자 레지스트리도 스캔
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key != null)
            {
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var sk = key.OpenSubKey(sub);
                    if (sk == null) continue;
                    var name = sk.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    dict[name!] = new InstalledApp
                    {
                        Name      = name!.Trim(),
                        Version   = sk.GetValue("DisplayVersion")?.ToString() ?? "",
                        Publisher = sk.GetValue("Publisher")?.ToString()?.Trim() ?? ""
                    };
                }
            }
        }
        catch { }

        return [.. dict.Values.OrderBy(a => a.Name)];
    }

    // ── 보안 ─────────────────────────────────────────────
    private SecurityInfo CollectSecurity()
    {
        var info = new SecurityInfo();

        // Defender / 안티바이러스
        try
        {
            var scope = new ManagementScope(@"\\.\root\SecurityCenter2");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT displayName,productState FROM AntiVirusProduct"));
            foreach (ManagementObject mo in s.Get())
            {
                var pState = Convert.ToUInt32(mo["productState"] ?? 0U);
                info.DefenderEnabled = ((pState >> 12) & 0xF) == 1;
                info.DefenderProduct = mo["displayName"]?.ToString() ?? "";
                break;
            }
        }
        catch { }

        // 방화벽
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            info.FirewallEnabled = (key?.GetValue("EnableFirewall") is int fw && fw == 1);
        }
        catch { }

        // BitLocker
        try
        {
            var scope = new ManagementScope(
                @"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT DriveLetter,ProtectionStatus FROM Win32_EncryptableVolume"));

            var statuses = new List<int>();
            foreach (ManagementObject mo in s.Get())
                statuses.Add(Convert.ToInt32(mo["ProtectionStatus"] ?? 0));

            info.BitLockerStatus = statuses.Count == 0 ? "Off"
                : statuses.All(st => st == 1) ? "On"
                : statuses.Any(st => st == 1) ? "Partial"
                : "Off";
        }
        catch { info.BitLockerStatus = "Unknown"; }

        // 자동 업데이트
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update");
            var auOpt = key?.GetValue("AUOptions");
            info.AutoUpdateEnabled = auOpt is int v && (v == 3 || v == 4);
        }
        catch { }

        return info;
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    private static string ParseWmiDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 8) return "";
        try { return $"{raw[..4]}-{raw[4..6]}-{raw[6..8]}"; }
        catch { return ""; }
    }

    private static DateTime ParseWmiDateTime(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 14) return default;
        try
        {
            int y  = int.Parse(raw[..4]);
            int mo = int.Parse(raw[4..6]);
            int d  = int.Parse(raw[6..8]);
            int h  = int.Parse(raw[8..10]);
            int mi = int.Parse(raw[10..12]);
            int s  = int.Parse(raw[12..14]);
            return new DateTime(y, mo, d, h, mi, s, DateTimeKind.Local);
        }
        catch { return default; }
    }
}
