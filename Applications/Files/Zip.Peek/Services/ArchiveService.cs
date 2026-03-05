using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ZipPeek.Services;

/// <summary>아카이브 내 항목 정보.</summary>
public class ArchiveNode
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long CompressedSize { get; init; }
    public long UncompressedSize { get; init; }
    public DateTime? LastModified { get; init; }
    public string Icon => IsDirectory ? "📁" : GetFileIcon(Name);

    public ObservableCollection<ArchiveNode> Children { get; } = [];

    public string SizeText => IsDirectory ? "" : FormatSize(UncompressedSize);
    public string CompressedText => IsDirectory ? "" : FormatSize(CompressedSize);
    public string RatioText
    {
        get
        {
            if (IsDirectory || UncompressedSize == 0) return "";
            double r = (1.0 - (double)CompressedSize / UncompressedSize) * 100;
            return $"{r:F0}%";
        }
    }
    public string DateText => LastModified?.ToString("yyyy-MM-dd HH:mm") ?? "";

    private static string FormatSize(long b) => b switch
    {
        >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{b / 1_048_576.0:F1} MB",
        >= 1_024 => $"{b / 1024.0:F0} KB",
        _ => $"{b} B"
    };

    private static string GetFileIcon(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" or ".log" or ".csv" or ".json" or ".xml"
            or ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg"  => "📄",
            ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp"
            or ".c" or ".h" or ".go" or ".rs" or ".kt" or ".swift" => "📝",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp"
            or ".webp" or ".svg" or ".ico"                         => "🖼",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz"
            or ".bz2" or ".xz"                                     => "🗜",
            ".exe" or ".dll" or ".so" or ".dylib"                  => "⚙",
            ".pdf"                                                  => "📕",
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a"       => "🎵",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm"       => "🎬",
            _ => "📎"
        };
    }
}

public class ArchiveStats
{
    public int TotalFiles { get; set; }
    public int TotalDirs { get; set; }
    public long TotalUncompressed { get; set; }
    public long TotalCompressed { get; set; }
    public string Format { get; set; } = "";
}

public class ArchiveService
{
    /// <summary>아카이브를 열어 루트 노드 목록과 통계를 반환.</summary>
    public (List<ArchiveNode> roots, ArchiveStats stats) Open(string path)
    {
        using var archive = ArchiveFactory.Open(path);
        var stats = new ArchiveStats { Format = archive.Type.ToString() };

        // 경로 → 노드 맵
        var nodeMap = new Dictionary<string, ArchiveNode>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<ArchiveNode>();

        foreach (var entry in archive.Entries)
        {
            var key = entry.Key?.Replace('\\', '/').TrimEnd('/') ?? "";
            if (string.IsNullOrEmpty(key)) continue;

            var node = new ArchiveNode
            {
                Name = Path.GetFileName(key.TrimEnd('/')) is { Length: > 0 } n ? n : key,
                FullPath = key,
                IsDirectory = entry.IsDirectory,
                CompressedSize = entry.CompressedSize,
                UncompressedSize = entry.Size,
                LastModified = entry.LastModifiedTime
            };
            nodeMap[key] = node;

            if (entry.IsDirectory)
                stats.TotalDirs++;
            else
            {
                stats.TotalFiles++;
                stats.TotalUncompressed += entry.Size;
                stats.TotalCompressed += entry.CompressedSize;
            }
        }

        // 부모-자식 연결 (경로 구조 기반)
        foreach (var (key, node) in nodeMap)
        {
            var parentPath = Path.GetDirectoryName(key)?.Replace('\\', '/') ?? "";
            if (string.IsNullOrEmpty(parentPath))
                roots.Add(node);
            else if (nodeMap.TryGetValue(parentPath, out var parent))
                parent.Children.Add(node);
            else
            {
                // 중간 폴더가 아카이브에 없을 수도 있음 → 자동 생성
                var phantom = EnsureDirectory(parentPath, nodeMap, roots);
                phantom.Children.Add(node);
            }
        }

        // 각 레벨 정렬 (폴더 먼저, 이름 순)
        SortNodes(roots);
        return (roots, stats);
    }

    private static ArchiveNode EnsureDirectory(string path, Dictionary<string, ArchiveNode> map, List<ArchiveNode> roots)
    {
        if (map.TryGetValue(path, out var existing)) return existing;
        var node = new ArchiveNode
        {
            Name = Path.GetFileName(path) is { Length: > 0 } n ? n : path,
            FullPath = path,
            IsDirectory = true
        };
        map[path] = node;
        var parentPath = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
        if (string.IsNullOrEmpty(parentPath))
            roots.Add(node);
        else
            EnsureDirectory(parentPath, map, roots).Children.Add(node);
        return node;
    }

    private static void SortNodes(IList<ArchiveNode> nodes)
    {
        var sorted = nodes.OrderByDescending(n => n.IsDirectory).ThenBy(n => n.Name).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i < nodes.Count) ((ObservableCollection<ArchiveNode>)nodes)[i] = sorted[i];
        }
        foreach (var node in nodes)
            if (node.Children.Count > 0) SortNodes(node.Children);
    }

    /// <summary>선택한 노드들을 outputDir에 추출.</summary>
    public async Task ExtractNodesAsync(string archivePath, IEnumerable<ArchiveNode> nodes,
        string outputDir, IProgress<(int done, int total)>? progress = null)
    {
        var targetPaths = new HashSet<string>(
            nodes.SelectMany(FlattenPaths), StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries
                .Where(e => !e.IsDirectory && targetPaths.Contains(e.Key?.Replace('\\', '/') ?? ""))
                .ToList();

            int done = 0;
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(outputDir,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                progress?.Report((++done, entries.Count));
            }
        });
    }

    /// <summary>전체 추출.</summary>
    public async Task ExtractAllAsync(string archivePath, string outputDir,
        IProgress<(int done, int total)>? progress = null)
    {
        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            int done = 0;
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(outputDir,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                progress?.Report((++done, entries.Count));
            }
        });
    }

    /// <summary>노드와 모든 하위 파일의 FullPath를 평탄화.</summary>
    public static IEnumerable<string> FlattenPaths(ArchiveNode node)
    {
        if (!node.IsDirectory)
        {
            yield return node.FullPath;
            yield break;
        }
        foreach (var child in node.Children)
            foreach (var p in FlattenPaths(child)) yield return p;
    }

    /// <summary>특정 엔트리의 스트림을 반환 (미리보기용).</summary>
    public byte[] ReadEntry(string archivePath, string entryKey)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Key?.Replace('\\', '/'), entryKey, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return [];
        using var ms = new MemoryStream();
        using var s = entry.OpenEntryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
