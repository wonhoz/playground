namespace SpecView.Models;

public class StorageItem
{
    public string Model         { get; set; } = "";
    public string InterfaceType { get; set; } = "";
    public ulong  SizeBytes     { get; set; }
    public string Status        { get; set; } = "";
    public string SmartStatus   { get; set; } = "알 수 없음";
    public bool   SmartOk       { get; set; } = true;
    public string MediaType     { get; set; } = "";

    public string SizeDisplay => SizeBytes switch
    {
        0 => "N/A",
        >= 1_000_000_000_000 => $"{SizeBytes / 1_000_000_000_000.0:F1} TB",
        _ => $"{SizeBytes / 1_000_000_000.0:F0} GB"
    };

    public string SmartColor => SmartOk ? "#10B981" : "#EF4444";
}
