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
        if (File.Exists(_path))
        {
            try
            {
                _favs = JsonSerializer.Deserialize<HashSet<int>>(File.ReadAllText(_path)) ?? [];
                return;  // 파일이 존재하면 빈 배열이라도 그대로 사용 (사용자가 의도적으로 비운 것)
            }
            catch { }
        }

        // 파일이 없을 때만 기본 즐겨찾기 적용
        _favs = [80, 443, 3000, 3306, 4200, 5000, 5432, 6379, 8000, 8080, 27017];
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
