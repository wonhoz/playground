namespace SpecView.Models;

public class HardwareData
{
    public DateTime        ScannedAt    { get; set; } = DateTime.Now;
    public string          ComputerName { get; set; } = "";
    public string          OsCaption    { get; set; } = "";
    public string          OsVersion    { get; set; } = "";
    public CpuInfo?        Cpu          { get; set; }
    public MemoryInfo      Memory       { get; set; } = new();
    public List<GpuInfo>   Gpus         { get; set; } = [];
    public MotherboardInfo Board        { get; set; } = new();
    public List<StorageItem>  Drives    { get; set; } = [];
    public List<NetworkItem>  Networks  { get; set; } = [];
}
