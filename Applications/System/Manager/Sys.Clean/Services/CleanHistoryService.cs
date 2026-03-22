using System.Text.Json;

namespace SysClean.Services;

public record CleanHistoryEntry(DateTime Time, int ItemCount, long CleanedBytes);

public class CleanHistoryService
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SysClean", "history.json");

    public List<CleanHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<CleanHistoryEntry>>(json) ?? [];
        }
        catch { return []; }
    }

    public void Append(CleanHistoryEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var entries = Load();
            entries.Insert(0, entry);
            if (entries.Count > 100) entries = entries.Take(100).ToList();
            File.WriteAllText(_filePath, JsonSerializer.Serialize(entries));
        }
        catch { }
    }

    public CleanHistoryEntry? GetLast() => Load().FirstOrDefault();

    public static string FormatRelativeTime(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "방금 전";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}시간 전";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}일 전";
        return time.ToString("yyyy-MM-dd");
    }
}
