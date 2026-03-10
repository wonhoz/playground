using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseFlick.Models;

public sealed class AppSettings
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MouseFlick", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented    = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool   Enabled          { get; set; } = true;
    public int    GestureThreshold { get; set; } = 30;
    public bool   ShowOverlay      { get; set; } = true;
    public List<GestureProfile> Profiles { get; set; } = [];

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(_path), _opts);
                if (s != null) return s;
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
            File.WriteAllText(_path, JsonSerializer.Serialize(this, _opts));
        }
        catch { }
    }
}
