using System.IO;
using System.Text.Json;
using EnvGuard.Models;

namespace EnvGuard.Services;

public static class SnapshotService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EnvGuard", "Snapshots");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static SnapshotService()
    {
        Directory.CreateDirectory(Dir);
    }

    public static Snapshot CreateSnapshot(string description, List<EnvVariable> variables)
    {
        var snap = new Snapshot
        {
            CreatedAt = DateTime.Now,
            Description = description,
            Entries = variables.Select(v => new SnapshotEntry
            {
                Name = v.Name, Value = v.Value, Scope = v.Scope
            }).ToList()
        };

        var filename = $"{snap.CreatedAt:yyyyMMdd_HHmmss}_{SanitizeFilename(description)}.json";
        var path = Path.Combine(Dir, filename);
        var json = JsonSerializer.Serialize(snap, JsonOpts);
        File.WriteAllText(path, json);

        return snap;
    }

    public static List<(string FilePath, Snapshot Snapshot)> GetSnapshots()
    {
        var list = new List<(string, Snapshot)>();

        if (!Directory.Exists(Dir)) return list;

        foreach (var file in Directory.GetFiles(Dir, "*.json").OrderByDescending(f => f))
        {
            try
            {
                var json = File.ReadAllText(file);
                var snap = JsonSerializer.Deserialize<Snapshot>(json, JsonOpts);
                if (snap != null)
                    list.Add((file, snap));
            }
            catch { /* skip corrupt files */ }
        }

        return list;
    }

    public static void RestoreSnapshot(Snapshot snapshot)
    {
        // Restore user variables
        var userEntries = snapshot.Entries.Where(e => e.Scope == EnvScope.User).ToList();
        var currentUser = EnvService.GetAll().Where(v => v.Scope == EnvScope.User).ToList();

        // Delete variables not in snapshot
        foreach (var cur in currentUser)
        {
            if (!userEntries.Any(e => e.Name.Equals(cur.Name, StringComparison.OrdinalIgnoreCase)))
                EnvService.DeleteVariable(cur.Name, EnvScope.User);
        }

        // Set/update snapshot variables
        foreach (var entry in userEntries)
            EnvService.SetVariable(entry.Name, entry.Value, EnvScope.User);

        // System variables (only if admin)
        if (EnvService.IsAdmin())
        {
            var sysEntries = snapshot.Entries.Where(e => e.Scope == EnvScope.System).ToList();
            var currentSys = EnvService.GetAll().Where(v => v.Scope == EnvScope.System).ToList();

            foreach (var cur in currentSys)
            {
                if (!sysEntries.Any(e => e.Name.Equals(cur.Name, StringComparison.OrdinalIgnoreCase)))
                    EnvService.DeleteVariable(cur.Name, EnvScope.System);
            }

            foreach (var entry in sysEntries)
                EnvService.SetVariable(entry.Name, entry.Value, EnvScope.System);
        }
    }

    public static void DeleteSnapshot(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
