using System.IO;
using System.Text.Json;

namespace DnsFlip.Models;

public sealed class AppConfig
{
    public List<DnsPreset> Presets { get; set; } = DnsPreset.GetDefaults();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DnsFlip", "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
            }
        }
        catch { }

        var config = new AppConfig();
        config.Save();
        return config;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
