using Microsoft.Win32;

namespace RegVault.Models;

public record RegValueEntry(string Name, RegistryValueKind Kind, string DataDisplay);

public class RegSnapshot
{
    public string Label       { get; set; } = "";
    public DateTime TakenAt   { get; set; } = DateTime.Now;
    public string RootPath    { get; set; } = "";

    // Key 전체 경로 → 값 목록
    public Dictionary<string, List<RegValueEntry>> Data { get; set; } = new();
}

public enum DiffType { Added, Removed, Modified }

public class DiffEntry
{
    public DiffType Diff      { get; set; }
    public string KeyPath     { get; set; } = "";
    public string? ValueName  { get; set; } // null = 키 자체가 추가/삭제
    public string? OldData    { get; set; }
    public string? NewData    { get; set; }

    public string DiffLabel => Diff switch
    {
        DiffType.Added    => "추가",
        DiffType.Removed  => "삭제",
        DiffType.Modified => "변경",
        _                 => ""
    };

    public string DiffColor => Diff switch
    {
        DiffType.Added    => "#4CAF50",
        DiffType.Removed  => "#F44336",
        DiffType.Modified => "#FF9800",
        _                 => "#AAAAAA"
    };
}
