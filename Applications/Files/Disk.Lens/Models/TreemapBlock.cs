namespace DiskLens.Models;

/// <summary>트리맵 렌더링용 블록 (정규화된 화면 좌표)</summary>
public class TreemapBlock
{
    public FileNode Node { get; set; } = null!;
    public Rect Bounds { get; set; }          // 화면 픽셀 좌표
    public Color FillColor { get; set; }
    public int Depth { get; set; }
}
