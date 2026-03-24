using System.IO;
using System.Text.Json;

namespace ImgCast.Services;

public class AppSettings
{
    public string OutputFormat { get; set; } = "ICO";
    public string InputFilter  { get; set; } = "All";
    public bool   Overwrite    { get; set; } = true;
    public int    JpgQuality   { get; set; } = 95;
    public int[]  IcoSizes     { get; set; } = [16, 32, 48, 64, 128, 256];

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ImgCast", "settings.json");

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }
}
