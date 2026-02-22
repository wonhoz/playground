using CommuteBuddy.Models;

namespace CommuteBuddy.Services;

public class SettingsService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CommuteBuddy", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented           = true,
        PropertyNameCaseInsensitive = true,
    };

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return AppSettings.CreateDefault();
        try
        {
            var text = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(text, JsonOpts)
                   ?? AppSettings.CreateDefault();
        }
        catch { return AppSettings.CreateDefault(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
