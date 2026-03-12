namespace DriveBench.Services;

public class DriveDetectService
{
    public List<DriveItem> GetDrives()
    {
        var result = new List<DriveItem>();
        foreach (var d in System.IO.DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            if (d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;

            var letter = d.RootDirectory.FullName.TrimEnd('\\');
            result.Add(new DriveItem
            {
                Letter     = letter,
                Label      = d.VolumeLabel.Length > 0 ? d.VolumeLabel : "로컬 디스크",
                MediaType  = DetectMediaType(letter[0]),
                TotalBytes = d.TotalSize
            });
        }
        return result;
    }

    private string DetectMediaType(char driveLetter)
    {
        try
        {
            // 논리 디스크 → 파티션 → 물리 디스크 경로로 탐색
            var assoc = new ManagementObjectSearcher("root\\cimv2",
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} " +
                "WHERE AssocClass=Win32_LogicalDiskToPartition ResultClass=Win32_DiskPartition");

            foreach (ManagementObject part in assoc.Get())
            {
                var diskIndex = (uint)part["DiskIndex"];

                // MSFT_PhysicalDisk: MediaType 3=HDD, 4=SSD, 5=SCM
                try
                {
                    using var ps = new ManagementObjectSearcher(
                        "root\\microsoft\\windows\\storage",
                        $"SELECT MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskIndex}'");
                    foreach (ManagementObject pd in ps.Get())
                    {
                        var mt  = Convert.ToInt32(pd["MediaType"]);
                        var bus = Convert.ToInt32(pd["BusType"]);
                        if (mt == 4 && bus == 17) return "NVMe";
                        if (mt == 4) return "SSD";
                        if (mt == 3) return "HDD";
                    }
                }
                catch { }

                // 대안: Win32_DiskDrive.MediaType 문자열 확인
                try
                {
                    using var dd = new ManagementObjectSearcher("root\\cimv2",
                        $"SELECT MediaType FROM Win32_DiskDrive WHERE Index={diskIndex}");
                    foreach (ManagementObject d in dd.Get())
                    {
                        var mt = d["MediaType"]?.ToString() ?? "";
                        if (mt.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return "SSD";
                        if (mt.Contains("Solid", StringComparison.OrdinalIgnoreCase)) return "SSD";
                        if (mt.Contains("Fixed", StringComparison.OrdinalIgnoreCase)) return "HDD";
                    }
                }
                catch { }
            }
        }
        catch { }
        return "Drive";
    }
}
