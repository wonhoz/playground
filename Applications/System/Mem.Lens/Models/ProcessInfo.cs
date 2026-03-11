namespace MemLens.Models;

/// <summary>프로세스 메모리 정보 스냅샷</summary>
public class ProcessInfo
{
    public int    Pid           { get; set; }
    public string Name          { get; set; } = "";
    public long   PrivateBytes  { get; set; }   // Private Working Set
    public long   WorkingSet    { get; set; }   // Total Working Set
    public long   VirtualBytes  { get; set; }   // Virtual Address Space
    public long   PagedPool     { get; set; }
    public long   NonPagedPool  { get; set; }
    public long   PageFaults    { get; set; }
    public double CpuPercent    { get; set; }
    public bool   IsDotNet      { get; set; }

    // .NET GC 힙 (IsDotNet일 때)
    public long   GcGen0Size    { get; set; }
    public long   GcGen1Size    { get; set; }
    public long   GcGen2Size    { get; set; }
    public long   GcLohSize     { get; set; }
    public long   GcTotalHeap   { get; set; }

    // 트렌드 (leakdetector)
    public MemoryTrend Trend { get; set; } = MemoryTrend.Stable;

    // 표시용
    public string PrivateDisplay  => FormatBytes(PrivateBytes);
    public string WorkingSetDisplay => FormatBytes(WorkingSet);
    public string TrendIndicator  => Trend switch
    {
        MemoryTrend.Rising  => "↑",
        MemoryTrend.Falling => "↓",
        _                   => "─",
    };
    public SolidColorBrush TrendBrush => Trend switch
    {
        MemoryTrend.Rising  => _red,
        MemoryTrend.Falling => _green,
        _                   => _gray,
    };

    private static readonly SolidColorBrush _red   = MakeBrush(0xFF, 0x6B, 0x6B);
    private static readonly SolidColorBrush _green  = MakeBrush(0x6E, 0xFF, 0x6E);
    private static readonly SolidColorBrush _gray   = MakeBrush(0x88, 0x88, 0x88);

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:F1} GB",
        >= 1024L * 1024        => $"{bytes / 1024.0 / 1024:F1} MB",
        >= 1024L               => $"{bytes / 1024.0:F1} KB",
        _                      => $"{bytes} B",
    };
}

public enum MemoryTrend { Stable, Rising, Falling }
