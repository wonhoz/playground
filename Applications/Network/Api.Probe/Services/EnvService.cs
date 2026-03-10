using System.IO;
using System.Text.Json;
using ApiProbe.Models;

namespace ApiProbe.Services;

public static class EnvService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ApiProbe", "environments.json");

    public static List<EnvPreset> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<List<EnvPreset>>(json);
                if (list is { Count: > 0 })
                    return list;
            }
        }
        catch { }

        // 기본 환경 목록
        return
        [
            new() { Name = "Local", Variables = new() { ["BASE_URL"] = "http://localhost:3000" } },
            new() { Name = "Dev",   Variables = new() { ["BASE_URL"] = "https://dev.example.com" } },
            new() { Name = "Prod",  Variables = new() { ["BASE_URL"] = "https://api.example.com" } },
        ];
    }

    public static void Save(IEnumerable<EnvPreset> presets)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(presets.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch { }
    }
}
