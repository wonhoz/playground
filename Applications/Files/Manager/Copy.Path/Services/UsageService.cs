using Microsoft.Data.Sqlite;

namespace CopyPath.Services;

public class UsageService : IDisposable
{
    private readonly SqliteConnection _db;

    public UsageService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Playground", "Copy.Path");
        Directory.CreateDirectory(dir);
        _db = new SqliteConnection($"Data Source={Path.Combine(dir, "usage.db")}");
        _db.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS usage (
                key    TEXT NOT NULL PRIMARY KEY,
                count  INTEGER NOT NULL DEFAULT 0
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Increment(string key)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (key, count) VALUES ($k, 1)
            ON CONFLICT(key) DO UPDATE SET count = count + 1;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<string, int> GetAll()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key, count FROM usage";
        var result = new Dictionary<string, int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetString(0)] = r.GetInt32(1);
        return result;
    }

    public void Dispose() => _db.Dispose();
}
