using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using FileDuplicates.Models;

namespace FileDuplicates.Services;

public static class ImageScanner
{
    private static readonly HashSet<string> ImageExts =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".heic", ".heif"];

    public static bool IsImage(string path) =>
        ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>이미지 파일 목록에서 유사도 기반 중복 그룹을 반환합니다.</summary>
    public static async Task<List<DuplicateGroup>> ScanAsync(
        IEnumerable<string> files,
        int                 threshold,
        IProgress<string>?  progress,
        CancellationToken   ct)
    {
        var imageFiles = files.Where(IsImage).ToList();
        if (imageFiles.Count < 2) return [];

        // 1단계: 모든 이미지의 dHash 계산
        var entries = new List<(FileEntry entry, ulong hash)>();

        foreach (var path in imageFiles)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(Path.GetFileName(path));

            try
            {
                var hash = await Task.Run(() => ComputeDHash(path), ct);
                entries.Add((new FileEntry
                {
                    Path         = path,
                    Size         = new FileInfo(path).Length,
                    LastModified = File.GetLastWriteTime(path)
                }, hash));
            }
            catch { /* 읽기 오류 건너뜀 */ }
        }

        // 2단계: Union-Find로 연결 컴포넌트 구성 (Hamming distance ≤ threshold)
        var n      = entries.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        var dist   = new int[n, n];  // 그룹 내 최대 거리 추적

        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int i = 0; i < n; i++)
        {
            ct.ThrowIfCancellationRequested();
            for (int j = i + 1; j < n; j++)
            {
                var d = HammingDistance(entries[i].hash, entries[j].hash);
                if (d <= threshold)
                    Union(i, j);
            }
        }

        // 3단계: 컴포넌트별로 그룹화
        var groups = entries
            .Select((e, i) => (e, root: Find(i)))
            .GroupBy(x => x.root)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                var fileList = g.Select(x => x.e.entry).ToList();
                // 그룹 내 최대 Hamming distance 계산
                var items    = g.ToList();
                int maxDist  = 0;
                for (int a = 0; a < items.Count; a++)
                    for (int b = a + 1; b < items.Count; b++)
                        maxDist = Math.Max(maxDist, HammingDistance(items[a].e.hash, items[b].e.hash));

                return new DuplicateGroup
                {
                    Type     = GroupType.Similar,
                    Files    = fileList,
                    Distance = maxDist
                };
            })
            .ToList();

        return groups;
    }

    /// <summary>dHash (difference hash) — 9×8 리사이즈 후 인접 픽셀 밝기 비교 64비트.</summary>
    private static ulong ComputeDHash(string path)
    {
        using var src     = new Bitmap(path);
        using var resized = new Bitmap(9, 8, PixelFormat.Format32bppArgb);
        using var g       = Graphics.FromImage(resized);
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.DrawImage(src, 0, 0, 9, 8);

        ulong hash = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var left  = resized.GetPixel(x,     y);
                var right = resized.GetPixel(x + 1, y);
                int lg = (left.R  * 299 + left.G  * 587 + left.B  * 114) / 1000;
                int rg = (right.R * 299 + right.G * 587 + right.B * 114) / 1000;
                if (lg > rg) hash |= 1UL << (y * 8 + x);
            }
        }
        return hash;
    }

    private static int HammingDistance(ulong a, ulong b)
    {
        ulong diff = a ^ b;
        int count = 0;
        while (diff != 0) { count += (int)(diff & 1); diff >>= 1; }
        return count;
    }
}
