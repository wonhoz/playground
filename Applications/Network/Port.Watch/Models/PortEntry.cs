namespace PortWatch.Models;

public class PortEntry
{
    public string Protocol    { get; set; } = "";
    public int    LocalPort   { get; set; }
    public string LocalAddr   { get; set; } = "";
    public string RemoteAddr  { get; set; } = "";
    public string State       { get; set; } = "";
    public int    Pid         { get; set; }
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public bool   IsFavorite  { get; set; }
}
