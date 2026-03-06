using System.IO;
using IconHunt.Models;
using Microsoft.Data.Sqlite;

namespace IconHunt.Services;

public class IconDatabase : IDisposable
{
    private readonly SqliteConnection _conn;
    public static readonly string DbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "IconHunt", "icons.db");

    public IconDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        _conn = new SqliteConnection($"Data Source={DbPath}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS collections (
                prefix     TEXT PRIMARY KEY,
                name       TEXT NOT NULL,
                total      INTEGER,
                license    TEXT,
                author     TEXT,
                url        TEXT,
                is_indexed INTEGER DEFAULT 0,
                indexed_at TEXT
            );
            CREATE TABLE IF NOT EXISTS icons (
                id         TEXT PRIMARY KEY,  -- 'prefix:name'
                prefix     TEXT NOT NULL,
                name       TEXT NOT NULL,
                tags       TEXT,
                svg_cached INTEGER DEFAULT 0
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS icons_fts USING fts5(
                id, name, tags, content='icons', content_rowid='rowid'
            );
            CREATE TABLE IF NOT EXISTS favorites (
                id         TEXT PRIMARY KEY,
                added_at   TEXT DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS recents (
                id         TEXT PRIMARY KEY,
                used_at    TEXT DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_icons_prefix ON icons(prefix);
        ";
        cmd.ExecuteNonQuery();
    }

    // ── 컬렉션 ──────────────────────────────────────────────
    public void UpsertCollection(IconCollection col)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO collections (prefix,name,total,license,author,url,is_indexed)
            VALUES ($p,$n,$t,$l,$a,$u,$i)
            ON CONFLICT(prefix) DO UPDATE SET
                name=excluded.name, total=excluded.total,
                license=excluded.license, author=excluded.author,
                url=excluded.url, is_indexed=excluded.is_indexed";
        cmd.Parameters.AddWithValue("$p", col.Prefix);
        cmd.Parameters.AddWithValue("$n", col.Name);
        cmd.Parameters.AddWithValue("$t", col.Total);
        cmd.Parameters.AddWithValue("$l", col.License);
        cmd.Parameters.AddWithValue("$a", col.Author);
        cmd.Parameters.AddWithValue("$u", col.Url);
        cmd.Parameters.AddWithValue("$i", col.IsIndexed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public List<IconCollection> GetCollections()
    {
        var result = new List<IconCollection>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT prefix,name,total,license,author,url,is_indexed FROM collections ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new IconCollection
            {
                Prefix = r.GetString(0), Name = r.GetString(1),
                Total = r.GetInt32(2), License = r.GetString(3),
                Author = r.IsDBNull(4) ? "" : r.GetString(4),
                Url = r.IsDBNull(5) ? "" : r.GetString(5),
                IsIndexed = r.GetInt32(6) == 1
            });
        }
        return result;
    }

    public void MarkCollectionIndexed(string prefix, int total)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"UPDATE collections SET is_indexed=1, indexed_at=datetime('now'), total=$t WHERE prefix=$p";
        cmd.Parameters.AddWithValue("$t", total);
        cmd.Parameters.AddWithValue("$p", prefix);
        cmd.ExecuteNonQuery();
    }

    // ── 아이콘 벌크 삽입 ────────────────────────────────────
    public void BulkInsertIcons(IEnumerable<IconEntry> icons)
    {
        using var tx = _conn.BeginTransaction();
        using var cmdIcon = _conn.CreateCommand();
        cmdIcon.Transaction = tx;
        cmdIcon.CommandText = @"
            INSERT OR IGNORE INTO icons (id, prefix, name, tags)
            VALUES ($id, $p, $n, $t)";
        var pId = cmdIcon.Parameters.Add("$id", SqliteType.Text);
        var pP = cmdIcon.Parameters.Add("$p", SqliteType.Text);
        var pN = cmdIcon.Parameters.Add("$n", SqliteType.Text);
        var pT = cmdIcon.Parameters.Add("$t", SqliteType.Text);

        using var cmdFts = _conn.CreateCommand();
        cmdFts.Transaction = tx;
        cmdFts.CommandText = "INSERT OR IGNORE INTO icons_fts(id,name,tags) VALUES($id,$n,$t)";
        var fId = cmdFts.Parameters.Add("$id", SqliteType.Text);
        var fN = cmdFts.Parameters.Add("$n", SqliteType.Text);
        var fT = cmdFts.Parameters.Add("$t", SqliteType.Text);

        foreach (var icon in icons)
        {
            pId.Value = icon.Id; pP.Value = icon.Prefix;
            pN.Value = icon.Name; pT.Value = icon.Tags;
            cmdIcon.ExecuteNonQuery();
            fId.Value = icon.Id; fN.Value = icon.Name; fT.Value = icon.Tags;
            cmdFts.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ── 검색 ────────────────────────────────────────────────
    public List<IconEntry> Search(string query, IEnumerable<string>? prefixFilter, int limit = 100)
    {
        var result = new List<IconEntry>();
        using var cmd = _conn.CreateCommand();
        bool hasFts = !string.IsNullOrWhiteSpace(query);
        var prefixes = prefixFilter?.ToList();
        bool hasFilter = prefixes?.Count > 0;

        if (hasFts)
        {
            // FTS5 검색
            var safeTerm = query.Replace("\"", "\"\"");
            var sb = new System.Text.StringBuilder();
            sb.Append(@"
                SELECT i.id, i.prefix, i.name, i.tags, i.svg_cached
                FROM icons_fts f
                JOIN icons i ON i.id = f.id
                WHERE icons_fts MATCH $q");
            if (hasFilter)
            {
                var holders = prefixes!.Select((_, idx) => $"$pr{idx}");
                sb.Append($" AND i.prefix IN ({string.Join(",", holders)})");
            }
            sb.Append(" LIMIT $lim");
            cmd.CommandText = sb.ToString();
            cmd.Parameters.AddWithValue("$q", $"{safeTerm}*");
            if (hasFilter)
            {
                for (int i = 0; i < prefixes!.Count; i++)
                    cmd.Parameters.AddWithValue($"$pr{i}", prefixes[i]);
            }
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("SELECT id, prefix, name, tags, svg_cached FROM icons WHERE 1=1");
            if (hasFilter)
            {
                var holders = prefixes!.Select((_, idx) => $"$pr{idx}");
                sb.Append($" AND prefix IN ({string.Join(",", holders)})");
            }
            sb.Append(" ORDER BY name LIMIT $lim");
            cmd.CommandText = sb.ToString();
            if (hasFilter)
            {
                for (int i = 0; i < prefixes!.Count; i++)
                    cmd.Parameters.AddWithValue($"$pr{i}", prefixes[i]);
            }
        }
        cmd.Parameters.AddWithValue("$lim", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new IconEntry
            {
                Id = r.GetString(0), Prefix = r.GetString(1),
                Name = r.GetString(2), Tags = r.IsDBNull(3) ? "" : r.GetString(3)
            });
        }
        return result;
    }

    public int CountIcons(IEnumerable<string>? prefixFilter = null)
    {
        using var cmd = _conn.CreateCommand();
        var prefixes = prefixFilter?.ToList();
        if (prefixes?.Count > 0)
        {
            var holders = prefixes.Select((_, i) => $"$p{i}");
            cmd.CommandText = $"SELECT COUNT(*) FROM icons WHERE prefix IN ({string.Join(",", holders)})";
            for (int i = 0; i < prefixes.Count; i++)
                cmd.Parameters.AddWithValue($"$p{i}", prefixes[i]);
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM icons";
        }
        return (int)(long)cmd.ExecuteScalar()!;
    }

    // ── 즐겨찾기 ────────────────────────────────────────────
    public void SetFavorite(string id, bool value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = value
            ? "INSERT OR REPLACE INTO favorites(id) VALUES($id)"
            : "DELETE FROM favorites WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public HashSet<string> GetFavoriteIds()
    {
        var result = new HashSet<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM favorites ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    public List<IconEntry> GetFavorites(int limit = 200)
    {
        var result = new List<IconEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT i.id, i.prefix, i.name, i.tags
            FROM favorites f JOIN icons i ON i.id = f.id
            ORDER BY f.added_at DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(new IconEntry { Id = r.GetString(0), Prefix = r.GetString(1), Name = r.GetString(2), Tags = r.IsDBNull(3) ? "" : r.GetString(3), IsFavorite = true });
        return result;
    }

    // ── 최근 사용 ────────────────────────────────────────────
    public void AddRecent(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO recents(id, used_at) VALUES($id, datetime('now'))";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        // 최대 50개 유지
        cmd.CommandText = "DELETE FROM recents WHERE id NOT IN (SELECT id FROM recents ORDER BY used_at DESC LIMIT 50)";
        cmd.ExecuteNonQuery();
    }

    public List<IconEntry> GetRecents(int limit = 20)
    {
        var result = new List<IconEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT i.id, i.prefix, i.name, i.tags
            FROM recents rc JOIN icons i ON i.id = rc.id
            ORDER BY rc.used_at DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$lim", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(new IconEntry { Id = r.GetString(0), Prefix = r.GetString(1), Name = r.GetString(2), Tags = r.IsDBNull(3) ? "" : r.GetString(3) });
        return result;
    }

    public void Dispose() => _conn.Dispose();
}
