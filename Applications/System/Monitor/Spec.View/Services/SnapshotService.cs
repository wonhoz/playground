namespace SpecView.Services;

public class SnapshotService
{
    private static readonly string _dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpecView");
    private static readonly string _path = Path.Combine(_dir, "last_snapshot.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Save(HardwareData data)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(data, _opts);
            File.WriteAllText(_path, json, Encoding.UTF8);
        }
        catch { }
    }

    public HardwareData? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var json = File.ReadAllText(_path, Encoding.UTF8);
            return JsonSerializer.Deserialize<HardwareData>(json);
        }
        catch { return null; }
    }

    public List<string> GetChanges(HardwareData current, HardwareData previous)
    {
        var changes = new List<string>();
        var prev    = previous;

        // CPU 변경
        if (current.Cpu?.Name != prev.Cpu?.Name && prev.Cpu is not null)
            changes.Add($"CPU 변경: {prev.Cpu.Name} → {current.Cpu?.Name}");

        // 메모리 용량 변경
        if (current.Memory.TotalBytes != prev.Memory.TotalBytes)
        {
            var diff = (long)current.Memory.TotalBytes - (long)prev.Memory.TotalBytes;
            var sign = diff > 0 ? "+" : "";
            changes.Add($"메모리 변경: {prev.Memory.TotalDisplay} → {current.Memory.TotalDisplay} ({sign}{diff / (1024 * 1024 * 1024L)} GB)");
        }

        // 슬롯 사용 수 변경
        if (current.Memory.UsedSlots != prev.Memory.UsedSlots)
            changes.Add($"메모리 슬롯: {prev.Memory.UsedSlots}개 사용 → {current.Memory.UsedSlots}개 사용");

        // GPU 수 변경
        if (current.Gpus.Count != prev.Gpus.Count)
            changes.Add($"GPU 수 변경: {prev.Gpus.Count}개 → {current.Gpus.Count}개");

        // 드라이브 수 변경
        if (current.Drives.Count != prev.Drives.Count)
            changes.Add($"저장장치 수 변경: {prev.Drives.Count}개 → {current.Drives.Count}개");

        // 마더보드 변경
        if (current.Board.Product != prev.Board.Product && !string.IsNullOrEmpty(prev.Board.Product))
            changes.Add($"마더보드 변경: {prev.Board.Product} → {current.Board.Product}");

        // BIOS 버전 변경
        if (current.Board.BiosVersion != prev.Board.BiosVersion && !string.IsNullOrEmpty(prev.Board.BiosVersion))
            changes.Add($"BIOS 업데이트: {prev.Board.BiosVersion} → {current.Board.BiosVersion}");

        return changes;
    }

    public DateTime? LastScanTime()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return File.GetLastWriteTime(_path);
        }
        catch { return null; }
    }
}
