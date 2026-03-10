namespace DriveBench.Models;

public class DriveItem
{
    public string Letter    { get; init; } = "";   // "C:"
    public string Label     { get; init; } = "";   // "Windows"
    public string MediaType { get; init; } = "";   // "SSD" / "HDD" / "NVMe" / "Drive"
    public long   TotalBytes{ get; init; }

    public string RootPath    => Letter + "\\";
    public string TotalText   => FormatSize(TotalBytes);
    public string DisplayName => $"{Letter}  {Label}  [{MediaType}  {TotalText}]";

    public override string ToString() => DisplayName;

    private static string FormatSize(long b) => b switch
    {
        >= 1099511627776L => $"{b / 1099511627776.0:F1} TB",
        >= 1073741824L    => $"{b / 1073741824.0:F1} GB",
        >= 1048576L       => $"{b / 1048576.0:F1} MB",
        _                 => $"{b} B"
    };
}
