using System.Xml.Linq;
using ComicCast.Models;
using Microsoft.Data.Sqlite;

namespace ComicCast.Services;

/// <summary>SQLite 기반 만화 라이브러리 관리 (ComicInfo.xml 파싱 포함)</summary>
public class LibraryService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;
    private readonly ArchiveService _archive;

    public event Action<ComicBook>? BookAdded;
    public event Action<int>?       BookRemoved;

    public LibraryService(ArchiveService archive)
    {
        _archive = archive;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ComicCast");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "library.db");
    }

    public void Initialize()
    {
        _conn = new SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();
        CreateSchema();
    }

    // ── 폴더 스캔 ────────────────────────────────────────────────────────────

    public async Task ScanFolderAsync(string folder, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var exts = new HashSet<string>([".cbz", ".cbr", ".cb7", ".cbt", ".zip", ".rar", ".7z", ".tar"],
            StringComparer.OrdinalIgnoreCase);

        // 폴더도 만화책으로 (내부에 이미지만 있는 경우)
        var paths = new List<string>();
        paths.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f))));

        // 이미지만 있는 하위 폴더
        foreach (var dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
        {
            if (ct.IsCancellationRequested) break;
            if (Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                    .Any(f => IsImage(f)) &&
                !Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                    .Any(f => exts.Contains(Path.GetExtension(f))))
                paths.Add(dir);
        }

        foreach (var path in paths)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report(Path.GetFileName(path));
            await Task.Run(() => AddBookIfNew(path), ct);
        }
    }

    public List<ComicBook> GetAllBooks() => QueryBooks("ORDER BY Series, Volume, Number, Title");

    public List<ComicBook> SearchBooks(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query)) return GetAllBooks();
        return QueryBooks("WHERE Title LIKE @q OR Series LIKE @q ORDER BY Series, Title",
            new SqliteParameter("@q", $"%{query}%"));
    }

    public void UpdateProgress(int bookId, int lastPage)
    {
        Execute("UPDATE Books SET LastPage=@lp, LastReadAt=@lr WHERE Id=@id",
            new SqliteParameter("@lp", lastPage),
            new SqliteParameter("@lr", DateTime.UtcNow.ToString("o")),
            new SqliteParameter("@id", bookId));
    }

    public void RemoveBook(int bookId)
    {
        Execute("DELETE FROM Books WHERE Id=@id", new SqliteParameter("@id", bookId));
        BookRemoved?.Invoke(bookId);
    }

    public List<LibraryFolder> GetFolders()
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT Id, Path, Label, AddedAt FROM Folders ORDER BY Label";
        var result = new List<LibraryFolder>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(new LibraryFolder
            {
                Id      = r.GetInt32(0),
                Path    = r.GetString(1),
                Label   = r.GetString(2),
                AddedAt = DateTime.Parse(r.GetString(3)),
            });
        return result;
    }

    public void AddFolder(string path)
    {
        var label = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path;
        Execute("INSERT OR IGNORE INTO Folders(Path, Label, AddedAt) VALUES(@p,@l,@a)",
            new SqliteParameter("@p", path),
            new SqliteParameter("@l", label),
            new SqliteParameter("@a", DateTime.UtcNow.ToString("o")));
    }

    public void RemoveFolder(int folderId)
        => Execute("DELETE FROM Folders WHERE Id=@id", new SqliteParameter("@id", folderId));

    // ── ComicInfo.xml 파싱 ────────────────────────────────────────────────────

    private ComicBook ParseComicInfo(string archivePath)
    {
        var book = new ComicBook
        {
            FilePath    = archivePath,
            Title       = Path.GetFileNameWithoutExtension(archivePath),
            ArchiveType = ArchiveService.DetectType(archivePath),
        };

        try
        {
            var pages = _archive.GetPages(archivePath);
            book.PageCount = pages.Count;

            // ComicInfo.xml 찾기
            Stream? xmlStream = null;
            if (book.ArchiveType == Models.ArchiveType.Folder)
            {
                var xmlPath = Path.Combine(archivePath, "ComicInfo.xml");
                if (File.Exists(xmlPath)) xmlStream = File.OpenRead(xmlPath);
            }
            else
            {
                var xmlPage = pages.FirstOrDefault(p =>
                    p.Name.Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));
                if (xmlPage is not null)
                    xmlStream = _archive.OpenPage(archivePath, xmlPage);
            }

            if (xmlStream is not null)
            {
                using (xmlStream)
                {
                    var doc = XDocument.Load(xmlStream);
                    var ns  = doc.Root?.Name.Namespace ?? XNamespace.None;
                    book.Title   = doc.Root?.Element(ns + "Title")?.Value   ?? book.Title;
                    book.Series  = doc.Root?.Element(ns + "Series")?.Value  ?? "";
                    book.Writer  = doc.Root?.Element(ns + "Writer")?.Value  ?? "";
                    book.Summary = doc.Root?.Element(ns + "Summary")?.Value ?? "";
                    int.TryParse(doc.Root?.Element(ns + "Volume")?.Value, out int vol);
                    int.TryParse(doc.Root?.Element(ns + "Number")?.Value, out int num);
                    book.Volume = vol;
                    book.Number = num;
                }
            }
        }
        catch { /* 파싱 실패 시 기본값 사용 */ }

        return book;
    }

    private void AddBookIfNew(string path)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Books WHERE FilePath=@p";
        cmd.Parameters.AddWithValue("@p", path);
        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return;

        var book = ParseComicInfo(path);
        InsertBook(book);
        BookAdded?.Invoke(book);
    }

    private void InsertBook(ComicBook b)
    {
        Execute(@"INSERT INTO Books
            (FilePath,Title,Series,Writer,Summary,Volume,Number,PageCount,LastPage,AddedAt,ArchiveType)
            VALUES(@fp,@ti,@se,@wr,@su,@vo,@nu,@pc,@lp,@ad,@at)",
            new SqliteParameter("@fp", b.FilePath),
            new SqliteParameter("@ti", b.Title),
            new SqliteParameter("@se", b.Series),
            new SqliteParameter("@wr", b.Writer),
            new SqliteParameter("@su", b.Summary),
            new SqliteParameter("@vo", b.Volume),
            new SqliteParameter("@nu", b.Number),
            new SqliteParameter("@pc", b.PageCount),
            new SqliteParameter("@lp", b.LastPage),
            new SqliteParameter("@ad", b.AddedAt.ToString("o")),
            new SqliteParameter("@at", (int)b.ArchiveType));

        // 방금 넣은 Id 갱신
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        b.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    private List<ComicBook> QueryBooks(string clause, params SqliteParameter[] parms)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = $"SELECT Id,FilePath,Title,Series,Writer,Summary,Volume,Number,PageCount,LastPage,AddedAt,LastReadAt,ArchiveType FROM Books {clause}";
        foreach (var p in parms) cmd.Parameters.Add(p);
        var list = new List<ComicBook>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ComicBook
            {
                Id          = r.GetInt32(0),
                FilePath    = r.GetString(1),
                Title       = r.GetString(2),
                Series      = r.IsDBNull(3) ? "" : r.GetString(3),
                Writer      = r.IsDBNull(4) ? "" : r.GetString(4),
                Summary     = r.IsDBNull(5) ? "" : r.GetString(5),
                Volume      = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                Number      = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                PageCount   = r.GetInt32(8),
                LastPage    = r.GetInt32(9),
                AddedAt     = DateTime.Parse(r.GetString(10)),
                LastReadAt  = r.IsDBNull(11) ? null : DateTime.Parse(r.GetString(11)),
                ArchiveType = (Models.ArchiveType)r.GetInt32(12),
            });
        return list;
    }

    private void Execute(string sql, params SqliteParameter[] parms)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parms) cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }

    private void CreateSchema()
    {
        Execute(@"CREATE TABLE IF NOT EXISTS Books (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            FilePath    TEXT NOT NULL UNIQUE,
            Title       TEXT NOT NULL,
            Series      TEXT,
            Writer      TEXT,
            Summary     TEXT,
            Volume      INTEGER DEFAULT 0,
            Number      INTEGER DEFAULT 0,
            PageCount   INTEGER DEFAULT 0,
            LastPage    INTEGER DEFAULT 0,
            AddedAt     TEXT NOT NULL,
            LastReadAt  TEXT,
            ArchiveType INTEGER DEFAULT 0
        )");
        Execute(@"CREATE TABLE IF NOT EXISTS Folders (
            Id      INTEGER PRIMARY KEY AUTOINCREMENT,
            Path    TEXT NOT NULL UNIQUE,
            Label   TEXT NOT NULL,
            AddedAt TEXT NOT NULL
        )");
    }

    private static bool IsImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp";
    }

    public void Dispose() => _conn?.Dispose();
}
