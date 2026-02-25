namespace DnsFlip.Models;

public sealed class DnsLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Adapter { get; set; } = "";
    public string PresetName { get; set; } = "";
    public string Dns { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}
