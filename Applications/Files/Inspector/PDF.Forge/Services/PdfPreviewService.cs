using PDFtoImage;
using SkiaSharp;
using System.Runtime.InteropServices;
using PdfRenderOptions = PDFtoImage.RenderOptions;

namespace PdfForge.Services;

public class PdfPreviewService
{
    /// <summary>
    /// 특정 페이지 썸네일을 BitmapSource로 반환 (0-based).
    /// </summary>
    public async Task<BitmapSource> GetThumbnailAsync(string pdfPath, int pageIndex = 0, int dpi = 150)
    {
        return await Task.Run(() =>
        {
            using var stream = File.OpenRead(pdfPath);
            using var skBitmap = Conversion.ToImage(stream, page: (Index)pageIndex,
                options: new PdfRenderOptions(Dpi: dpi));
            return SkBitmapToBitmapSource(skBitmap);
        });
    }

    /// <summary>
    /// 모든 페이지 썸네일 목록을 반환.
    /// </summary>
    public async Task<List<BitmapSource>> GetAllThumbnailsAsync(string pdfPath, int dpi = 100,
        IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var stream = File.OpenRead(pdfPath);
            var results = new List<BitmapSource>();
            int idx = 0;
            foreach (var skBitmap in Conversion.ToImages(stream, options: new PdfRenderOptions(Dpi: dpi)))
            {
                using (skBitmap)
                    results.Add(SkBitmapToBitmapSource(skBitmap));
                progress?.Report(++idx);
            }
            return results;
        });
    }

    private static BitmapSource SkBitmapToBitmapSource(SKBitmap skBitmap)
    {
        // SKBitmap을 BGRA32 포맷으로 변환 후 WriteableBitmap에 복사
        using var converted = skBitmap.Copy(SKColorType.Bgra8888);
        var wb = new WriteableBitmap(
            converted.Width, converted.Height,
            96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        Marshal.Copy(converted.Bytes, 0, wb.BackBuffer,
            converted.ByteCount);
        wb.AddDirtyRect(new Int32Rect(0, 0, converted.Width, converted.Height));
        wb.Unlock();
        wb.Freeze();
        return wb;
    }
}
