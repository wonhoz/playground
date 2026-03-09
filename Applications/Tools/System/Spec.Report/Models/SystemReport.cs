namespace SpecReport.Models;

// ── 최상위 리포트 ─────────────────────────────────────────
public class SystemReport
{
    public string   ComputerName  { get; set; } = "";
    public string   UserName      { get; set; } = "";
    public DateTime CollectedAt   { get; set; }

    public CpuInfo               Cpu             { get; set; } = new();
    public List<RamSlotInfo>     RamSlots        { get; set; } = [];
    public long                  TotalRamBytes   { get; set; }
    public List<GpuInfo>         Gpus            { get; set; } = [];
    public List<StorageDriveInfo>Drives          { get; set; } = [];
    public OsInfo                Os              { get; set; } = new();
    public List<NetAdapterInfo>  NetworkAdapters { get; set; } = [];
    public List<InstalledApp>    Software        { get; set; } = [];
    public SecurityInfo          Security        { get; set; } = new();
}

// ── CPU ──────────────────────────────────────────────────
public class CpuInfo
{
    public string Name          { get; set; } = "";
    public string Manufacturer  { get; set; } = "";
    public int    PhysicalCores { get; set; }
    public int    LogicalCores  { get; set; }
    public double MaxClockGHz   { get; set; }
    public string Architecture  { get; set; } = "";
    public string Socket        { get; set; } = "";
}

// ── RAM ──────────────────────────────────────────────────
public class RamSlotInfo
{
    public string Slot          { get; set; } = "";
    public long   CapacityBytes { get; set; }
    public int    SpeedMHz      { get; set; }
    public string Manufacturer  { get; set; } = "";
    public string MemoryType    { get; set; } = "";
}

// ── GPU ──────────────────────────────────────────────────
public class GpuInfo
{
    public string Name          { get; set; } = "";
    public long   VramBytes     { get; set; }
    public string DriverVersion { get; set; } = "";
    public string DriverDate    { get; set; } = "";
    public int    CurrentWidth  { get; set; }
    public int    CurrentHeight { get; set; }
    public int    RefreshRate   { get; set; }
}

// ── 스토리지 ─────────────────────────────────────────────
public class StorageDriveInfo
{
    public string DriveLetter  { get; set; } = "";
    public string Label        { get; set; } = "";
    public string FileSystem   { get; set; } = "";
    public long   TotalBytes   { get; set; }
    public long   FreeBytes    { get; set; }
    public string MediaType    { get; set; } = "";  // NVMe SSD / SSD / HDD / Unknown
    public string Model        { get; set; } = "";
}

// ── OS ───────────────────────────────────────────────────
public class OsInfo
{
    public string   Caption          { get; set; } = "";
    public string   Version          { get; set; } = "";
    public string   BuildNumber      { get; set; } = "";
    public string   Architecture     { get; set; } = "";
    public DateTime InstallDate      { get; set; }
    public DateTime LastBoot         { get; set; }
    public string   RegisteredOwner  { get; set; } = "";
    public string   DotNetVersion    { get; set; } = "";
    public string   WindowsUpdateDate{ get; set; } = "";
}

// ── 네트워크 ─────────────────────────────────────────────
public class NetAdapterInfo
{
    public string       Name         { get; set; } = "";
    public string       Description  { get; set; } = "";
    public List<string> IpAddresses  { get; set; } = [];
    public string       MacAddress   { get; set; } = "";
    public List<string> DnsServers   { get; set; } = [];
    public string       Speed        { get; set; } = "";
    public bool         IsWireless   { get; set; }
}

// ── 설치 소프트웨어 ──────────────────────────────────────
public class InstalledApp
{
    public string Name        { get; set; } = "";
    public string Version     { get; set; } = "";
    public string Publisher   { get; set; } = "";
    public string InstallDate { get; set; } = "";
}

// ── 보안 ─────────────────────────────────────────────────
public class SecurityInfo
{
    public bool   DefenderEnabled  { get; set; }
    public string DefenderProduct  { get; set; } = "";
    public bool   FirewallEnabled  { get; set; }
    public string BitLockerStatus  { get; set; } = "Unknown"; // On / Off / Partial / Unknown
    public bool   AutoUpdateEnabled{ get; set; }
}

// ── 비교 결과 ─────────────────────────────────────────────
public class CompareResult
{
    public SystemReport          Old            { get; set; } = null!;
    public SystemReport          New            { get; set; } = null!;
    public List<FieldChange>     Changes        { get; set; } = [];
    public List<InstalledApp>    AddedSoftware  { get; set; } = [];
    public List<InstalledApp>    RemovedSoftware{ get; set; } = [];
    public List<SoftwareUpdate>  UpdatedSoftware{ get; set; } = [];
}

public record FieldChange(string Section, string Field, string OldValue, string NewValue);
public record SoftwareUpdate(string Name, string OldVersion, string NewVersion);
