namespace SysClean.Models;

public class InstalledProgram
{
    public string Name { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string InstallDate { get; init; } = "";
    public long SizeBytes { get; init; }
    public string SizeText => SizeBytes > 0 ? CleanTarget.FormatSize(SizeBytes) : "—";
    public string UninstallString { get; init; } = "";
    public string RegistryKey { get; init; } = "";
}
