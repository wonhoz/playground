using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ApiProbe.Models;

namespace ApiProbe.Services;

public static class CollectionService
{
    private static readonly string _dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApiProbe");
    private static readonly string _file = Path.Combine(_dir, "collections.json");

    public static ObservableCollection<ApiCollection> Load()
    {
        try
        {
            if (!File.Exists(_file)) return [];
            var json = File.ReadAllText(_file);
            var list = JsonSerializer.Deserialize<List<ApiCollection>>(json);
            return list is null ? [] : new ObservableCollection<ApiCollection>(list);
        }
        catch { return []; }
    }

    public static void Save(IEnumerable<ApiCollection> collections)
    {
        Directory.CreateDirectory(_dir);
        var json = JsonSerializer.Serialize(collections,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}
