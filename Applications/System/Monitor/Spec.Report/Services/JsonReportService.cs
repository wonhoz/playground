namespace SpecReport.Services;

public class JsonReportService
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented    = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public void Save(SystemReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, _opts);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public SystemReport? Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<SystemReport>(json, _opts);
        }
        catch { return null; }
    }

    public CompareResult Compare(SystemReport oldR, SystemReport newR)
    {
        var result = new CompareResult { Old = oldR, New = newR };

        // ─ CPU
        CompareField(result, "CPU", "프로세서",   oldR.Cpu.Name,         newR.Cpu.Name);
        CompareField(result, "CPU", "물리 코어",  $"{oldR.Cpu.PhysicalCores}", $"{newR.Cpu.PhysicalCores}");
        CompareField(result, "CPU", "최대 클럭",  $"{oldR.Cpu.MaxClockGHz} GHz", $"{newR.Cpu.MaxClockGHz} GHz");

        // ─ RAM
        CompareField(result, "RAM", "총 용량",
            FormatBytes(oldR.TotalRamBytes), FormatBytes(newR.TotalRamBytes));

        // ─ GPU
        var oldGpu = oldR.Gpus.FirstOrDefault();
        var newGpu = newR.Gpus.FirstOrDefault();
        if (oldGpu != null && newGpu != null)
        {
            CompareField(result, "GPU", "드라이버 버전",
                oldGpu.DriverVersion, newGpu.DriverVersion);
            CompareField(result, "GPU", "해상도",
                $"{oldGpu.CurrentWidth}×{oldGpu.CurrentHeight}",
                $"{newGpu.CurrentWidth}×{newGpu.CurrentHeight}");
        }

        // ─ OS
        CompareField(result, "OS", "버전",       oldR.Os.Caption,     newR.Os.Caption);
        CompareField(result, "OS", "빌드 번호",  oldR.Os.BuildNumber, newR.Os.BuildNumber);
        CompareField(result, "OS", "마지막 업데이트", oldR.Os.WindowsUpdateDate, newR.Os.WindowsUpdateDate);

        // ─ 스토리지 (드라이브별 여유 공간)
        foreach (var nd in newR.Drives)
        {
            var od = oldR.Drives.FirstOrDefault(d => d.DriveLetter == nd.DriveLetter);
            if (od == null) continue;
            CompareField(result, $"스토리지 {nd.DriveLetter}", "여유 공간",
                FormatBytes(od.FreeBytes), FormatBytes(nd.FreeBytes));
        }

        // ─ 보안
        CompareField(result, "보안", "Defender",
            oldR.Security.DefenderEnabled ? "활성" : "비활성",
            newR.Security.DefenderEnabled ? "활성" : "비활성");
        CompareField(result, "보안", "방화벽",
            oldR.Security.FirewallEnabled ? "활성" : "비활성",
            newR.Security.FirewallEnabled ? "활성" : "비활성");
        CompareField(result, "보안", "BitLocker",
            oldR.Security.BitLockerStatus, newR.Security.BitLockerStatus);

        // ─ 소프트웨어 비교
        var oldDict = oldR.Software.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        var newDict = newR.Software.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in newDict)
        {
            if (!oldDict.TryGetValue(kv.Key, out var oldApp))
                result.AddedSoftware.Add(kv.Value);
            else if (!string.Equals(oldApp.Version, kv.Value.Version))
                result.UpdatedSoftware.Add(new SoftwareUpdate(kv.Key, oldApp.Version, kv.Value.Version));
        }
        foreach (var kv in oldDict)
        {
            if (!newDict.ContainsKey(kv.Key))
                result.RemovedSoftware.Add(kv.Value);
        }

        result.AddedSoftware  = [.. result.AddedSoftware.OrderBy(a => a.Name)];
        result.RemovedSoftware= [.. result.RemovedSoftware.OrderBy(a => a.Name)];
        result.UpdatedSoftware= [.. result.UpdatedSoftware.OrderBy(u => u.Name)];

        return result;
    }

    private static void CompareField(CompareResult r, string section, string field,
                                     string? oldVal, string? newVal)
    {
        if (!string.Equals(oldVal, newVal))
            r.Changes.Add(new FieldChange(section, field, oldVal ?? "", newVal ?? ""));
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_099_511_627_776L => $"{bytes / 1_099_511_627_776.0:F1} TB",
        >= 1_073_741_824L     => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576L         => $"{bytes / 1_048_576.0:F1} MB",
        _                     => $"{bytes} B"
    };
}
