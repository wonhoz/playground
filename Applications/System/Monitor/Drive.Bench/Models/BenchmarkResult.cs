namespace DriveBench.Models;

public record BenchmarkResult
{
    public string TestName  { get; init; } = "";
    public string TestKey   { get; init; } = "";   // seq1m / seq128k / rnd4k_q1t1 / rnd4k_q8t8
    public double ReadMBps  { get; init; }
    public double WriteMBps { get; init; }
    public double ReadIOPS  { get; init; }
    public double WriteIOPS { get; init; }

    public string ReadMBpsText  => ReadMBps  > 0 ? $"{ReadMBps:F1}" : "—";
    public string WriteMBpsText => WriteMBps > 0 ? $"{WriteMBps:F1}" : "—";
    public string ReadIOPSText  => ReadIOPS  > 0 ? $"{ReadIOPS:N0}" : "—";
    public string WriteIOPSText => WriteIOPS > 0 ? $"{WriteIOPS:N0}" : "—";
}
