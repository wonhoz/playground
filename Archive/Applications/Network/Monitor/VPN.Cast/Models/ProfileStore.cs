using System.Text.Json;

namespace VpnCast.Models;

public static class ProfileStore
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnCast");
    private static readonly string _file = Path.Combine(_dir, "profiles.json");

    public static List<TunnelProfile> Load()
    {
        try
        {
            Directory.CreateDirectory(_dir);
            if (!File.Exists(_file)) return [];
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<List<TunnelProfile>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void Save(IEnumerable<TunnelProfile> profiles)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(profiles.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        catch { }
    }

    public static string ProfilesDir => Path.Combine(_dir, "configs");
}
