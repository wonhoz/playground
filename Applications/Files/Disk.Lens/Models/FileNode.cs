namespace DiskLens.Models;

/// <summary>파일/폴더 트리 노드</summary>
public class FileNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }           // 바이트 (디렉터리는 하위 합산)
    public FileNode? Parent { get; set; }
    public List<FileNode> Children { get; } = [];

    /// <summary>표시용 크기 문자열 (KB/MB/GB)</summary>
    public string SizeText => FormatSize(Size);

    /// <summary>확장자 (소문자, 없으면 "")</summary>
    public string Extension => IsDirectory ? "" : Path.GetExtension(Name).ToLowerInvariant();

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024L)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
