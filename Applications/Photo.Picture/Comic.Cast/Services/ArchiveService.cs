using System.IO.Compression;
using ComicCast.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using ComicArchiveType = ComicCast.Models.ArchiveType;

namespace ComicCast.Services;

/// <summary>CBZ/CBR/CB7/CBT/폴더에서 페이지 목록 로드 및 스트림 제공</summary>
public class ArchiveService
{
    private static readonly HashSet<string> ImageExts =
        [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".avif"];

    /// <summary>아카이브 형식 감지</summary>
    public static ComicArchiveType DetectType(string path)
    {
        if (Directory.Exists(path)) return ComicArchiveType.Folder;
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cbz" or ".zip" => ComicArchiveType.Cbz,
            ".cbr" or ".rar" => ComicArchiveType.Cbr,
            ".cb7" or ".7z"  => ComicArchiveType.Cb7,
            ".cbt" or ".tar" => ComicArchiveType.Cbt,
            _                => ComicArchiveType.Cbz,
        };
    }

    /// <summary>아카이브에서 이미지 페이지 목록 추출 (이름순 정렬)</summary>
    public List<ComicPage> GetPages(string path)
    {
        var type = DetectType(path);
        return type switch
        {
            ComicArchiveType.Folder => GetFolderPages(path),
            ComicArchiveType.Cbz    => GetZipPages(path),
            _                  => GetSharpCompressPages(path),
        };
    }

    /// <summary>특정 페이지 이미지 스트림 반환 (호출자가 Dispose 책임)</summary>
    public Stream OpenPage(string archivePath, ComicPage page)
    {
        var type = DetectType(archivePath);
        return type switch
        {
            ComicArchiveType.Folder => File.OpenRead(page.EntryKey),
            ComicArchiveType.Cbz    => OpenZipEntry(archivePath, page.EntryKey),
            _                  => OpenSharpEntry(archivePath, page.EntryKey),
        };
    }

    /// <summary>페이지 이미지를 BitmapImage로 로드 (캐시 옵션 포함)</summary>
    public BitmapImage LoadBitmap(string archivePath, ComicPage page)
    {
        using var stream = OpenPage(archivePath, page);
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>비동기 BitmapImage 로드</summary>
    public async Task<BitmapImage> LoadBitmapAsync(string archivePath, ComicPage page,
        CancellationToken ct = default)
    {
        // Task.Run으로 IO를 스레드 풀에서 실행하고, await 완료 후 캡처된 동기화 컨텍스트(Dispatcher)로 복귀
        // stream.CopyToAsync 내부의 ConfigureAwait(false)가 동기화 컨텍스트를 파괴하는 문제 방지
        var ms = await Task.Run(() =>
        {
            using var stream = OpenPage(archivePath, page);
            var mem = new MemoryStream();
            stream.CopyTo(mem);
            mem.Position = 0;
            return mem;
        }, ct);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static List<ComicPage> GetFolderPages(string folder)
    {
        return Directory.EnumerateFiles(folder)
            .Where(f => ImageExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select((f, i) => new ComicPage
            {
                Index    = i,
                Name     = Path.GetFileName(f),
                EntryKey = f,
                Size     = new FileInfo(f).Length,
            })
            .ToList();
    }

    private static List<ComicPage> GetZipPages(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries
            .Where(e => ImageExts.Contains(Path.GetExtension(e.Name).ToLowerInvariant()))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .Select((e, i) => new ComicPage
            {
                Index    = i,
                Name     = e.Name,
                EntryKey = e.FullName,
                Size     = e.Length,
            })
            .ToList();
    }

    private static List<ComicPage> GetSharpCompressPages(string archivePath)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        return archive.Entries
            .Where(e => !e.IsDirectory &&
                        ImageExts.Contains(Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select((e, i) => new ComicPage
            {
                Index    = i,
                Name     = Path.GetFileName(e.Key ?? ""),
                EntryKey = e.Key ?? "",
                Size     = e.Size,
            })
            .ToList();
    }

    private static Stream OpenZipEntry(string zipPath, string entryKey)
    {
        var zip   = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(entryKey)
                    ?? throw new FileNotFoundException($"ZIP 항목을 찾을 수 없음: {entryKey}");
        var ms = new MemoryStream();
        using var es = entry.Open();
        es.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        zip.Dispose();
        return ms;
    }

    private static Stream OpenSharpEntry(string archivePath, string entryKey)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        var entry = archive.Entries
            .FirstOrDefault(e => e.Key == entryKey)
            ?? throw new FileNotFoundException($"아카이브 항목을 찾을 수 없음: {entryKey}");
        var ms = new MemoryStream();
        entry.WriteTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
