namespace DriveBench.Services;

public class SmartService
{
    private static readonly Dictionary<int, string> AttrNames = new()
    {
        [1]   = "Raw Read Error Rate",
        [3]   = "Spin Up Time",
        [4]   = "Start/Stop Count",
        [5]   = "Reallocated Sectors Count",
        [7]   = "Seek Error Rate",
        [9]   = "Power-On Hours",
        [10]  = "Spin Retry Count",
        [12]  = "Power Cycle Count",
        [177] = "Wear Leveling Count",
        [179] = "Used Reserved Block Count (Total)",
        [181] = "Program Fail Count (Total)",
        [182] = "Erase Fail Count (Total)",
        [183] = "Runtime Bad Block",
        [187] = "Uncorrectable Error Count",
        [190] = "Airflow Temperature",
        [192] = "Power-Off Retract Count",
        [193] = "Load/Unload Cycle Count",
        [194] = "Temperature",
        [195] = "Hardware ECC Recovered",
        [196] = "Reallocation Event Count",
        [197] = "Current Pending Sector Count",
        [198] = "Uncorrectable Sector Count",
        [199] = "UltraDMA CRC Error Count",
        [231] = "SSD Life Left",
        [233] = "Media Wearout Indicator",
        [241] = "Total LBAs Written",
        [242] = "Total LBAs Read",
    };

    public (List<SmartAttribute> Attrs, bool IsOk, string ModelName, string SerialNo) GetSmart(char driveLetter)
    {
        var attrs    = new List<SmartAttribute>();
        var model    = "";
        var serial   = "";
        var isOk     = true;

        try
        {
            // 드라이브 레터 → 물리 디스크 인덱스
            uint diskIndex = 0;
            using (var q = new ManagementObjectSearcher("root\\cimv2",
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} " +
                "WHERE AssocClass=Win32_LogicalDiskToPartition ResultClass=Win32_DiskPartition"))
            {
                foreach (ManagementObject p in q.Get())
                {
                    diskIndex = (uint)p["DiskIndex"];
                    break;
                }
            }

            // 모델 / 시리얼 조회
            using (var q = new ManagementObjectSearcher("root\\cimv2",
                $"SELECT Model, SerialNumber FROM Win32_DiskDrive WHERE Index={diskIndex}"))
            {
                foreach (ManagementObject d in q.Get())
                {
                    model  = d["Model"]?.ToString()?.Trim()  ?? "";
                    serial = d["SerialNumber"]?.ToString()?.Trim() ?? "";
                }
            }

            // S.M.A.R.T 예측 실패 상태
            using (var q = new ManagementObjectSearcher("root\\wmi",
                "SELECT * FROM MSStorageDriver_FailurePredictStatus"))
            {
                foreach (ManagementObject o in q.Get())
                {
                    var predFail = (bool)(o["PredictFailure"] ?? false);
                    if (predFail) isOk = false;
                }
            }

            // S.M.A.R.T 데이터 (raw bytes)
            using var dataQ = new ManagementObjectSearcher("root\\wmi",
                "SELECT * FROM MSStorageDriver_FailurePredictData");
            foreach (ManagementObject o in dataQ.Get())
            {
                var data = (byte[]?)o["VendorSpecific"];
                if (data == null || data.Length < 362) continue;

                for (int i = 2; i < 362; i += 12)
                {
                    int id = data[i];
                    if (id == 0) continue;

                    int flags  = data[i + 1] | (data[i + 2] << 8);
                    int cur    = data[i + 3];
                    int worst  = data[i + 4];
                    // raw: 6 bytes little-endian
                    long rawVal = 0;
                    for (int b = 0; b < 6; b++)
                        rawVal |= (long)data[i + 5 + b] << (b * 8);

                    attrs.Add(new SmartAttribute
                    {
                        Id        = id,
                        Name      = AttrNames.TryGetValue(id, out var n) ? n : $"Unknown ({id:X2}h)",
                        Current   = cur,
                        Worst     = worst,
                        Threshold = 0,  // threshold는 별도 쿼리 필요, 여기선 생략
                        Raw       = rawVal.ToString()
                    });
                }
                break; // 첫 번째 드라이브만
            }
        }
        catch { }

        return (attrs, isOk, model, serial);
    }
}
