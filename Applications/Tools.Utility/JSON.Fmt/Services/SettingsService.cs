using System.IO;
using System.Text.Json;

namespace JsonFmt.Services;

public class WindowSettings
{
    public double Width { get; set; } = 1000;
    public double Height { get; set; } = 640;
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JsonFmt", "settings.json");

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<WindowSettings>(json) ?? new WindowSettings();
            }
        }
        catch { }
        return new WindowSettings();
    }

    public static void Save(WindowSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch { }
    }
}
