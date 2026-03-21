using System.IO;
using SkiaSharp;
using Svg.Skia;

namespace ImgCast.Services;

public record ConversionResult(int Success, int Failed, IReadOnlyList<(string File, string Error)> Errors);

public enum OutputFormat { ICO, PNG, JPG, BMP }
public enum InputFilter  { All, SVG, PNG, JPG, BMP }

public static class ImageConverter
{
    static readonly int[] IcoSizes = [16, 24, 32, 48, 64, 128, 256];

    static readonly string[] SupportedExts = [".svg", ".png", ".jpg", ".jpeg", ".bmp"];

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
            ? [.. files]
            : [.. files.Where(f => MatchesFilter(f, filter))];
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
            _               => true
        };
    }

    // ─── 변환 메인 ───────────────────────────────────────────────────────────
    public static async Task<ConversionResult> ConvertAsync(
        string[] files,
        OutputFormat output,
        bool overwrite,
        int jpgQuality,
        IProgress<(int Current, int Total, string File)>? progress,
        CancellationToken ct)
    {
        int success = 0, failed = 0;
        var errors = new List<(string, string)>();

        await Task.Run(() =>
        {
            for (int i = 0; i < files.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                string src = files[i];
                progress?.Report((i + 1, files.Length, Path.GetFileName(src)));

                try
                {
                    ConvertSingle(src, output, overwrite, jpgQuality);
                    success++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add((src, ex.Message));
                }
            }
        }, ct);

        return new ConversionResult(success, failed, errors);
    }

    // ─── 단일 파일 변환 ──────────────────────────────────────────────────────
    static void ConvertSingle(string src, OutputFormat output, bool overwrite, int jpgQuality)
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

        using SKBitmap src_bmp = LoadAsBitmap(src, ext, output == OutputFormat.ICO ? IcoSizes[^1] : 0);

        switch (output)
        {
            case OutputFormat.ICO:
                SaveIco(src, ext, dest);
                break;
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

    // ─── ICO 저장 (7개 사이즈) ──────────────────────────────────────────────
    static void SaveIco(string src, string ext, string dest)
    {
        var images = new List<(int, byte[])>();
        foreach (int sz in IcoSizes)
        {
            using var bmp = LoadAsBitmap(src, ext, sz);
            images.Add((sz, BitmapToPng(bmp)));
        }
        File.WriteAllBytes(dest, IcoEncoder.Encode(images));
    }

    // ─── 이미지 로드 ────────────────────────────────────────────────────────
    static SKBitmap LoadAsBitmap(string path, string ext, int targetSize)
    {
        if (ext == ".svg")
            return LoadSvgAsBitmap(path, targetSize > 0 ? targetSize : 256);

        using var original = SKBitmap.Decode(path)
            ?? throw new InvalidDataException("이미지 디코드 실패");

        if (targetSize <= 0)
            return original.Copy();

        return ResizeFit(original, targetSize);
    }

    static SKBitmap LoadSvgAsBitmap(string path, int size)
    {
        var svg = new SKSvg();
        svg.Load(path);
        if (svg.Picture is null)
            throw new InvalidOperationException("SVG 파싱 실패");

        var bmp = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);

        float srcW = svg.Picture.CullRect.Width;
        float srcH = svg.Picture.CullRect.Height;
        if (srcW <= 0 || srcH <= 0) { srcW = size; srcH = size; }

        // aspect ratio 유지 + 투명 패딩
        float scale = Math.Min(size / srcW, size / srcH);
        float dx = (size - srcW * scale) / 2f;
        float dy = (size - srcH * scale) / 2f;

        canvas.Translate(dx, dy);
        canvas.Scale(scale);
        canvas.DrawPicture(svg.Picture);

        return bmp;
    }

    static SKBitmap ResizeFit(SKBitmap src, int size)
    {
        float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
        int fw = (int)(src.Width  * scale);
        int fh = (int)(src.Height * scale);

        var resized = src.Resize(new SKImageInfo(fw, fh), SKFilterQuality.High)
            ?? throw new InvalidOperationException("리사이즈 실패");

        if (fw == size && fh == size)
            return resized;

        // 투명 패딩으로 정사각형 완성
        var padded = new SKBitmap(size, size);
        using var canvas = new SKCanvas(padded);
        canvas.Clear(SKColors.Transparent);
        int ox = (size - fw) / 2;
        int oy = (size - fh) / 2;
        canvas.DrawBitmap(resized, ox, oy);
        resized.Dispose();
        return padded;
    }

    // ─── 저장 유틸 ───────────────────────────────────────────────────────────
    static void SaveEncoded(SKBitmap bmp, string dest, SKEncodedImageFormat fmt, int quality)
    {
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(fmt, quality);
        using var fs   = File.OpenWrite(dest);
        data.SaveTo(fs);
    }

    static byte[] BitmapToPng(SKBitmap bmp)
    {
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
