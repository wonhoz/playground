using Microsoft.Data.Sqlite;

namespace CharPad.Services;

public class StorageService : IDisposable
{
    private readonly SqliteConnection _db;
    private const int MaxRecents = 20;

    public StorageService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Playground", "Char.Pad");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "charpad.db");

        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS recents (
                char     TEXT NOT NULL PRIMARY KEY,
                used_at  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS favorites (
                char       TEXT NOT NULL PRIMARY KEY,
                sort_order INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Recents ──────────────────────────────────────────────────────

    public void AddRecent(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recents (char, used_at) VALUES ($ch, $now)
            ON CONFLICT(char) DO UPDATE SET used_at = $now;
            """;
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        // Keep only MaxRecents most recent
        using var trim = _db.CreateCommand();
        trim.CommandText = """
            DELETE FROM recents
            WHERE char NOT IN (
                SELECT char FROM recents
                ORDER BY used_at DESC
                LIMIT $max
            );
            """;
        trim.Parameters.AddWithValue("$max", MaxRecents);
        trim.ExecuteNonQuery();
    }

    public List<string> GetRecents()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char FROM recents ORDER BY used_at DESC LIMIT $max";
        cmd.Parameters.AddWithValue("$max", MaxRecents);
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    // ── Favorites ────────────────────────────────────────────────────

    public void AddFavorite(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO favorites (char, sort_order)
            VALUES ($ch, (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM favorites));
            """;
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.ExecuteNonQuery();
    }

    public void RemoveFavorite(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM favorites WHERE char = $ch";
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.ExecuteNonQuery();
    }

    public bool IsFavorite(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM favorites WHERE char = $ch";
        cmd.Parameters.AddWithValue("$ch", ch);
        return cmd.ExecuteScalar() != null;
    }

    public List<string> GetFavorites()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char FROM favorites ORDER BY sort_order";
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    // ── Settings ─────────────────────────────────────────────────────────

    public string? GetSetting(string key)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = $value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
