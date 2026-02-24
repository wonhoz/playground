using System.Text.Json;

namespace PortWatch.Services;

public static class FavoritesService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PortWatch", "favorites.json");

    private static HashSet<int> _favs = [];

    static FavoritesService() => Load();

    public static bool IsFavorite(int port) => _favs.Contains(port);
    public static IReadOnlySet<int> All => _favs;

    public static void Toggle(int port)
    {
        if (!_favs.Remove(port)) _favs.Add(port);
        Save();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(_path))
                _favs = JsonSerializer.Deserialize<HashSet<int>>(File.ReadAllText(_path)) ?? [];
        }
        catch { }

        // 기본 즐겨찾기 포트
        if (_favs.Count == 0)
            _favs = [3000, 8080, 8000, 5000, 4200, 5432, 3306, 6379, 27017, 443, 80];
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_favs));
        }
        catch { }
    }
}
