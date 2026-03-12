namespace SpecView.Models;

public class MemorySlot
{
    public string BankLabel     { get; set; } = "";
    public string DeviceLocator { get; set; } = "";
    public ulong  CapacityBytes { get; set; }
    public uint   SpeedMHz      { get; set; }
    public string MemoryType    { get; set; } = "";
    public string Manufacturer  { get; set; } = "";
    public string PartNumber    { get; set; } = "";
    public bool   IsEmpty       => CapacityBytes == 0;

    public string CapacityDisplay => IsEmpty
        ? "빈 슬롯"
        : $"{CapacityBytes / (1024.0 * 1024 * 1024):F0} GB";

    public string SpeedDisplay => SpeedMHz == 0 ? "" : $"{SpeedMHz} MHz";
}

public class MemoryInfo
{
    public List<MemorySlot> Slots      { get; set; } = [];
    public int              TotalSlots { get; set; }
    public int              UsedSlots  { get; set; }
    public ulong            TotalBytes { get; set; }
    public uint             MaxSpeedMHz{ get; set; }

    public string TotalDisplay => $"{TotalBytes / (1024.0 * 1024 * 1024):F0} GB";
    public string SlotDisplay  => $"{UsedSlots} / {TotalSlots} 슬롯 사용";
}
