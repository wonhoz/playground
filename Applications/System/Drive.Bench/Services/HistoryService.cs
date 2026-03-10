namespace DriveBench.Services;

public class HistoryService
{
    private readonly string _dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DriveBench");
    private readonly string _file;

    private static readonly JsonSerializerOptions _jOpts = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public HistoryService()
    {
        _file = Path.Combine(_dir, "history.json");
        Directory.CreateDirectory(_dir);
    }

    public List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_file)) return [];
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json, _jOpts) ?? [];
        }
        catch { return []; }
    }

    public void Save(HistoryEntry entry)
    {
        var list = Load();
        list.Add(entry);
        // 드라이브당 최근 50개만 보관
        list = list
            .GroupBy(e => e.DriveLetter)
            .SelectMany(g => g.OrderByDescending(e => e.Timestamp).Take(50))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
        File.WriteAllText(_file, JsonSerializer.Serialize(list, _jOpts));
    }

    public List<HistoryEntry> GetByDrive(string driveLetter)
        => Load().Where(e => e.DriveLetter == driveLetter)
                 .OrderBy(e => e.Timestamp)
                 .ToList();
}
