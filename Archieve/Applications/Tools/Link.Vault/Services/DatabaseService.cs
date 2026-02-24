using System.IO;
using LinkVault.Models;
using Microsoft.Data.Sqlite;

namespace LinkVault.Services;

public class DatabaseService : IDisposable
{
    private readonly SqliteConnection _conn;

    private static readonly string DbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinkVault", "bookmarks.db");

    public DatabaseService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        _conn = new SqliteConnection($"Data Source={DbPath}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;

            CREATE TABLE IF NOT EXISTS bookmarks (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                url          TEXT NOT NULL,
                title        TEXT NOT NULL DEFAULT '',
                description  TEXT NOT NULL DEFAULT '',
                tags         TEXT NOT NULL DEFAULT '',
                stars        INTEGER NOT NULL DEFAULT 0,
                is_read      INTEGER NOT NULL DEFAULT 0,
                snapshot_path TEXT,
                favicon_path  TEXT,
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_bookmarks_created ON bookmarks(created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_bookmarks_stars   ON bookmarks(stars DESC);

            CREATE VIRTUAL TABLE IF NOT EXISTS bookmarks_fts USING fts5(
                title, description, tags, body,
                content=bookmarks, content_rowid=id,
                tokenize='unicode61'
            );

            CREATE TRIGGER IF NOT EXISTS bookmarks_ai AFTER INSERT ON bookmarks BEGIN
                INSERT INTO bookmarks_fts(rowid, title, description, tags, body)
                VALUES (new.id, new.title, new.description, new.tags, '');
            END;

            CREATE TRIGGER IF NOT EXISTS bookmarks_ad AFTER DELETE ON bookmarks BEGIN
                INSERT INTO bookmarks_fts(bookmarks_fts, rowid, title, description, tags, body)
                VALUES ('delete', old.id, old.title, old.description, old.tags, '');
            END;

            CREATE TRIGGER IF NOT EXISTS bookmarks_au AFTER UPDATE ON bookmarks BEGIN
                INSERT INTO bookmarks_fts(bookmarks_fts, rowid, title, description, tags, body)
                VALUES ('delete', old.id, old.title, old.description, old.tags, '');
                INSERT INTO bookmarks_fts(rowid, title, description, tags, body)
                VALUES (new.id, new.title, new.description, new.tags, '');
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    // ── CRUD ──────────────────────────────────────────────────────────

    public long Insert(Bookmark b)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bookmarks (url, title, description, tags, stars, is_read,
                                   snapshot_path, favicon_path, created_at, updated_at)
            VALUES ($url, $title, $desc, $tags, $stars, $read,
                    $snap, $fav, $ca, $ua);
            SELECT last_insert_rowid();
            """;
        var now = DateTime.UtcNow.ToString("O");
        cmd.Parameters.AddWithValue("$url",   b.Url);
        cmd.Parameters.AddWithValue("$title", b.Title);
        cmd.Parameters.AddWithValue("$desc",  b.Description);
        cmd.Parameters.AddWithValue("$tags",  b.Tags);
        cmd.Parameters.AddWithValue("$stars", b.Stars);
        cmd.Parameters.AddWithValue("$read",  b.IsRead ? 1 : 0);
        cmd.Parameters.AddWithValue("$snap",  (object?)b.SnapshotPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fav",   (object?)b.FaviconPath  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ca",    now);
        cmd.Parameters.AddWithValue("$ua",    now);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void Update(Bookmark b)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE bookmarks SET
                url=          $url,
                title=        $title,
                description=  $desc,
                tags=         $tags,
                stars=        $stars,
                is_read=      $read,
                snapshot_path=$snap,
                favicon_path= $fav,
                updated_at=   $ua
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id",    b.Id);
        cmd.Parameters.AddWithValue("$url",   b.Url);
        cmd.Parameters.AddWithValue("$title", b.Title);
        cmd.Parameters.AddWithValue("$desc",  b.Description);
        cmd.Parameters.AddWithValue("$tags",  b.Tags);
        cmd.Parameters.AddWithValue("$stars", b.Stars);
        cmd.Parameters.AddWithValue("$read",  b.IsRead ? 1 : 0);
        cmd.Parameters.AddWithValue("$snap",  (object?)b.SnapshotPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fav",   (object?)b.FaviconPath  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ua",    DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM bookmarks WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetRead(long id, bool isRead)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE bookmarks SET is_read=$r, updated_at=$ua WHERE id=$id";
        cmd.Parameters.AddWithValue("$r",  isRead ? 1 : 0);
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetStars(long id, int stars)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE bookmarks SET stars=$s, updated_at=$ua WHERE id=$id";
        cmd.Parameters.AddWithValue("$s",  stars);
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSnapshotPath(long id, string path)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE bookmarks SET snapshot_path=$p, updated_at=$ua WHERE id=$id";
        cmd.Parameters.AddWithValue("$p",  path);
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ── 조회 ──────────────────────────────────────────────────────────

    /// <summary>전체 목록 (최신순)</summary>
    public List<Bookmark> GetAll(bool unreadOnly = false, int? minStars = null, string? tag = null)
    {
        var where = BuildWhere(unreadOnly, minStars, tag);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM bookmarks{where} ORDER BY created_at DESC LIMIT 500";
        return ReadBookmarks(cmd);
    }

    /// <summary>FTS 전문 검색</summary>
    public List<Bookmark> Search(string query, bool unreadOnly = false, int? minStars = null, string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return GetAll(unreadOnly, minStars, tag);

        var ftsQuery = string.Join(" OR ",
            query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => $"\"{t.Replace("\"", "")}\"*"));

        using var cmd = _conn.CreateCommand();
        var extraWhere = BuildWhereExtra(unreadOnly, minStars, tag);
        cmd.CommandText = $"""
            SELECT b.* FROM bookmarks b
            JOIN bookmarks_fts fts ON b.id = fts.rowid
            WHERE bookmarks_fts MATCH $q {extraWhere}
            ORDER BY rank, b.created_at DESC
            LIMIT 200
            """;
        cmd.Parameters.AddWithValue("$q", ftsQuery);
        return ReadBookmarks(cmd);
    }

    /// <summary>모든 태그 목록 (빈도순)</summary>
    public List<string> GetAllTags()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT tags FROM bookmarks WHERE tags != ''";
        var tagCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            foreach (var t in reader.GetString(0).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                tagCount[t] = tagCount.GetValueOrDefault(t) + 1;
        }
        return tagCount.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    private object? Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public int CountAll()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bookmarks";
        return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private static string BuildWhere(bool unreadOnly, int? minStars, string? tag)
    {
        var parts = new List<string>();
        if (unreadOnly) parts.Add("is_read=0");
        if (minStars.HasValue) parts.Add($"stars>={minStars}");
        if (!string.IsNullOrWhiteSpace(tag))
            parts.Add($"(',' || tags || ',') LIKE '%,{tag.Replace("'", "''")},%'");
        return parts.Count > 0 ? " WHERE " + string.Join(" AND ", parts) : "";
    }

    private static string BuildWhereExtra(bool unreadOnly, int? minStars, string? tag)
    {
        var parts = new List<string>();
        if (unreadOnly) parts.Add("b.is_read=0");
        if (minStars.HasValue) parts.Add($"b.stars>={minStars}");
        if (!string.IsNullOrWhiteSpace(tag))
            parts.Add($"(',' || b.tags || ',') LIKE '%,{tag.Replace("'", "''")},%'");
        return parts.Count > 0 ? "AND " + string.Join(" AND ", parts) : "";
    }

    private static List<Bookmark> ReadBookmarks(SqliteCommand cmd)
    {
        var list = new List<Bookmark>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapRow(reader));
        return list;
    }

    private static Bookmark MapRow(SqliteDataReader r) => new()
    {
        Id           = r.GetInt64(r.GetOrdinal("id")),
        Url          = r.GetString(r.GetOrdinal("url")),
        Title        = r.GetString(r.GetOrdinal("title")),
        Description  = r.GetString(r.GetOrdinal("description")),
        Tags         = r.GetString(r.GetOrdinal("tags")),
        Stars        = r.GetInt32(r.GetOrdinal("stars")),
        IsRead       = r.GetInt32(r.GetOrdinal("is_read")) == 1,
        SnapshotPath = r.IsDBNull(r.GetOrdinal("snapshot_path")) ? null : r.GetString(r.GetOrdinal("snapshot_path")),
        FaviconPath  = r.IsDBNull(r.GetOrdinal("favicon_path"))  ? null : r.GetString(r.GetOrdinal("favicon_path")),
        CreatedAt    = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))).ToLocalTime(),
        UpdatedAt    = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at"))).ToLocalTime()
    };

    public void Dispose() => _conn.Dispose();
}
