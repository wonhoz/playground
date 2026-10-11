using System.IO;
using System.Text.Json;

namespace AudioCast.Services;

public class AppSettings
{
    public string  OutputFormat  { get; set; } = "MP3";
    public string  QualityKey    { get; set; } = "320";
    public string  InputFilter   { get; set; } = "All";
    public bool    SmartQuality  { get; set; } = true;
    public bool    KeepTags      { get; set; } = true;
    public bool    Overwrite     { get; set; } = false;
    public bool    UseSourceDir  { get; set; } = false;
    public string  OutputDir     { get; set; } = DefaultOutputDir;
    public bool    KeepStructure { get; set; } = true;
    public int     Parallelism   { get; set; } = Environment.ProcessorCount >= 8 ? 4 : 2;
    public string? FfmpegPath    { get; set; }
    public string? LastOutputDir  { get; set; }
    public string? LastResultText { get; set; }

    public static string DefaultOutputDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Audio.Cast");

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioCast", "settings.json");

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
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
