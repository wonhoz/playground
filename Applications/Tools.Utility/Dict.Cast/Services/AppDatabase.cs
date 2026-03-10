namespace Dict.Cast.Services;

using Microsoft.Data.Sqlite;

public class AppDatabase : IDisposable
{
    readonly SqliteConnection _conn;
    readonly object _lock = new();

    public AppDatabase(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        Init();
    }

    void Init()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS history (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                word     TEXT NOT NULL,
                looked_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS wordlist (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                word     TEXT NOT NULL UNIQUE,
                added_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS translations (
                key      TEXT PRIMARY KEY,
                ko       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_history_word ON history(word);
        ";
        cmd.ExecuteNonQuery();
    }

    public void AddHistory(string word)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO history(word) VALUES(@w)";
        cmd.Parameters.AddWithValue("@w", word.ToLowerInvariant());
        cmd.ExecuteNonQuery();
    }

    public List<string> GetRecentHistory(int limit = 8)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT word FROM history ORDER BY id DESC LIMIT @lim";
        cmd.Parameters.AddWithValue("@lim", limit);
        var result = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    public bool IsInWordlist(string word)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM wordlist WHERE word=@w";
        cmd.Parameters.AddWithValue("@w", word.ToLowerInvariant());
        return cmd.ExecuteScalar() != null;
    }

    public void AddToWordlist(string word)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO wordlist(word) VALUES(@w)";
        cmd.Parameters.AddWithValue("@w", word.ToLowerInvariant());
        cmd.ExecuteNonQuery();
    }

    public void RemoveFromWordlist(string word)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM wordlist WHERE word=@w";
        cmd.Parameters.AddWithValue("@w", word.ToLowerInvariant());
        cmd.ExecuteNonQuery();
    }

    public List<string> GetWordlist()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT word FROM wordlist ORDER BY word";
        var result = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    public void ExportWordlistToCsv(string path)
    {
        var words = GetWordlist();
        File.WriteAllLines(path, new[] { "word" }.Concat(words));
    }

    public void ExportWordlistToAnki(string path)
    {
        var words = GetWordlist();
        File.WriteAllLines(path, words.Select(w => $"{w}\t{w}"));
    }

    // ── 번역 캐시 ─────────────────────────────────────────────────────────

    public string? GetCachedTranslation(string key)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT ko FROM translations WHERE key=@k";
            cmd.Parameters.AddWithValue("@k", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void CacheTranslation(string key, string ko)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO translations(key,ko) VALUES(@k,@v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", ko);
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() => _conn.Dispose();
}
