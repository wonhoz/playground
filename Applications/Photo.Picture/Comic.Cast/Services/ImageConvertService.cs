using ComicCast.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ISImage     = SixLabors.ImageSharp.Image;
using ISResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using ISSize      = SixLabors.ImageSharp.Size;

namespace ComicCast.Services;

/// <summary>만화책 이미지 일괄 WebP 변환</summary>
public class ImageConvertService
{
    private readonly ArchiveService _archive;

    public ImageConvertService(ArchiveService archive) => _archive = archive;

    /// <summary>아카이브 → 새 CBZ(WebP 변환) 생성</summary>
    public async Task ConvertToWebpCbzAsync(
        ComicBook book,
        int quality = 85,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var pages   = _archive.GetPages(book.FilePath);
        var outPath = Path.ChangeExtension(book.FilePath, ".webp.cbz");
        if (File.Exists(outPath)) File.Delete(outPath);

        using var zip = new System.IO.Compression.ZipArchive(
            File.Create(outPath),
            System.IO.Compression.ZipArchiveMode.Create,
            leaveOpen: false,
            System.Text.Encoding.UTF8);

        var encoder = new WebpEncoder { Quality = quality };

        for (int i = 0; i < pages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var page = pages[i];

            using var srcStream = _archive.OpenPage(book.FilePath, page);
            using var img       = await ISImage.LoadAsync(srcStream, ct);

            var entryName = Path.ChangeExtension(page.Name, ".webp");
            var entry     = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.NoCompression);
            await using var entryStream = entry.Open();
            await img.SaveAsWebpAsync(entryStream, encoder, ct);

            progress?.Report((i + 1, pages.Count));
        }
    }

    /// <summary>썸네일(커버) BitmapImage 생성 (첫 페이지 기준)</summary>
    public async Task<BitmapImage?> GetThumbnailAsync(
        ComicBook book,
        int maxWidth = 160,
        CancellationToken ct = default)
    {
        try
        {
            // ImageSharp 내부에서 ConfigureAwait(false)를 사용하므로 Task.Run으로 격리
            // → await 완료 후 Dispatcher(UI 스레드)로 복귀 보장
            var ms = await Task.Run(async () =>
            {
                var pages = _archive.GetPages(book.FilePath);
                if (pages.Count == 0) return null;

                using var stream = _archive.OpenPage(book.FilePath, pages[0]);
                using var img    = await ISImage.LoadAsync(stream, ct).ConfigureAwait(false);

                img.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new ISSize(maxWidth, 0),
                    Mode = ISResizeMode.Max,
                }));

                var mem = new MemoryStream();
                await img.SaveAsJpegAsync(mem, ct).ConfigureAwait(false);
                mem.Position = 0;
                return mem;
            }, ct);

            if (ms is null) return null;

            // Task.Run 완료 후 Dispatcher에서 BitmapImage 생성
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
