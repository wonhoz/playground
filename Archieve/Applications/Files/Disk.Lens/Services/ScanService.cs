namespace DiskLens.Services;

/// <summary>비동기 디스크 스캔 서비스</summary>
public static class ScanService
{
    /// <summary>지정 경로를 재귀 스캔하여 FileNode 트리 반환</summary>
    public static async Task<FileNode?> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => ScanDirectory(rootPath, null, progress, ct), ct);
    }

    private static FileNode? ScanDirectory(
        string path,
        FileNode? parent,
        IProgress<ScanProgress>? progress,
        CancellationToken ct,
        int[] counter = null!)
    {
        ct.ThrowIfCancellationRequested();

        counter ??= [0];

        var node = new FileNode
        {
            Name      = Path.GetFileName(path) is { Length: > 0 } n ? n : path,
            FullPath  = path,
            IsDirectory = true,
            Parent    = parent,
        };

        try
        {
            // 파일 처리
            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    var child = new FileNode
                    {
                        Name      = fi.Name,
                        FullPath  = fi.FullName,
                        IsDirectory = false,
                        Size      = fi.Length,
                        Parent    = node,
                    };
                    node.Children.Add(child);
                    counter[0]++;

                    if (counter[0] % 200 == 0)
                        progress?.Report(new ScanProgress { CurrentPath = file, ScannedCount = counter[0] });
                }
                catch { /* 접근 불가 파일 무시 */ }
            }

            // 하위 디렉터리 처리
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                var child = ScanDirectory(dir, node, progress, ct, counter);
                if (child != null)
                    node.Children.Add(child);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (OperationCanceledException) { throw; }
        catch { }

        // 디렉터리 크기 = 모든 자식 합산
        node.Size = node.Children.Sum(c => c.Size);
        return node;
    }

    /// <summary>트리에서 TOP N 큰 파일 추출</summary>
    public static List<TopFileEntry> GetTopFiles(FileNode root, int count = 20)
    {
        var list = new List<FileNode>();
        Collect(root, list);
        return list
            .OrderByDescending(f => f.Size)
            .Take(count)
            .Select((f, i) => new TopFileEntry
            {
                Rank      = i + 1,
                Name      = f.Name,
                FullPath  = f.FullPath,
                Size      = f.Size,
                Extension = f.Extension,
                ExtColor  = ExtensionColors.Get(f.Extension),
            })
            .ToList();
    }

    private static void Collect(FileNode node, List<FileNode> result)
    {
        if (!node.IsDirectory)
        {
            result.Add(node);
            return;
        }
        foreach (var child in node.Children)
            Collect(child, result);
    }
}
