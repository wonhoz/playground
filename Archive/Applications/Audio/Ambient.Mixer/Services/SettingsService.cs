using System.Text.Json;
using AmbientMixer.Models;

namespace AmbientMixer.Services;

public static class SettingsService
{
    private static readonly string Dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmbientMixer");
    private static readonly string File = Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static MixerSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
            {
                var json = System.IO.File.ReadAllText(File);
                return JsonSerializer.Deserialize<MixerSettings>(json, Opts) ?? new MixerSettings();
            }
        }
        catch { }
        return new MixerSettings();
    }

    public static void Save(MixerSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(settings, Opts));
        }
        catch { }
    }
}
