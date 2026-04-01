using Microsoft.Data.Sqlite;

namespace CopyPath.Services;

public class UsageService : IDisposable
{
    private readonly SqliteConnection _db;
    private const int MaxRecentPaths = 10;

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
            CREATE TABLE IF NOT EXISTS recent_paths (
                path       TEXT NOT NULL PRIMARY KEY,
                used_at    TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ── 포맷 사용 빈도 ────────────────────────────────────────────────────

    public async Task IncrementAsync(string key)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (key, count) VALUES ($k, 1)
            ON CONFLICT(key) DO UPDATE SET count = count + 1;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string, int>> GetAllAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key, count FROM usage";
        var result = new Dictionary<string, int>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result[r.GetString(0)] = r.GetInt32(1);
        return result;
    }

    // ── 최근 경로 히스토리 ────────────────────────────────────────────────

    public async Task AddRecentPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recent_paths (path, used_at) VALUES ($p, $t)
            ON CONFLICT(path) DO UPDATE SET used_at = $t;
            """;
        cmd.Parameters.AddWithValue("$p", path);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();

        // 최대 개수 초과 시 오래된 항목 삭제
        using var trim = _db.CreateCommand();
        trim.CommandText = $"""
            DELETE FROM recent_paths WHERE path NOT IN (
                SELECT path FROM recent_paths ORDER BY used_at DESC LIMIT {MaxRecentPaths}
            );
            """;
        await trim.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetRecentPathsAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT path FROM recent_paths ORDER BY used_at DESC LIMIT {MaxRecentPaths}";
        var result = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(r.GetString(0));
        return result;
    }

    public void Dispose() => _db.Dispose();
}
