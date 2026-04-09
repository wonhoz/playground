using System.Text.Json;

namespace Brush.Scale.Services;

public class AppSettings
{
    public string SelectedModel   { get; set; } = "Bicubic";
    public int    ScaleFactor     { get; set; } = 4;
    public string OutputFormat    { get; set; } = "Png";
    public int    JpegQuality     { get; set; } = 95;
    public string BatchInputDir       { get; set; } = "";
    public string BatchOutputDir      { get; set; } = "";
    public string OutputPattern       { get; set; } = "{name}_{scale}x";
    public bool   BatchRecursive      { get; set; } = false;
    public bool   OpenOutputOnComplete { get; set; } = false;
}

public static class SettingsService
{
    static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Brush.Scale", "settings.json");

    static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
        }
        catch { }
    }
}
