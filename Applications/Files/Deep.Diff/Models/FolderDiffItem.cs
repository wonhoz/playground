namespace DeepDiff.Models;

public class FolderDiffItem
{
    public string RelPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public DiffStatus Status { get; set; }
    public int Depth { get; set; }

    // Left side
    public string? LeftName { get; set; }
    public long? LeftSize { get; set; }
    public DateTime? LeftModified { get; set; }
    public string? LeftFullPath { get; set; }

    // Right side
    public string? RightName { get; set; }
    public long? RightSize { get; set; }
    public DateTime? RightModified { get; set; }
    public string? RightFullPath { get; set; }

    // Display
    public string DisplayName => LeftName ?? RightName ?? RelPath;
    public string LeftSizeText   => LeftSize.HasValue   ? FormatSize(LeftSize.Value)   : "";
    public string RightSizeText  => RightSize.HasValue  ? FormatSize(RightSize.Value)  : "";
    public string LeftModText    => LeftModified?.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string RightModText   => RightModified?.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string IndentedName   => new string(' ', Depth * 2) + (IsDirectory ? "📁 " : "  ") + DisplayName;

    public string StatusIcon => Status switch
    {
        DiffStatus.Same       => "=",
        DiffStatus.Different  => "≠",
        DiffStatus.LeftOnly   => "←",
        DiffStatus.RightOnly  => "→",
        _ => "?"
    };

    public string StatusColor => Status switch
    {
        DiffStatus.Same       => "#3FC878",
        DiffStatus.Different  => "#F0B030",
        DiffStatus.LeftOnly   => "#5B8FFF",
        DiffStatus.RightOnly  => "#E05555",
        _ => "#888888"
    };

    public string LeftRowColor => Status switch
    {
        DiffStatus.Different => "#2A2310",
        DiffStatus.LeftOnly  => "#182030",
        _                    => "Transparent"
    };

    public string RightRowColor => Status switch
    {
        DiffStatus.Different  => "#2A2310",
        DiffStatus.RightOnly  => "#182030",
        _                     => "Transparent"
    };

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
    }
}
