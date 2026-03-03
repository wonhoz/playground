using System.IO;
using Microsoft.Data.Sqlite;

namespace InkCast.Services;

/// <summary>SQLite 데이터베이스 초기화 및 연결 관리</summary>
public class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "InkCast");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "inkcast.db");
        Initialize();
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    private void Initialize()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS workspaces (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT    NOT NULL,
                created_at    TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                last_opened_at TEXT   NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS notes (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                workspace_id INTEGER NOT NULL REFERENCES workspaces(id),
                title        TEXT    NOT NULL,
                content      TEXT    NOT NULL DEFAULT '',
                created_at   TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                updated_at   TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_notes_ws_title
                ON notes(workspace_id, title);

            CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                title, content,
                content=notes, content_rowid=id,
                tokenize='unicode61 remove_diacritics 2'
            );

            CREATE TABLE IF NOT EXISTS tags (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                workspace_id INTEGER NOT NULL REFERENCES workspaces(id),
                name         TEXT    NOT NULL,
                UNIQUE(workspace_id, name)
            );

            CREATE TABLE IF NOT EXISTS note_tags (
                note_id INTEGER REFERENCES notes(id) ON DELETE CASCADE,
                tag_id  INTEGER REFERENCES tags(id)  ON DELETE CASCADE,
                PRIMARY KEY (note_id, tag_id)
            );

            CREATE TABLE IF NOT EXISTS note_links (
                source_id    INTEGER REFERENCES notes(id) ON DELETE CASCADE,
                target_title TEXT    NOT NULL,
                PRIMARY KEY (source_id, target_title)
            );

            -- FTS5 동기화 트리거
            CREATE TRIGGER IF NOT EXISTS notes_fts_insert
                AFTER INSERT ON notes BEGIN
                    INSERT INTO notes_fts(rowid, title, content)
                    VALUES (new.id, new.title, new.content);
                END;

            CREATE TRIGGER IF NOT EXISTS notes_fts_delete
                AFTER DELETE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, title, content)
                    VALUES ('delete', old.id, old.title, old.content);
                END;

            CREATE TRIGGER IF NOT EXISTS notes_fts_update
                AFTER UPDATE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, title, content)
                    VALUES ('delete', old.id, old.title, old.content);
                    INSERT INTO notes_fts(rowid, title, content)
                    VALUES (new.id, new.title, new.content);
                END;
            """;
        cmd.ExecuteNonQuery();
    }
}
