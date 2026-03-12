using System.Text.Json;

namespace VpnCast.Models;

public class ConnectionLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ProfileName { get; set; } = "";
    public TunnelType Type { get; set; }
    public string Action { get; set; } = "";   // "연결" / "해제"
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public static class ConnectionLogStore
{
    private static readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VpnCast", "connection_log.json");
    private static readonly List<ConnectionLog> _entries = [];

    static ConnectionLogStore() => Load();

    private static void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var json = File.ReadAllText(_file);
            var list = JsonSerializer.Deserialize<List<ConnectionLog>>(json) ?? [];
            _entries.AddRange(list);
        }
        catch { }
    }

    public static void Add(ConnectionLog entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > 100) _entries.RemoveAt(_entries.Count - 1);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        catch { }
    }

    public static IReadOnlyList<ConnectionLog> GetRecent(int count = 50)
        => _entries.Take(count).ToList();
}
