using System.IO;
using System.Text.Json;
using CrosswordCast.Models;

namespace CrosswordCast.Services;

public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrosswordCast", "state.json");

    public static void Save(int seed, char[,] userGrid, int elapsedSeconds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            int n = Puzzle.N;
            var flat = new char[n * n];
            for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                flat[r * n + c] = userGrid[r, c];

            var data = new SaveData
            {
                Seed           = seed,
                UserGrid       = new string(flat),
                ElapsedSeconds = elapsedSeconds,
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    public static (int seed, char[,] userGrid, int elapsedSeconds)? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath));
            if (data is null || data.UserGrid.Length < Puzzle.N * Puzzle.N) return null;

            int n    = Puzzle.N;
            var grid = new char[n, n];
            for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                grid[r, c] = data.UserGrid[r * n + c];

            return (data.Seed, grid, data.ElapsedSeconds);
        }
        catch { return null; }
    }

    private class SaveData
    {
        public int    Seed           { get; set; }
        public string UserGrid       { get; set; } = "";
        public int    ElapsedSeconds { get; set; }
    }
}
