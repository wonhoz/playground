namespace MemLens.Models;

/// <summary>시간별 메모리 샘플 (타임라인 그래프용)</summary>
public class MemorySnapshot
{
    public DateTime Time         { get; set; }
    public long     PrivateBytes { get; set; }
    public long     WorkingSet   { get; set; }
}
