using System.IO;
using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace ImgCast.Services;

public record ConversionResult(int Success, int Failed, IReadOnlyList<(string File, string Error)> Errors, bool Cancelled = false, int Skipped = 0);

public enum OutputFormat { ICO, PNG, JPG, BMP }
public enum InputFilter  { All, SVG, PNG, JPG, BMP, ICO }

public static class ImageConverter
{
    static readonly int[] DefaultIcoSizes = [16, 32, 48, 64, 128, 256];

    static readonly string[] SupportedExts = [".svg", ".png", ".jpg", ".jpeg", ".bmp", ".ico"];


    // ─── 파일 수집 ───────────────────────────────────────────────────────────
    public static string[] CollectFiles(string[] dropped, InputFilter filter)
    {
        var files = new List<string>();
        foreach (var path in dropped)
        {
            if (Directory.Exists(path))
                files.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupported));
            else if (File.Exists(path) && IsSupported(path))
                files.Add(path);
        }

        return filter == InputFilter.All
            ? [.. files.Distinct(StringComparer.OrdinalIgnoreCase)]
            : [.. files.Where(f => MatchesFilter(f, filter)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedExts.Contains(ext);
    }

    static bool MatchesFilter(string path, InputFilter filter)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return filter switch
        {
            InputFilter.SVG => ext == ".svg",
            InputFilter.PNG => ext == ".png",
            InputFilter.JPG => ext is ".jpg" or ".jpeg",
            InputFilter.BMP => ext == ".bmp",
            InputFilter.ICO => ext == ".ico",
            _               => true
        };
    }

    // ─── 변환 메인 ───────────────────────────────────────────────────────────
    public static async Task<ConversionResult> ConvertAsync(
        string[] files,
        OutputFormat output,
        bool overwrite,
        int jpgQuality,
        int[]? icoSizes,
        int svgOutputSize,
        IProgress<(int Current, int Total, string File)>? progress,
        CancellationToken ct)
    {
        int success = 0, failed = 0, skipped = 0;
        var errors = new List<(string, string)>();
        var sizes  = icoSizes is { Length: > 0 } ? icoSizes : DefaultIcoSizes;
        bool cancelled = false;

        await Task.Run(() =>
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (ct.IsCancellationRequested) { cancelled = true; break; }
                string src = files[i];
                progress?.Report((i + 1, files.Length, Path.GetFileName(src)));

                // 동일 포맷 변환 스킵 (ICO→ICO는 재인코딩 의미 없음)
                string srcExt = Path.GetExtension(src).ToLowerInvariant();
                string outExt = output switch
                {
                    OutputFormat.ICO => ".ico",
                    OutputFormat.JPG => ".jpg",
                    OutputFormat.BMP => ".bmp",
                    _                => ".png"
                };
                if (srcExt == outExt || (srcExt == ".jpeg" && outExt == ".jpg"))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    ConvertSingle(src, output, overwrite, jpgQuality, sizes, svgOutputSize);
                    success++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add((src, ex.Message));
                }
            }
        }, CancellationToken.None);

        return new ConversionResult(success, failed, errors, cancelled, skipped);
    }

    // ─── 단일 파일 변환 ──────────────────────────────────────────────────────
    static void ConvertSingle(string src, OutputFormat output, bool overwrite, int jpgQuality, int[] icoSizes, int svgOutputSize)
    {
        string ext = Path.GetExtension(src).ToLowerInvariant();
        string outExt = output switch
        {
            OutputFormat.ICO => ".ico",
            OutputFormat.JPG => ".jpg",
            OutputFormat.BMP => ".bmp",
            _                => ".png"
        };

        string dest = BuildDestPath(src, outExt, overwrite);

        if (output == OutputFormat.ICO)
        {
            SaveIco(src, ext, dest, icoSizes);
            return;
        }

        // SVG 소스는 선택된 출력 크기로 래스터화, 래스터 소스는 원본 크기 유지
        int loadSize = ext == ".svg" ? svgOutputSize : 0;
        using SKBitmap src_bmp = LoadAsBitmap(src, ext, loadSize);

        switch (output)
        {
            case OutputFormat.PNG:
                SaveEncoded(src_bmp, dest, SKEncodedImageFormat.Png, 100);
                break;
            case OutputFormat.JPG:
                SaveEncoded(src_bmp, dest, SKEncodedImageFormat.Jpeg, jpgQuality);
                break;
            case OutputFormat.BMP:
                SaveEncoded(src_bmp, dest, SKEncodedImageFormat.Bmp, 100);
                break;
        }
    }

    static string BuildDestPath(string src, string outExt, bool overwrite)
    {
        string dir  = Path.GetDirectoryName(src)!;
        string stem = Path.GetFileNameWithoutExtension(src);
        string candidate = Path.Combine(dir, stem + outExt);

        if (overwrite || !File.Exists(candidate))
            return candidate;

        return Path.Combine(dir, stem + "_converted" + outExt);
    }

    // ─── ICO 저장 (커스텀 사이즈 지원) ────────────────────────────────────
    static void SaveIco(string src, string ext, string dest, int[] sizes)
    {
        var images = new List<(int, byte[])>();
        foreach (int sz in sizes)
        {
            using var bmp = LoadAsBitmap(src, ext, sz);
            images.Add((sz, BitmapToPng(bmp)));
        }
        File.WriteAllBytes(dest, IcoEncoder.Encode(images));
    }

    // ─── 이미지 로드 (ICO 포함) ─────────────────────────────────────────────
    static SKBitmap LoadAsBitmap(string path, string ext, int targetSize)
    {
        if (ext == ".svg")
            return LoadSvgAsBitmap(path, targetSize > 0 ? targetSize : 1024);

        // ICO / PNG / JPG / BMP — SkiaSharp 기본 디코더로 처리
        using var original = SKBitmap.Decode(path)
            ?? throw new InvalidDataException("이미지 디코드 실패");

        if (targetSize <= 0)
            return original.Copy();

        return ResizeFit(original, targetSize);
    }

    static SKBitmap LoadSvgAsBitmap(string path, int size)
    {
        // SVG 전처리: 8자리 hex 색상 변환 + feDropShadow 확장 + dominant-baseline 보정
        string svgText  = File.ReadAllText(path, Encoding.UTF8);
        string processed = SvgPreprocessor.Process(svgText);
        string tempPath  = Path.Combine(Path.GetTempPath(), $"imgcast_{Guid.NewGuid():N}.svg");
        try
        {
            File.WriteAllText(tempPath, processed, new UTF8Encoding(false));
            return RenderSvg(tempPath, size);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    static SKBitmap RenderSvg(string path, int size)
    {
        var svg = new SKSvg();
        svg.Load(path);
        if (svg.Picture is null)
            throw new InvalidOperationException("SVG 파싱 실패");

        float srcW = svg.Picture.CullRect.Width;
        float srcH = svg.Picture.CullRect.Height;
        if (srcW <= 0 || srcH <= 0) { srcW = size; srcH = size; }

        // 소형 사이즈는 2x 슈퍼샘플링으로 계단 현상 방지
        int renderSize = size < 64 ? size * 2 : size;

        var renderBmp = new SKBitmap(new SKImageInfo(renderSize, renderSize, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(renderBmp))
        {
            canvas.Clear(SKColors.Transparent);

            float scale = Math.Min(renderSize / srcW, renderSize / srcH);
            float dx = (renderSize - srcW * scale) / 2f;
            float dy = (renderSize - srcH * scale) / 2f;

            canvas.Translate(dx, dy);
            canvas.Scale(scale);
            canvas.DrawPicture(svg.Picture);
        }

        if (renderSize == size)
            return renderBmp;

        // 2x → target 다운스케일
        var downscaled = renderBmp.Resize(
            new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        renderBmp.Dispose();
        return downscaled ?? throw new InvalidOperationException("SVG 다운스케일 실패");
    }

    static SKBitmap ResizeFit(SKBitmap src, int size)
    {
        float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
        int fw = (int)(src.Width  * scale);
        int fh = (int)(src.Height * scale);

        var resized = src.Resize(new SKImageInfo(fw, fh, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("리사이즈 실패");

        if (fw == size && fh == size)
            return resized;

        // 투명 패딩으로 정사각형 완성
        var padded = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(padded);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(resized, (size - fw) / 2, (size - fh) / 2);
        resized.Dispose();
        return padded;
    }

    // ─── ICO 미리보기 렌더링 ────────────────────────────────────────────────
    public static async Task<SKBitmap?> RenderIcoPreviewAsync(string path, int size = 240)
    {
        if (!path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            return await Task.Run(() =>
            {
                using var original = SKBitmap.Decode(path);
                if (original is null) return null;
                return ResizeFit(original, size);
            });
        }
        catch { return null; }
    }

    // ─── PNG/JPG/BMP 미리보기 렌더링 ───────────────────────────────────────
    public static async Task<SKBitmap?> RenderBitmapPreviewAsync(string path, int size = 240)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var original = SKBitmap.Decode(path);
                if (original is null) return null;
                return ResizeFit(original, size);
            });
        }
        catch { return null; }
    }

    // ─── SVG 미리보기 렌더링 ────────────────────────────────────────────────
    public static async Task<SKBitmap?> RenderPreviewAsync(string path, int size = 240)
    {
        if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            return await Task.Run(() => LoadSvgAsBitmap(path, size));
        }
        catch
        {
            return null;
        }
    }

    // ─── 저장 유틸 ───────────────────────────────────────────────────────────
    static void SaveEncoded(SKBitmap bmp, string dest, SKEncodedImageFormat fmt, int quality)
    {
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(fmt, quality);
        using var fs   = File.Open(dest, FileMode.Create);
        data.SaveTo(fs);
    }

    static byte[] BitmapToPng(SKBitmap bmp)
    {
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
