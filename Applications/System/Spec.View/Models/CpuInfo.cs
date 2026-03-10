namespace SpecView.Models;

public class CpuInfo
{
    public string Name         { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Socket       { get; set; } = "";
    public int    Cores        { get; set; }
    public int    Threads      { get; set; }
    public double MaxClockMHz  { get; set; }
    public string L2Cache      { get; set; } = "";
    public string L3Cache      { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Description  { get; set; } = "";

    public string ClockDisplay => MaxClockMHz >= 1000
        ? $"{MaxClockMHz / 1000.0:F2} GHz"
        : $"{MaxClockMHz:F0} MHz";
}
