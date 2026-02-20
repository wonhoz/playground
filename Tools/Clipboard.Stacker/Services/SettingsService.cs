using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClipboardStacker.Models;

namespace ClipboardStacker.Services;

public static class SettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipboardStacker", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _opts) ?? new();
        }
        catch { }
        return new AppSettings();
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
