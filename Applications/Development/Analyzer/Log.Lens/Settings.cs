using System.IO;
using System.Text.Json;

namespace LogLens;

internal record AppSettings(
    double Left, double Top, double Width, double Height,
    int MaxLinesIndex, int EncodingIndex);

internal static class SettingsManager
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LogLens", "settings.json");

    internal static AppSettings Default => new(double.NaN, double.NaN, 1080, 720, 1, 0);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? Default;
        }
        catch { }
        return Default;
    }

    public static void Save(AppSettings s)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(s));
        }
        catch { }
    }
}
