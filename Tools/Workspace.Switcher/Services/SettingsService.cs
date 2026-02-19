using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using WorkspaceSwitcher.Models;

namespace WorkspaceSwitcher.Services;

public static class SettingsService
{
    private static readonly string _path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WorkspaceSwitcher", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static SwitcherSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(_path))
            {
                var json = System.IO.File.ReadAllText(_path);
                return JsonSerializer.Deserialize<SwitcherSettings>(json, _opts) ?? new();
            }
        }
        catch { }
        return new SwitcherSettings();
    }

    public static void Save(SwitcherSettings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            System.IO.File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
        }
        catch { }
    }
}
