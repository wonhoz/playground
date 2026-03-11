using System.IO;
using System.Text.Json;

namespace RegVault.Models;

public class Bookmark
{
    public string Label    { get; set; } = "";
    public string FullPath { get; set; } = "";
}

public static class BookmarkStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RegVault", "bookmarks.json");

    public static List<Bookmark> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<Bookmark>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void Save(IEnumerable<Bookmark> bookmarks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(bookmarks.ToList(),
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
