using System.Text.Json;

namespace CharArt.Models;

public class AppSettings
{
    public string       CharSetName   { get; set; } = "ASCII 기본";
    public string       FontFamily    { get; set; } = "Consolas";
    public int          FontSizeIndex { get; set; } = 2;
    public int          Columns       { get; set; } = 80;
    public bool         Invert        { get; set; } = false;
    public List<string> RecentFiles   { get; set; } = [];

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CharArt", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 5) RecentFiles.RemoveRange(5, RecentFiles.Count - 5);
    }
}
