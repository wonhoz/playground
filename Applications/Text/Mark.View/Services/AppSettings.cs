using System.IO;
using System.Text.Json;

namespace MarkView.Services;

public class AppSettings
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarkView", "settings.json");

    public string Theme { get; set; } = "dark";
    public List<string> RecentFiles { get; set; } = [];
    public List<string> OpenFiles { get; set; } = [];
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double EditorFontSize { get; set; } = 13;
    public bool IsEditMode { get; set; } = false;
    public bool IsTocVisible { get; set; } = false;
    public double TocWidth { get; set; } = 220;
    public double EditorSplitRatio { get; set; } = 0.5;

    private const int MaxRecentFiles = 10;

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
