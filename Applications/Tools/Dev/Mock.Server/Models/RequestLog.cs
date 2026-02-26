namespace MockServer.Models;

public class RequestLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string   Method    { get; set; } = "";
    public string   Path      { get; set; } = "";
    public int      Status    { get; set; }
    public bool     Matched   { get; set; }
    public long     DelayMs   { get; set; }

    public string Display =>
        $"[{Timestamp:HH:mm:ss}]  {Method,-7} {Path,-35}  {Status}  " +
        $"{(Matched ? "✓" : "✗ (404)")}  {DelayMs}ms";
}
