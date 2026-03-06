using System.IO;
using DiskLens.Models;

namespace DiskLens.Services;

public record ScanProgress(string CurrentPath, long ItemsScanned, long TotalSize);

public class DiskScanner
{
    private long _itemsScanned;

    public async Task<DiskItem> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        _itemsScanned = 0;
        return await Task.Run(() => ScanDirectory(rootPath, 0, progress, ct), ct);
    }

    private DiskItem ScanDirectory(string path, int depth, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        DirectoryInfo di;
        try { di = new DirectoryInfo(path); }
        catch { return MakeErrorItem(path, depth); }

        var item = new DiskItem
        {
            Name = string.IsNullOrEmpty(di.Name) ? path : di.Name,
            FullPath = path,
            IsDirectory = true,
            Depth = depth,
            LastModified = SafeGet(() => di.LastWriteTime),
        };

        try
        {
            // 하위 디렉토리 재귀 스캔
            DirectoryInfo[] dirs;
            try { dirs = di.GetDirectories(); }
            catch (UnauthorizedAccessException) { item.AccessDenied = true; return item; }

            foreach (var sub in dirs)
            {
                ct.ThrowIfCancellationRequested();
                var child = ScanDirectory(sub.FullName, depth + 1, progress, ct);
                item.Children.Add(child);
                item.Size += child.Size;
                item.AllocatedSize += child.AllocatedSize;
                item.FileCount += child.FileCount;
                item.FolderCount += child.FolderCount + 1;
            }

            // 파일 스캔
            FileInfo[] files;
            try { files = di.GetFiles(); }
            catch (UnauthorizedAccessException) { files = []; }

            foreach (var fi in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileSize = SafeGet(() => fi.Length);
                var allocSize = GetAllocatedSize(fileSize);
                var fileItem = new DiskItem
                {
                    Name = fi.Name,
                    FullPath = fi.FullName,
                    IsDirectory = false,
                    Size = fileSize,
                    AllocatedSize = allocSize,
                    FileCount = 1,
                    Depth = depth + 1,
                    LastModified = SafeGet(() => fi.LastWriteTime),
                };
                item.Children.Add(fileItem);
                item.Size += fileSize;
                item.AllocatedSize += allocSize;
                item.FileCount++;

                var cnt = Interlocked.Increment(ref _itemsScanned);
                if (cnt % 500 == 0)
                    progress?.Report(new(fi.FullName, cnt, item.Size));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        // 크기 내림차순 정렬
        var sorted = item.Children.OrderByDescending(c => c.Size).ToList();
        item.Children.Clear();
        foreach (var c in sorted) item.Children.Add(c);

        // 부모 대비 % 계산
        if (item.Size > 0)
        {
            foreach (var child in item.Children)
                child.PercentOfParent = item.Size > 0 ? child.Size * 100.0 / item.Size : 0;
        }

        return item;
    }

    private static DiskItem MakeErrorItem(string path, int depth) => new()
    {
        Name = Path.GetFileName(path).Length > 0 ? Path.GetFileName(path) : path,
        FullPath = path,
        IsDirectory = true,
        AccessDenied = true,
        Depth = depth,
    };

    private static T SafeGet<T>(Func<T> func)
    {
        try { return func(); } catch { return default!; }
    }

    // 클러스터 크기 기반 할당 크기 추정 (기본 4096B)
    private static long GetAllocatedSize(long fileSize)
    {
        const long cluster = 4096;
        if (fileSize == 0) return 0;
        return (fileSize + cluster - 1) / cluster * cluster;
    }

    // 최대 N개의 가장 큰 파일 목록 추출
    public static List<DiskItem> GetTopFiles(DiskItem root, int count = 200)
    {
        var result = new List<DiskItem>();
        CollectFiles(root, result);
        return [.. result.OrderByDescending(f => f.Size).Take(count)];
    }

    private static void CollectFiles(DiskItem item, List<DiskItem> result)
    {
        foreach (var child in item.Children)
        {
            if (!child.IsDirectory)
                result.Add(child);
            else
                CollectFiles(child, result);
        }
    }

    // 확장자별 통계 집계
    public static List<ExtensionInfo> GetExtensionStats(DiskItem root)
    {
        var dict = new Dictionary<string, ExtensionInfo>(StringComparer.OrdinalIgnoreCase);
        CollectExtensions(root, dict);
        return [.. dict.Values.OrderByDescending(e => e.TotalSize)];
    }

    private static void CollectExtensions(DiskItem item, Dictionary<string, ExtensionInfo> dict)
    {
        foreach (var child in item.Children)
        {
            if (!child.IsDirectory)
            {
                var ext = Path.GetExtension(child.Name);
                if (string.IsNullOrEmpty(ext)) ext = "(확장자 없음)";
                if (!dict.TryGetValue(ext, out var info))
                {
                    info = new ExtensionInfo { Extension = ext };
                    dict[ext] = info;
                }
                info.TotalSize += child.Size;
                info.FileCount++;
            }
            else
            {
                CollectExtensions(child, dict);
            }
        }
    }
}
