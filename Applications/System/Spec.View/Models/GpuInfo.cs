namespace SpecView.Models;

public class GpuInfo
{
    public string Name                  { get; set; } = "";
    public ulong  AdapterRamBytes       { get; set; }
    public string DriverVersion         { get; set; } = "";
    public string DriverDate            { get; set; } = "";
    public uint   CurrentRefreshRate    { get; set; }
    public string VideoModeDescription  { get; set; } = "";
    public string AdapterCompatibility  { get; set; } = "";

    public string VramDisplay => AdapterRamBytes == 0
        ? "N/A"
        : $"{AdapterRamBytes / (1024.0 * 1024 * 1024):F1} GB";
}
