using Microsoft.Data.Sqlite;

namespace CopyPath.Services;

public record RecentPath(string Path, bool Starred);

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
                used_at    TEXT NOT NULL,
                starred    INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS format_visibility (
                key     TEXT NOT NULL PRIMARY KEY,
                hidden  INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
                key    TEXT NOT NULL PRIMARY KEY,
                value  TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // 기존 DB에 starred 컬럼이 없을 경우 마이그레이션
        try
        {
            using var alter = _db.CreateCommand();
            alter.CommandText = "ALTER TABLE recent_paths ADD COLUMN starred INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }
        catch { /* 이미 존재하면 무시 */ }
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

        // 최대 개수 초과 시 오래된 항목 삭제 (별표 고정 항목 제외)
        using var trim = _db.CreateCommand();
        trim.CommandText = $"""
            DELETE FROM recent_paths
            WHERE starred = 0
              AND path NOT IN (
                  SELECT path FROM recent_paths
                  WHERE starred = 0
                  ORDER BY used_at DESC LIMIT {MaxRecentPaths}
              );
            """;
        await trim.ExecuteNonQueryAsync();
    }

    public async Task<List<RecentPath>> GetRecentPathsAsync()
    {
        using var cmd = _db.CreateCommand();
        // 별표 고정 항목 먼저, 이후 최신순
        cmd.CommandText = $"""
            SELECT path, starred FROM recent_paths
            ORDER BY starred DESC, used_at DESC
            LIMIT {MaxRecentPaths}
            """;
        var result = new List<RecentPath>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new RecentPath(r.GetString(0), r.GetInt32(1) == 1));
        return result;
    }

    public async Task DeleteRecentAsync(string path)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_paths WHERE path = $p";
        cmd.Parameters.AddWithValue("$p", path);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ToggleStarAsync(string path)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE recent_paths SET starred = 1 - starred WHERE path = $p";
        cmd.Parameters.AddWithValue("$p", path);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 포맷 숨기기 ───────────────────────────────────────────────────────

    public async Task<HashSet<string>> GetHiddenFormatsAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT key FROM format_visibility WHERE hidden = 1";
        var result = new HashSet<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(r.GetString(0));
        return result;
    }

    public async Task SetFormatHiddenAsync(string key, bool hidden)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO format_visibility (key, hidden) VALUES ($k, $h)
            ON CONFLICT(key) DO UPDATE SET hidden = $h;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$h", hidden ? 1 : 0);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 설정 ─────────────────────────────────────────────────────────────

    public async Task<int> GetHideDelayAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = 'hide_delay'";
        var val = await cmd.ExecuteScalarAsync();
        return val is string s && int.TryParse(s, out int ms) ? ms : 400;
    }

    public async Task SetHideDelayAsync(int ms)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ('hide_delay', $v)
            ON CONFLICT(key) DO UPDATE SET value = $v;
            """;
        cmd.Parameters.AddWithValue("$v", ms.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 포맷 전체 복원 ────────────────────────────────────────────────────

    public async Task RestoreAllFormatsAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM format_visibility";
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose() => _db.Dispose();
}
