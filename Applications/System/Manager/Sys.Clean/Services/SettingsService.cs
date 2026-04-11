using System.Text.Json;

namespace SysClean.Services;

public record AppSettings(
    double Left, double Top, double Width, double Height,
    string LastTab, string[]? SelectedCleanerIds = null);

public static class SettingsService
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SysClean", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return Default();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? Default();
        }
        catch { return Default(); }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
        }
        catch { }
    }

    private static AppSettings Default() => new(double.NaN, double.NaN, 1100, 720, "Cleaner");
}
