namespace FileDuplicates.Models;

public enum GroupType { Hash, Similar }

public class DuplicateGroup
{
    public GroupType    Type      { get; init; }
    public List<FileEntry> Files  { get; init; } = [];
    public int          Distance  { get; init; }  // Hamming distance (Similar 전용)

    public long   TotalSize  => Files.Sum(f => f.Size);
    public string TypeBadge  => Type == GroupType.Hash
        ? "SHA256"
        : $"유사 ({Distance}bit)";
    public string Summary    => $"{Files.Count}개 파일 · {FormatSize(TotalSize)}";

    private static string FormatSize(long b) => b switch
    {
        < 1024L               => $"{b} B",
        < 1024L * 1024        => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / 1024.0 / 1024:F1} MB",
        _                     => $"{b / 1024.0 / 1024 / 1024:F2} GB"
    };
}
