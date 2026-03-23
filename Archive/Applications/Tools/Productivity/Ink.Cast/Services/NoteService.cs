using System.Text.RegularExpressions;
using InkCast.Models;
using Microsoft.Data.Sqlite;

namespace InkCast.Services;

/// <summary>노트 CRUD / 태그·링크 파싱 / FTS5 검색</summary>
public class NoteService
{
    private readonly DatabaseService _db;

    private static readonly Regex TagRegex  = new(@"(?<![#\w])#(\w+)", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new(@"\[\[([^\[\]\n]+)\]\]", RegexOptions.Compiled);

    public NoteService(DatabaseService db) => _db = db;

    // ── 워크스페이스 ────────────────────────────────────────────

    public async Task<int> EnsureDefaultWorkspaceAsync()
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM workspaces ORDER BY id LIMIT 1";
        var result = await cmd.ExecuteScalarAsync();
        if (result is long id) return (int)id;

        cmd.CommandText = "INSERT INTO workspaces(name) VALUES('기본 워크스페이스'); SELECT last_insert_rowid();";
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<Workspace>> GetWorkspacesAsync()
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_at, last_opened_at FROM workspaces ORDER BY last_opened_at DESC";
        var list = new List<Workspace>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Workspace
            {
                Id            = r.GetInt32(0),
                Name          = r.GetString(1),
                CreatedAt     = DateTime.Parse(r.GetString(2)),
                LastOpenedAt  = DateTime.Parse(r.GetString(3))
            });
        return list;
    }

    public async Task<int> CreateWorkspaceAsync(string name)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO workspaces(name) VALUES($name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateWorkspaceLastOpenedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE workspaces SET last_opened_at=datetime('now','localtime') WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteWorkspaceAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM workspaces WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 노트 CRUD ────────────────────────────────────────────────

    public async Task<List<Note>> GetAllAsync(int workspaceId)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.id, n.title, n.content, n.created_at, n.updated_at,
                   GROUP_CONCAT(t.name, ',') AS tags
            FROM notes n
            LEFT JOIN note_tags nt ON n.id = nt.note_id
            LEFT JOIN tags t       ON nt.tag_id = t.id
            WHERE n.workspace_id = $wid
            GROUP BY n.id
            ORDER BY n.updated_at DESC
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        return await ReadNotesAsync(cmd, workspaceId);
    }

    public async Task<Note?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.id, n.title, n.content, n.created_at, n.updated_at,
                   GROUP_CONCAT(t.name, ',') AS tags
            FROM notes n
            LEFT JOIN note_tags nt ON n.id = nt.note_id
            LEFT JOIN tags t       ON nt.tag_id = t.id
            WHERE n.id = $id
            GROUP BY n.id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        var list = await ReadNotesAsync(cmd, 0);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<Note?> GetByTitleAsync(int workspaceId, string title)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM notes WHERE workspace_id=$wid AND title=$title LIMIT 1";
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$title", title);
        var result = await cmd.ExecuteScalarAsync();
        if (result is null) return null;
        return await GetByIdAsync((int)(long)result);
    }

    public async Task<int> CreateAsync(Note note)
    {
        using var conn = _db.CreateConnection();
        using var tx   = conn.BeginTransaction();
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO notes(workspace_id, title, content)
            VALUES($wid, $title, $content);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$wid",     note.WorkspaceId);
        cmd.Parameters.AddWithValue("$title",   note.Title);
        cmd.Parameters.AddWithValue("$content", note.Content);
        note.Id = (int)(long)(await cmd.ExecuteScalarAsync())!;

        await SaveTagsAsync(conn, tx, note);
        await SaveLinksAsync(conn, tx, note);
        tx.Commit();
        return note.Id;
    }

    public async Task UpdateAsync(Note note)
    {
        using var conn = _db.CreateConnection();
        using var tx   = conn.BeginTransaction();
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE notes
            SET title=$title, content=$content, updated_at=datetime('now','localtime')
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$title",   note.Title);
        cmd.Parameters.AddWithValue("$content", note.Content);
        cmd.Parameters.AddWithValue("$id",      note.Id);
        await cmd.ExecuteNonQueryAsync();

        cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM note_tags  WHERE note_id=$id; DELETE FROM note_links WHERE source_id=$id;";
        cmd.Parameters.AddWithValue("$id", note.Id);
        await cmd.ExecuteNonQueryAsync();

        await SaveTagsAsync(conn, tx, note);
        await SaveLinksAsync(conn, tx, note);
        tx.Commit();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 검색 ─────────────────────────────────────────────────────

    public async Task<List<Note>> SearchAsync(int workspaceId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(workspaceId);

        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        // FTS5 prefix 퍼지 검색
        var ftsQuery = string.Join(" OR ", query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => $"\"{w.Replace("\"", "")}\"*"));

        cmd.CommandText = """
            SELECT n.id, n.title, n.content, n.created_at, n.updated_at,
                   GROUP_CONCAT(t.name, ',') AS tags
            FROM notes_fts f
            JOIN  notes n   ON f.rowid = n.id
            LEFT JOIN note_tags nt ON n.id = nt.note_id
            LEFT JOIN tags t       ON nt.tag_id = t.id
            WHERE n.workspace_id=$wid AND notes_fts MATCH $q
            GROUP BY n.id
            ORDER BY rank
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$q",   ftsQuery);

        try   { return await ReadNotesAsync(cmd, workspaceId); }
        catch { return []; }
    }

    public async Task<List<Note>> GetByTagAsync(int workspaceId, string tagName)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT n.id, n.title, n.content, n.created_at, n.updated_at,
                   GROUP_CONCAT(t2.name, ',') AS tags
            FROM notes n
            JOIN  note_tags nt  ON n.id = nt.note_id
            JOIN  tags t        ON nt.tag_id = t.id AND t.name=$tag AND t.workspace_id=$wid
            LEFT JOIN note_tags nt2 ON n.id = nt2.note_id
            LEFT JOIN tags t2       ON nt2.tag_id = t2.id
            WHERE n.workspace_id=$wid
            GROUP BY n.id
            ORDER BY n.updated_at DESC
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$tag", tagName);
        return await ReadNotesAsync(cmd, workspaceId);
    }

    // ── 태그 ─────────────────────────────────────────────────────

    public async Task<List<(string Name, int Count)>> GetAllTagsAsync(int workspaceId)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.name, COUNT(nt.note_id) AS cnt
            FROM tags t
            LEFT JOIN note_tags nt ON t.id = nt.tag_id
            WHERE t.workspace_id=$wid
            GROUP BY t.id
            ORDER BY cnt DESC, t.name
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        var list = new List<(string, int)>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetInt32(1)));
        return list;
    }

    // ── 백링크 / 그래프 ──────────────────────────────────────────

    public async Task<List<Note>> GetBacklinksAsync(int workspaceId, string noteTitle)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT n.id, n.title, n.content, n.created_at, n.updated_at
            FROM note_links nl
            JOIN notes n ON nl.source_id = n.id
            WHERE nl.target_title=$title AND n.workspace_id=$wid
            ORDER BY n.updated_at DESC
            """;
        cmd.Parameters.AddWithValue("$title", noteTitle);
        cmd.Parameters.AddWithValue("$wid",   workspaceId);
        return await ReadNotesAsync(cmd, workspaceId);
    }

    public async Task<List<(int SourceId, string SourceTitle, string TargetTitle)>> GetAllLinksAsync(int workspaceId)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT nl.source_id, n.title, nl.target_title
            FROM note_links nl
            JOIN notes n ON nl.source_id = n.id
            WHERE n.workspace_id=$wid
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        var list = new List<(int, string, string)>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
        return list;
    }

    // ── 자동완성용 제목 목록 ─────────────────────────────────────

    public async Task<List<string>> GetAllTitlesAsync(int workspaceId)
    {
        using var conn = _db.CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT title FROM notes WHERE workspace_id=$wid ORDER BY title";
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        var list = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    // ── 정적 헬퍼 ───────────────────────────────────────────────

    public static List<string> ExtractTags(string content)
        => [.. TagRegex.Matches(content).Select(m => m.Groups[1].Value.ToLower()).Distinct()];

    public static List<string> ExtractLinks(string content)
        => [.. LinkRegex.Matches(content).Select(m => m.Groups[1].Value.Trim()).Distinct()];

    // ── Private ──────────────────────────────────────────────────

    private static async Task<List<Note>> ReadNotesAsync(SqliteCommand cmd, int workspaceId)
    {
        var list = new List<Note>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var note = new Note
            {
                Id          = r.GetInt32(0),
                WorkspaceId = workspaceId,
                Title       = r.GetString(1),
                Content     = r.GetString(2),
                CreatedAt   = DateTime.Parse(r.GetString(3)),
                UpdatedAt   = DateTime.Parse(r.GetString(4)),
            };
            if (!r.IsDBNull(5))
                note.Tags = [.. r.GetString(5).Split(',', StringSplitOptions.RemoveEmptyEntries)];
            list.Add(note);
        }
        return list;
    }

    private async Task SaveTagsAsync(SqliteConnection conn, SqliteTransaction tx, Note note)
    {
        var tags = ExtractTags(note.Content);
        note.Tags = tags;
        foreach (var tag in tags)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO tags(workspace_id, name) VALUES($wid, $name);
                SELECT id FROM tags WHERE workspace_id=$wid AND name=$name;
                """;
            cmd.Parameters.AddWithValue("$wid",  note.WorkspaceId);
            cmd.Parameters.AddWithValue("$name", tag);
            var tagId = (long)(await cmd.ExecuteScalarAsync())!;

            cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO note_tags(note_id, tag_id) VALUES($nid, $tid)";
            cmd.Parameters.AddWithValue("$nid", note.Id);
            cmd.Parameters.AddWithValue("$tid", tagId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task SaveLinksAsync(SqliteConnection conn, SqliteTransaction tx, Note note)
    {
        foreach (var link in ExtractLinks(note.Content))
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO note_links(source_id, target_title) VALUES($sid, $title)";
            cmd.Parameters.AddWithValue("$sid",   note.Id);
            cmd.Parameters.AddWithValue("$title", link);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
