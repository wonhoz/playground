using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using SoundBoard.Models;

namespace SoundBoard.Services;

public static class SettingsService
{
    private static readonly string Dir  =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundBoard");
    private static readonly string Path = System.IO.Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // 이모지 그대로 저장
    };

    public static BoardSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<BoardSettings>(File.ReadAllText(Path), Opts);
                if (s is not null) return s;
            }
        }
        catch { }
        return BoardSettings.CreateDefault();
    }

    public static void Save(BoardSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(settings, Opts));
        }
        catch { }
    }
}
