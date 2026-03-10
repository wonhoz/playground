using System.Drawing;
using System.Drawing.Drawing2D;
using Imaging = System.Drawing.Imaging;

namespace Mosaic.Forge.Services;

record MosaicOptions(int TileSize, int MaxReuse);

sealed class MosaicEngine
{
    static readonly HashSet<string> _imgExts = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".webp" };

    // ── 소스 폴더 스캔 ────────────────────────────────────────────────────────

    public async Task<List<TileEntry>> ScanFolderAsync(
        string folder,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => _imgExts.Contains(Path.GetExtension(f)))
            .ToArray();

        var bag  = new ConcurrentBag<TileEntry>();
        int done = 0;
        int idx  = 0;

        await Task.Run(() => Parallel.ForEach(files,
            new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
            file =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var bmp       = LoadBitmap(file);
                    var (L, A, B)       = ComputeAvgLab(bmp);
                    int i               = Interlocked.Increment(ref idx);
                    bag.Add(new TileEntry { FilePath = file, LabL = L, LabA = A, LabB = B, Index = i - 1 });
                }
                catch { /* 유효하지 않은 이미지 건너뜀 */ }

                progress?.Report((Interlocked.Increment(ref done), files.Length));
            }), ct);

        return [.. bag.OrderBy(t => t.Index)];
    }

    // ── 모자이크 생성 ─────────────────────────────────────────────────────────

    public async Task<Bitmap> GenerateAsync(
        Bitmap target,
        List<TileEntry> tiles,
        MosaicOptions opts,
        IProgress<(int Done, int Total, string Phase)>? progress = null,
        CancellationToken ct = default)
    {
        if (tiles.Count == 0) throw new InvalidOperationException("소스 이미지가 없습니다.");

        int T  = opts.TileSize;
        int gW = Math.Max(1, target.Width  / T);
        int gH = Math.Max(1, target.Height / T);

        // Phase 1: k-d Tree 구축
        progress?.Report((0, 1, "색상 인덱스 구축 중..."));
        var tree = new KdTree();
        tree.Build(tiles);

        // Phase 2: 타겟 이미지 픽셀 잠금 → 셀별 색상 매칭
        var td = target.LockBits(
            new Rectangle(0, 0, target.Width, target.Height),
            Imaging.ImageLockMode.ReadOnly,
            Imaging.PixelFormat.Format32bppArgb);

        var assignments = new TileEntry[gH, gW];
        var useCounts   = new Dictionary<int, int>();
        int maxReuse    = opts.MaxReuse;

        await Task.Run(() =>
        {
            for (int gy = 0; gy < gH; gy++)
            {
                ct.ThrowIfCancellationRequested();
                for (int gx = 0; gx < gW; gx++)
                {
                    var (L, A, B) = CellAvgLab(td, gx * T, gy * T, T, T);

                    TileEntry tile;
                    if (maxReuse <= 0)
                    {
                        tile = tree.FindNearest(L, A, B);
                    }
                    else
                    {
                        tile = tree.FindNearest(L, A, B, t =>
                            !useCounts.TryGetValue(t.Index, out int c) || c < maxReuse);
                    }

                    assignments[gy, gx] = tile;
                    useCounts.TryGetValue(tile.Index, out int cnt);
                    useCounts[tile.Index] = cnt + 1;
                }
                progress?.Report((gy + 1, gH, "타일 매칭 중..."));
            }
        }, ct);

        target.UnlockBits(td);
        ct.ThrowIfCancellationRequested();

        // Phase 3: 고유 타일 이미지 로딩
        var unique     = assignments.Cast<TileEntry>().Distinct().ToList();
        var tilePixels = new ConcurrentDictionary<string, byte[]>();
        int loaded     = 0;

        await Task.Run(() => Parallel.ForEach(unique,
            new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
            entry =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var bmp = LoadAndResize(entry.FilePath, T);
                    tilePixels[entry.FilePath] = ExtractPixels(bmp, T);
                }
                catch { }
                progress?.Report((Interlocked.Increment(ref loaded), unique.Count, "타일 이미지 로딩 중..."));
            }), ct);

        ct.ThrowIfCancellationRequested();

        // Phase 4: 출력 비트맵 합성
        int composed = 0;
        var output = await Task.Run(() =>
        {
            var bmp = new Bitmap(gW * T, gH * T, Imaging.PixelFormat.Format32bppArgb);
            var bd  = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                          Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format32bppArgb);

            Parallel.For(0, gH, gy =>
            {
                for (int gx = 0; gx < gW; gx++)
                {
                    if (tilePixels.TryGetValue(assignments[gy, gx].FilePath, out var px))
                        CopyTile(bd, px, gx, gy, T);
                }
                progress?.Report((Interlocked.Increment(ref composed), gH, "모자이크 합성 중..."));
            });

            bmp.UnlockBits(bd);
            return bmp;
        }, ct);

        return output;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    static Bitmap LoadBitmap(string path)
    {
        using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var src = new Bitmap(fs);
        var result    = new Bitmap(src.Width, src.Height, Imaging.PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(result);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
        return result;
    }

    internal static Bitmap LoadAndResize(string path, int size)
    {
        using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var src = new Bitmap(fs);
        var dst       = new Bitmap(size, size, Imaging.PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(dst);
        g.InterpolationMode    = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality   = CompositingQuality.HighQuality;
        g.SmoothingMode        = SmoothingMode.AntiAlias;

        // 중앙 크롭 (정사각형)
        float aspect = (float)src.Width / src.Height;
        int cropW, cropH, cropX, cropY;
        if (aspect >= 1f)
        {
            cropH = src.Height; cropW = src.Height;
            cropX = (src.Width - cropW) / 2; cropY = 0;
        }
        else
        {
            cropW = src.Width; cropH = src.Width;
            cropX = 0; cropY = (src.Height - cropH) / 2;
        }

        g.DrawImage(src, new Rectangle(0, 0, size, size),
                    new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
        return dst;
    }

    internal static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        var bd = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            Imaging.ImageLockMode.ReadOnly,
            Imaging.PixelFormat.Format32bppArgb);
        var bs = BitmapSource.Create(
            bmp.Width, bmp.Height, 96, 96,
            PixelFormats.Bgra32, null,
            bd.Scan0, bd.Stride * bmp.Height, bd.Stride);
        bmp.UnlockBits(bd);
        bs.Freeze();
        return bs;
    }

    static (double L, double A, double B) ComputeAvgLab(Bitmap bmp)
    {
        var bd   = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                       Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb);
        int step = Math.Max(1, Math.Min(bmp.Width, bmp.Height) / 20);
        double sumL = 0, sumA = 0, sumB = 0;
        int count = 0;

        unsafe
        {
            byte* p = (byte*)bd.Scan0;
            for (int y = 0; y < bmp.Height; y += step)
            {
                byte* row = p + y * bd.Stride;
                for (int x = 0; x < bmp.Width; x += step)
                {
                    byte* px    = row + x * 4;
                    var (L, A, B) = ColorSpace.RgbToLab(px[2], px[1], px[0]); // BGRA
                    sumL += L; sumA += A; sumB += B;
                    count++;
                }
            }
        }

        bmp.UnlockBits(bd);
        return count == 0 ? (50, 0, 0) : (sumL / count, sumA / count, sumB / count);
    }

    static unsafe (double L, double A, double B) CellAvgLab(Imaging.BitmapData bd,
        int x0, int y0, int w, int h)
    {
        int step  = Math.Max(1, w / 8);
        double sumL = 0, sumA = 0, sumB = 0;
        int count = 0;
        byte* p   = (byte*)bd.Scan0;

        for (int y = y0; y < y0 + h && y < bd.Height; y += step)
        {
            byte* row = p + y * bd.Stride;
            for (int x = x0; x < x0 + w && x < bd.Width; x += step)
            {
                byte* px    = row + x * 4;
                var (L, A, B) = ColorSpace.RgbToLab(px[2], px[1], px[0]);
                sumL += L; sumA += A; sumB += B;
                count++;
            }
        }

        return count == 0 ? (50, 0, 0) : (sumL / count, sumA / count, sumB / count);
    }

    static unsafe byte[] ExtractPixels(Bitmap bmp, int size)
    {
        var bd    = bmp.LockBits(new Rectangle(0, 0, size, size),
                       Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb);
        var bytes = new byte[size * size * 4];
        fixed (byte* dst = bytes)
        {
            for (int row = 0; row < size; row++)
                Buffer.MemoryCopy(
                    (byte*)bd.Scan0 + row * bd.Stride,
                    dst + row * size * 4,
                    (long)size * 4, (long)size * 4);
        }
        bmp.UnlockBits(bd);
        return bytes;
    }

    static unsafe void CopyTile(Imaging.BitmapData bd, byte[] pixels, int gx, int gy, int T)
    {
        byte* dst = (byte*)bd.Scan0 + (long)gy * T * bd.Stride + (long)gx * T * 4;
        fixed (byte* src = pixels)
        {
            for (int row = 0; row < T; row++)
                Buffer.MemoryCopy(
                    src + row * T * 4,
                    dst + row * bd.Stride,
                    (long)T * 4, (long)T * 4);
        }
    }
}
