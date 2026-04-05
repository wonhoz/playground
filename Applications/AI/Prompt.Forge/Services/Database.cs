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
        EnsureMigrations();
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
                use_count   INTEGER NOT NULL DEFAULT 0,
                created_at  TEXT    NOT NULL DEFAULT (datetime('now')),
                updated_at  TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS prompts_fts
                USING fts5(title, content, tags, notes, service, content=prompts, content_rowid=id);

            CREATE TRIGGER IF NOT EXISTS prompts_ai AFTER INSERT ON prompts BEGIN
                INSERT INTO prompts_fts(rowid, title, content, tags, notes, service)
                VALUES (new.id, new.title, new.content, new.tags, new.notes, new.service);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_au AFTER UPDATE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes, service)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes, old.service);
                INSERT INTO prompts_fts(rowid, title, content, tags, notes, service)
                VALUES (new.id, new.title, new.content, new.tags, new.notes, new.service);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_ad AFTER DELETE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes, service)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes, old.service);
            END;
        ");
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    /// 기존 DB 호환성을 위한 컬럼 추가 마이그레이션 (실패 시 이미 존재하는 것으로 간주)
    void EnsureMigrations()
    {
        try { Execute("ALTER TABLE prompts ADD COLUMN use_count INTEGER NOT NULL DEFAULT 0;"); }
        catch { }

        bool sortOrderAdded = false;
        try
        {
            Execute("ALTER TABLE prompts ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;");
            sortOrderAdded = true;
        }
        catch { }

        if (sortOrderAdded)
            InitializeSortOrders();

        // FTS5 인덱스에 service 컬럼 추가 — 기존 FTS에 service가 없으면 재생성
        EnsureFtsServiceColumn();
    }

    void EnsureFtsServiceColumn()
    {
        // FTS 테이블 컬럼 목록 조회: service가 없으면 재생성 필요
        bool hasService;
        try
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='prompts_fts'";
            var sql = cmd.ExecuteScalar() as string ?? "";
            hasService = sql.Contains("service", StringComparison.OrdinalIgnoreCase);
        }
        catch { return; }

        if (hasService) return;

        // 기존 FTS 트리거·인덱스 제거 후 service 포함하여 재생성
        Execute("DROP TRIGGER IF EXISTS prompts_ai");
        Execute("DROP TRIGGER IF EXISTS prompts_au");
        Execute("DROP TRIGGER IF EXISTS prompts_ad");
        Execute("DROP TABLE IF EXISTS prompts_fts");

        Execute(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS prompts_fts
                USING fts5(title, content, tags, notes, service, content=prompts, content_rowid=id);

            CREATE TRIGGER IF NOT EXISTS prompts_ai AFTER INSERT ON prompts BEGIN
                INSERT INTO prompts_fts(rowid, title, content, tags, notes, service)
                VALUES (new.id, new.title, new.content, new.tags, new.notes, new.service);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_au AFTER UPDATE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes, service)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes, old.service);
                INSERT INTO prompts_fts(rowid, title, content, tags, notes, service)
                VALUES (new.id, new.title, new.content, new.tags, new.notes, new.service);
            END;

            CREATE TRIGGER IF NOT EXISTS prompts_ad AFTER DELETE ON prompts BEGIN
                INSERT INTO prompts_fts(prompts_fts, rowid, title, content, tags, notes, service)
                VALUES ('delete', old.id, old.title, old.content, old.tags, old.notes, old.service);
            END;
        ");

        // 기존 데이터 재인덱싱
        Execute("INSERT INTO prompts_fts(prompts_fts) VALUES('rebuild')");
    }

    void InitializeSortOrders()
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM prompts WHERE parent_id IS NULL ORDER BY is_favorite DESC, updated_at DESC";
        var ids = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt32(0));

        UpdateSortOrders(ids.Select((id, i) => (id, i)));
    }

    public void UpdateSortOrders(IEnumerable<(int id, int order)> orders)
    {
        using var tx = _conn.BeginTransaction();
        foreach (var (id, order) in orders)
        {
            var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE prompts SET sort_order = $order WHERE id = $id";
            cmd.Parameters.AddWithValue("$order", order);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void IncrementUseCount(int id)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE prompts SET use_count = use_count + 1 WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<PromptItem> Search(string query, string? tag, string? service, bool? favOnly,
                                   string sortOrder = "updated")
    {
        var conditions = new List<string>();
        var cmd = _conn.CreateCommand();

        string baseTable;
        if (!string.IsNullOrWhiteSpace(query))
        {
            // FTS5 특수 연산자(AND, OR, NOT, ", (, ))가 포함된 경우 구문 오류 방지:
            // 각 토큰에 " "로 감싸 리터럴 문자열로 처리
            var safeQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => "\"" + t.Replace("\"", "\"\"") + "\"*"));
            cmd.Parameters.AddWithValue("$q", safeQuery);
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
            conditions.Add("(',' || p.tags || ',') LIKE $tag");
            cmd.Parameters.AddWithValue("$tag", $"%,{tag},%");
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

        var order = sortOrder switch
        {
            "use_count" => "p.use_count DESC, p.is_favorite DESC, p.updated_at DESC",
            "custom"    => "p.sort_order ASC",
            _           => "p.is_favorite DESC, p.updated_at DESC"
        };
        cmd.CommandText = baseTable + where + $" ORDER BY {order}";

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
            INSERT INTO prompts (title, content, tags, service, is_favorite, version, notes, parent_id, sort_order, use_count)
            VALUES ($title, $content, $tags, $svc, $fav, $ver, $notes, $pid,
                    CASE WHEN $pid IS NULL
                         THEN (SELECT COALESCE(MAX(sort_order), -1) + 1 FROM prompts WHERE parent_id IS NULL)
                         ELSE 0 END,
                    $use_count);
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

    public void DeleteHistoryItem(int historyId)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM prompts WHERE id = $id AND parent_id IS NOT NULL";
        cmd.Parameters.AddWithValue("$id", historyId);
        cmd.ExecuteNonQuery();
    }

    public void ToggleFavorite(int id)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE prompts SET is_favorite = NOT is_favorite WHERE id = $id";
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
        cmd.Parameters.AddWithValue("$title",     p.Title);
        cmd.Parameters.AddWithValue("$content",   p.Content);
        cmd.Parameters.AddWithValue("$tags",      p.Tags);
        cmd.Parameters.AddWithValue("$svc",       p.Service);
        cmd.Parameters.AddWithValue("$fav",       p.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$ver",       p.Version);
        cmd.Parameters.AddWithValue("$notes",     p.Notes);
        cmd.Parameters.AddWithValue("$pid",       p.ParentId.HasValue ? (object)p.ParentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$use_count", p.UseCount);
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
                UseCount   = r.IsDBNull(r.GetOrdinal("use_count")) ? 0 : r.GetInt32(r.GetOrdinal("use_count")),
                ParentId   = r.IsDBNull(r.GetOrdinal("parent_id")) ? null : r.GetInt32(r.GetOrdinal("parent_id")),
                SortOrder  = r.IsDBNull(r.GetOrdinal("sort_order")) ? 0 : r.GetInt32(r.GetOrdinal("sort_order")),
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
