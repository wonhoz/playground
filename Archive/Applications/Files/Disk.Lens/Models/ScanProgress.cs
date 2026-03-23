namespace DiskLens.Models;

/// <summary>스캔 진행 상태 보고</summary>
public class ScanProgress
{
    public string CurrentPath { get; set; } = "";
    public int ScannedCount { get; set; }
}
