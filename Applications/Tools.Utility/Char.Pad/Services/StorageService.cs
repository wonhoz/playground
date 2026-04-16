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
                char      TEXT NOT NULL PRIMARY KEY,
                used_at   TEXT NOT NULL,
                use_count INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS favorites (
                char       TEXT NOT NULL PRIMARY KEY,
                sort_order INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS custom_chars (
                char         TEXT NOT NULL PRIMARY KEY,
                display_name TEXT NOT NULL,
                created_at   TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS search_history (
                query      TEXT NOT NULL PRIMARY KEY,
                used_at    TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // 기존 DB 마이그레이션: use_count 컬럼 없는 경우 추가
        try
        {
            using var alt = _db.CreateCommand();
            alt.CommandText = "ALTER TABLE recents ADD COLUMN use_count INTEGER NOT NULL DEFAULT 1";
            alt.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException) { /* 이미 존재하면 무시 */ }

        // 기존 DB 마이그레이션: custom_chars sort_order 컬럼 없는 경우 추가
        try
        {
            using var alt = _db.CreateCommand();
            alt.CommandText = "ALTER TABLE custom_chars ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0";
            alt.ExecuteNonQuery();
            // 신규 추가된 컬럼 — 기존 행에 rowid 기반 고유 순서 부여
            using var init = _db.CreateCommand();
            init.CommandText = "UPDATE custom_chars SET sort_order = rowid";
            init.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException) { /* 이미 존재하면 무시 */ }
    }

    // ── Recents ──────────────────────────────────────────────────────

    public void AddRecent(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recents (char, used_at, use_count) VALUES ($ch, $now, 1)
            ON CONFLICT(char) DO UPDATE SET used_at = $now, use_count = use_count + 1;
            """;
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        // Keep only MaxRecents — use_count 우선, 동점이면 used_at 최신 순
        using var trim = _db.CreateCommand();
        trim.CommandText = """
            DELETE FROM recents
            WHERE char NOT IN (
                SELECT char FROM recents
                ORDER BY use_count DESC, used_at DESC
                LIMIT $max
            );
            """;
        trim.Parameters.AddWithValue("$max", MaxRecents);
        trim.ExecuteNonQuery();
    }

    public void ClearRecents()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM recents";
        cmd.ExecuteNonQuery();
    }

    public void RemoveRecent(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM recents WHERE char = $ch";
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetRecents()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char FROM recents ORDER BY use_count DESC, used_at DESC LIMIT $max";
        cmd.Parameters.AddWithValue("$max", MaxRecents);
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    /// <summary>최근 사용 문자 목록 + 사용 횟수 반환 (툴팁 표시용)</summary>
    public Dictionary<string, int> GetRecentUseCounts()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char, use_count FROM recents ORDER BY use_count DESC, used_at DESC LIMIT $max";
        cmd.Parameters.AddWithValue("$max", MaxRecents);
        var result = new Dictionary<string, int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result[reader.GetString(0)] = reader.GetInt32(1);
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

    /// <summary>즐겨찾기 순서를 한 칸 위(음수 delta)또는 아래(양수 delta)로 이동</summary>
    public void MoveFavorite(string ch, int delta)
    {
        var list = GetFavoritesWithOrder();
        int idx = list.FindIndex(x => x.Char == ch);
        int newIdx = Math.Clamp(idx + delta, 0, list.Count - 1);
        if (idx == newIdx) return;

        var item = list[idx];
        list.RemoveAt(idx);
        list.Insert(newIdx, item);

        // sort_order 재할당
        using var tx = _db.BeginTransaction();
        for (int i = 0; i < list.Count; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE favorites SET sort_order = $order WHERE char = $ch";
            cmd.Parameters.AddWithValue("$order", i);
            cmd.Parameters.AddWithValue("$ch", list[i].Char);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private List<(string Char, int Order)> GetFavoritesWithOrder()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char, sort_order FROM favorites ORDER BY sort_order";
        var result = new List<(string, int)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add((reader.GetString(0), reader.GetInt32(1)));
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

    // ── Custom Chars ─────────────────────────────────────────────────────

    public void AddCustomChar(string ch, string displayName)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO custom_chars (char, display_name, created_at, sort_order)
            VALUES ($ch, $name, $now, (SELECT COALESCE(MAX(sort_order), -1) + 1 FROM custom_chars))
            ON CONFLICT(char) DO UPDATE SET display_name = $name;
            """;
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.Parameters.AddWithValue("$name", displayName);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void UpdateCustomChar(string ch, string newName)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE custom_chars SET display_name = $name WHERE char = $ch";
        cmd.Parameters.AddWithValue("$name", newName);
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.ExecuteNonQuery();
    }

    public void RemoveCustomChar(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM custom_chars WHERE char = $ch";
        cmd.Parameters.AddWithValue("$ch", ch);
        cmd.ExecuteNonQuery();
    }

    public bool IsCustomChar(string ch)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM custom_chars WHERE char = $ch";
        cmd.Parameters.AddWithValue("$ch", ch);
        return cmd.ExecuteScalar() != null;
    }

    public List<(string Char, string Name)> GetCustomChars()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char, display_name FROM custom_chars ORDER BY sort_order";
        var result = new List<(string, string)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add((reader.GetString(0), reader.GetString(1)));
        return result;
    }

    /// <summary>커스텀 문자 순서를 한 칸 위(음수 delta) 또는 아래(양수 delta)로 이동</summary>
    public void MoveCustomChar(string ch, int delta)
    {
        var list = GetCustomCharsWithOrder();
        int idx = list.FindIndex(x => x.Char == ch);
        int newIdx = Math.Clamp(idx + delta, 0, list.Count - 1);
        if (idx == newIdx) return;

        var item = list[idx];
        list.RemoveAt(idx);
        list.Insert(newIdx, item);

        using var tx = _db.BeginTransaction();
        for (int i = 0; i < list.Count; i++)
        {
            using var cmd = _db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE custom_chars SET sort_order = $order WHERE char = $ch";
            cmd.Parameters.AddWithValue("$order", i);
            cmd.Parameters.AddWithValue("$ch", list[i].Char);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private List<(string Char, int Order)> GetCustomCharsWithOrder()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT char, sort_order FROM custom_chars ORDER BY sort_order";
        var result = new List<(string, int)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── Search History ───────────────────────────────────────────────────

    private const int MaxSearchHistory = 10;

    public void AddSearchHistory(string query)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO search_history (query, used_at) VALUES ($q, $now)
            ON CONFLICT(query) DO UPDATE SET used_at = $now;
            """;
        cmd.Parameters.AddWithValue("$q", query);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        using var trim = _db.CreateCommand();
        trim.CommandText = """
            DELETE FROM search_history
            WHERE query NOT IN (
                SELECT query FROM search_history ORDER BY used_at DESC LIMIT $max
            );
            """;
        trim.Parameters.AddWithValue("$max", MaxSearchHistory);
        trim.ExecuteNonQuery();
    }

    public List<string> GetSearchHistory()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT query FROM search_history ORDER BY used_at DESC LIMIT $max";
        cmd.Parameters.AddWithValue("$max", MaxSearchHistory);
        var result = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    public void RemoveSearchHistory(string query)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM search_history WHERE query = $q";
        cmd.Parameters.AddWithValue("$q", query);
        cmd.ExecuteNonQuery();
    }

    // ── Export / Import ─────────────────────────────────────────────────

    public record ExportData(List<string> Favorites, List<(string Char, string Name)> CustomChars);

    public ExportData Export() => new(GetFavorites(), GetCustomChars());

    public void Import(ExportData data, bool overwrite)
    {
        var favSql = overwrite
            ? "INSERT OR REPLACE INTO favorites (char, sort_order) VALUES ($ch, (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM favorites))"
            : "INSERT OR IGNORE INTO favorites (char, sort_order) VALUES ($ch, (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM favorites))";
        var customSql = overwrite
            ? "INSERT OR REPLACE INTO custom_chars (char, display_name, created_at) VALUES ($ch, $name, $now)"
            : "INSERT OR IGNORE INTO custom_chars (char, display_name, created_at) VALUES ($ch, $name, $now)";

        using var tx = _db.BeginTransaction();
        foreach (var ch in data.Favorites)
        {
            using var cmd = _db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = favSql;
            cmd.Parameters.AddWithValue("$ch", ch);
            cmd.ExecuteNonQuery();
        }
        foreach (var (ch, name) in data.CustomChars)
        {
            using var cmd = _db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = customSql;
            cmd.Parameters.AddWithValue("$ch", ch);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Dispose() => _db.Dispose();
}
