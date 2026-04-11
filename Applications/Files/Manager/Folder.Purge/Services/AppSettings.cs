using System.Text.Json;

namespace FolderPurge.Services;

public class ScanHistoryEntry
{
    public DateTime Time { get; set; }
    public int FoundCount { get; set; }
    public long FreedBytes { get; set; }   // 삭제로 확보한 용량 (스캔만 한 경우 0)
    public int DeletedCount { get; set; }  // 실제 삭제된 항목 수 (스캔만 한 경우 0)
    public List<string> Roots { get; set; } = [];

    public string FreedBytesText => Helpers.SizeFormatter.Format(FreedBytes);
}

public class AppSettings
{
    public List<string> TargetFolders { get; set; } = [];
    public bool ScanEmptyFolders { get; set; } = true;
    public bool ScanVsArtifacts { get; set; } = true;
    public bool ScanEmptyFiles { get; set; } = false;
    public bool UseRecycleBin { get; set; } = true;
    public bool PreviewOnly { get; set; } = false;
    public bool ExcludeRecentFolders { get; set; } = false;
    public int MinAgeDays { get; set; } = 7;
    public List<string> ExcludedFolders { get; set; } =
        [".git", ".vs", ".svn", ".hg", "node_modules", "__pycache__"];
    public List<string> VsArtifactFolderNames { get; set; } = ["bin", "obj"];
    public List<string> VsArtifactFileExtensions { get; set; } = [".user"];
    public string SortColumn { get; set; } = "크기";
    public bool SortDescending { get; set; } = true;
    public DateTime? LastScanTime { get; set; } = null;
    public List<ScanHistoryEntry> ScanHistory { get; set; } = [];

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FolderPurge", "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch { /* 손상된 설정 무시, 기본값 사용 */ }
        return new();
    }

    /// <summary>저장 결과 반환 — 실패 시 사용자에게 알릴 수 있도록 변경</summary>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, _opts));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void AddHistory(ScanHistoryEntry entry, int max = 5)
    {
        ScanHistory.Insert(0, entry);
        if (ScanHistory.Count > max)
            ScanHistory.RemoveRange(max, ScanHistory.Count - max);
    }
}
