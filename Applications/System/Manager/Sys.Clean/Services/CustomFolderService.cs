using System.Text.Json;

namespace SysClean.Services;

public static class CustomFolderService
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SysClean", "custom_folders.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void Save(List<string> folders)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(folders));
        }
        catch { }
    }

    public static void Add(string folder)
    {
        var list = Load();
        if (!list.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(folder);
            Save(list);
        }
    }

    public static void Remove(string folder)
    {
        var list = Load();
        list.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        Save(list);
    }
}
