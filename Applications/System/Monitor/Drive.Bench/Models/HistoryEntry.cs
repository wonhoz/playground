namespace DriveBench.Models;

public class HistoryEntry
{
    public DateTime              Timestamp   { get; init; } = DateTime.Now;
    public string                DriveLetter { get; init; } = "";
    public string                DriveLabel  { get; init; } = "";
    public string                MediaType   { get; init; } = "";
    public long                  FileSizeBytes { get; init; }
    public List<BenchmarkResult> Results     { get; init; } = [];
}
