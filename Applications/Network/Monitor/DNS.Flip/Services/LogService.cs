using System.IO;
using System.Text.Json;
using DnsFlip.Models;

namespace DnsFlip.Services;

public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DnsFlip", "change_log.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void AddEntry(DnsLogEntry entry)
    {
        var entries = GetEntries();
        entries.Insert(0, entry);

        // Keep last 200 entries
        if (entries.Count > 200)
            entries = entries.Take(200).ToList();

        var dir = Path.GetDirectoryName(LogPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(LogPath, JsonSerializer.Serialize(entries, JsonOpts));
    }

    public static List<DnsLogEntry> GetEntries()
    {
        try
        {
            if (File.Exists(LogPath))
            {
                var json = File.ReadAllText(LogPath);
                return JsonSerializer.Deserialize<List<DnsLogEntry>>(json, JsonOpts) ?? [];
            }
        }
        catch { }
        return [];
    }
}
