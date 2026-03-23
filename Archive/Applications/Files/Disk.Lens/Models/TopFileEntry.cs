namespace DiskLens.Models;

/// <summary>TOP 20 큰 파일 목록 항목</summary>
public class TopFileEntry
{
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
    public string SizeText => FileNode.FormatSize(Size);
    public string Extension { get; set; } = "";
    public Color ExtColor { get; set; }
}
