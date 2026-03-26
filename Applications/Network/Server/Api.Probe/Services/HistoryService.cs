using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ApiProbe.Models;

namespace ApiProbe.Services;

public static class HistoryService
{
    private static readonly string _dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApiProbe");
    private static readonly string _file = Path.Combine(_dir, "history.json");

    public const int MaxHistory = 50;

    public static List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_file)) return [];
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void Save(IEnumerable<HistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        catch (Exception ex) { Debug.WriteLine($"[HistoryService] Save 실패: {ex.Message}"); }
    }
}
