namespace Prompt.Forge.Services;

sealed class Database : IDisposable
{
    readonly SqliteConnection _conn;

    public Database(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        Migrate();
    }

    void Migrate()
    {
        Execute(@"
            CREATE TABLE IF NOT EXISTS prompts (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                title       TEXT    NOT NULL DEFAULT '',
                content     TEXT    NOT NULL DEFAULT '',
                tags        TEXT    NOT NULL DEFAULT '',
                service     TEXT    NOT NULL DEFAULT '',
                is_favorite INTEGER NOT NULL DEFAULT 0,
                version     INTEGER NOT NULL DEFAULT 1,
                notes       TEXT    NOT NULL DEFAULT '',
                parent_id   INTEGER,
                created_at  TEXT    NOT NULL DEFAULT (datetime('now')),
                updated_at  TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS prompts_fts
                USING fts5(title, content, tags, notes, content=prompts, content_rowid=id);

            CREATE TRIGGER IF NOT EXISTS prompts_ai AFTER INSERT ON prompts BEGIN
                INSERT INTO prompts_fts(rowid, title, content, tags, notes)
                VALUES (new.id, new.title, new.content, new.tags, new.notes);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_au AFTER UPDATE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes);
                INSERT INTO prompts_fts(rowid, title, content, tags, notes)
                VALUES (new.id, new.title, new.content, new.tags, new.notes);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_ad AFTER DELETE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes);
            END;
        ");
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public List<PromptItem> Search(string query, string? tag, string? service, bool? favOnly)
    {
        var conditions = new List<string>();
        var cmd = _conn.CreateCommand();

        string baseTable;
        if (!string.IsNullOrWhiteSpace(query))
        {
            cmd.Parameters.AddWithValue("$q", query + "*");
            baseTable = @"
                SELECT p.* FROM prompts p
                INNER JOIN prompts_fts fts ON p.id = fts.rowid
                WHERE fts.prompts_fts MATCH $q AND p.parent_id IS NULL";
        }
        else
        {
            baseTable = "SELECT p.* FROM prompts p WHERE p.parent_id IS NULL";
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            conditions.Add("p.tags LIKE $tag");
            cmd.Parameters.AddWithValue("$tag", $"%{tag}%");
        }
        if (!string.IsNullOrWhiteSpace(service))
        {
            conditions.Add("p.service = $svc");
            cmd.Parameters.AddWithValue("$svc", service);
        }
        if (favOnly == true)
            conditions.Add("p.is_favorite = 1");

        string where = conditions.Count > 0
            ? " AND " + string.Join(" AND ", conditions)
            : "";

        cmd.CommandText = baseTable + where + " ORDER BY p.is_favorite DESC, p.updated_at DESC";

        return ReadItems(cmd);
    }

    public PromptItem? GetById(int id)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return ReadItems(cmd).FirstOrDefault();
    }

    public List<PromptItem> GetVersionHistory(int rootId)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts WHERE parent_id = $pid ORDER BY version";
        cmd.Parameters.AddWithValue("$pid", rootId);
        return ReadItems(cmd);
    }

    public int Insert(PromptItem p)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO prompts (title, content, tags, service, is_favorite, version, notes, parent_id)
            VALUES ($title, $content, $tags, $svc, $fav, $ver, $notes, $pid);
            SELECT last_insert_rowid();";
        BindParams(cmd, p);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Update(PromptItem p)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE prompts SET
                title = $title, content = $content, tags = $tags,
                service = $svc, is_favorite = $fav, version = $ver,
                notes = $notes, updated_at = datetime('now')
            WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", p.Id);
        BindParams(cmd, p);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM prompts WHERE id = $id OR parent_id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void ToggleFavorite(int id)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE prompts SET is_favorite = NOT is_favorite, updated_at = datetime('now') WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<PromptItem> GetAll()
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts WHERE parent_id IS NULL ORDER BY is_favorite DESC, updated_at DESC";
        return ReadItems(cmd);
    }

    public List<string> GetAllTags()
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT tags FROM prompts WHERE parent_id IS NULL AND tags != ''";
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            foreach (var t in reader.GetString(0).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                tags.Add(t);
        return [.. tags.OrderBy(t => t)];
    }

    public List<string> GetAllServices()
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT service FROM prompts WHERE parent_id IS NULL AND service != '' ORDER BY service";
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    static void BindParams(SqliteCommand cmd, PromptItem p)
    {
        cmd.Parameters.AddWithValue("$title",   p.Title);
        cmd.Parameters.AddWithValue("$content", p.Content);
        cmd.Parameters.AddWithValue("$tags",    p.Tags);
        cmd.Parameters.AddWithValue("$svc",     p.Service);
        cmd.Parameters.AddWithValue("$fav",     p.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$ver",     p.Version);
        cmd.Parameters.AddWithValue("$notes",   p.Notes);
        cmd.Parameters.AddWithValue("$pid",     p.ParentId.HasValue ? (object)p.ParentId.Value : DBNull.Value);
    }

    static List<PromptItem> ReadItems(SqliteCommand cmd)
    {
        var list = new List<PromptItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PromptItem
            {
                Id         = r.GetInt32(r.GetOrdinal("id")),
                Title      = r.GetString(r.GetOrdinal("title")),
                Content    = r.GetString(r.GetOrdinal("content")),
                Tags       = r.GetString(r.GetOrdinal("tags")),
                Service    = r.GetString(r.GetOrdinal("service")),
                IsFavorite = r.GetInt32(r.GetOrdinal("is_favorite")) == 1,
                Version    = r.GetInt32(r.GetOrdinal("version")),
                Notes      = r.GetString(r.GetOrdinal("notes")),
                ParentId   = r.IsDBNull(r.GetOrdinal("parent_id")) ? null : r.GetInt32(r.GetOrdinal("parent_id")),
                CreatedAt  = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
                UpdatedAt  = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at")))
            });
        }
        return list;
    }

    void Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
