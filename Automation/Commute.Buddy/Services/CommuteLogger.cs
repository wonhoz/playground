using CommuteBuddy.Models;

namespace CommuteBuddy.Services;

public class CommuteLogger
{
    private readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CommuteBuddy", "logs");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public void Log(string locationName, string direction)
    {
        Directory.CreateDirectory(_logDir);
        var now  = DateTime.Now;
        var path = MonthPath(now.Year, now.Month);
        var log  = LoadFile(path) ?? new MonthlyLog { Year = now.Year, Month = now.Month };

        log.Entries.Add(new CommuteEntry
        {
            Timestamp    = now,
            LocationName = locationName,
            Direction    = direction,
        });

        File.WriteAllText(path, JsonSerializer.Serialize(log, JsonOpts));
    }

    public MonthlyLog? GetMonth(int year, int month)
    {
        var path = MonthPath(year, month);
        return LoadFile(path);
    }

    public MonthlyLog? GetCurrentMonth() => GetMonth(DateTime.Now.Year, DateTime.Now.Month);

    // 과거 n개월 중 데이터가 있는 것들 반환
    public List<(int Year, int Month)> GetAvailableMonths()
    {
        var result = new List<(int, int)>();
        if (!Directory.Exists(_logDir)) return result;

        foreach (var f in Directory.GetFiles(_logDir, "????-??.json"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            if (name.Length == 7 &&
                int.TryParse(name[..4], out var y) &&
                int.TryParse(name[5..], out var m))
                result.Add((y, m));
        }
        return result.OrderByDescending(x => x.Item1 * 100 + x.Item2).ToList();
    }

    private MonthlyLog? LoadFile(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<MonthlyLog>(File.ReadAllText(path)); }
        catch { return null; }
    }

    private string MonthPath(int year, int month) =>
        Path.Combine(_logDir, $"{year:0000}-{month:00}.json");
}
