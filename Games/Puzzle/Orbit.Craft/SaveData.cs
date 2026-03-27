using System.IO;
using System.Text.Json;

namespace OrbitCraft;

public class LevelRecord
{
    public bool Cleared    { get; set; }
    public int  BestStars  { get; set; }
    public int  BestLaunch { get; set; }
}

public class SaveData
{
    public Dictionary<int, LevelRecord> Levels { get; set; } = [];

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OrbitCraft", "save.json");

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<SaveData>(json) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public LevelRecord GetOrCreate(int level)
    {
        if (!Levels.TryGetValue(level, out var rec))
            Levels[level] = rec = new();
        return rec;
    }

    /// <summary>발사 횟수로 별점 계산: 1발=3★, 2발=2★, 3발=1★, 4발+=0★</summary>
    public static int CalcStars(int launchCount) => launchCount switch
    {
        1 => 3,
        2 => 2,
        3 => 1,
        _ => 0
    };

    public static string StarsString(int stars) => stars switch
    {
        3 => "★★★",
        2 => "★★☆",
        1 => "★☆☆",
        _ => "☆☆☆"
    };
}
