using KeyMacro.Models;

namespace KeyMacro.Services;

/// <summary>매크로 규칙을 사용자 로컬 폴더의 JSON 파일로 보관</summary>
public static class MacroStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Key.Macro", "macros.json");

    public static ObservableCollection<MacroRule> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new ObservableCollection<MacroRule>();
            var json = File.ReadAllText(FilePath);
            var rules = JsonSerializer.Deserialize<List<MacroRule>>(json, Options);
            return new ObservableCollection<MacroRule>(rules ?? new List<MacroRule>());
        }
        catch
        {
            return new ObservableCollection<MacroRule>();
        }
    }

    public static void Save(IEnumerable<MacroRule> rules)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(rules.ToList(), Options));
    }

    /// <summary>다른 PC로 옮기거나 백업할 때 쓰는 내보내기</summary>
    public static void Export(string path, IEnumerable<MacroRule> rules)
        => File.WriteAllText(path, JsonSerializer.Serialize(rules.ToList(), Options));

    public static List<MacroRule> Import(string path)
        => JsonSerializer.Deserialize<List<MacroRule>>(File.ReadAllText(path), Options) ?? new();
}
