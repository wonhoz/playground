using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public static class SettingsService
{
    private static readonly string _path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickLauncher", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented    = true,
        Encoder          = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static LauncherSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(_path))
            {
                var json = System.IO.File.ReadAllText(_path);
                return JsonSerializer.Deserialize<LauncherSettings>(json, _opts) ?? new();
            }
        }
        catch { }
        return new LauncherSettings();
    }

    public static void Save(LauncherSettings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            System.IO.File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
        }
        catch { }
    }
}
