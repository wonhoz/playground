namespace SpecView.Models;

public class NetworkItem
{
    public string Name              { get; set; } = "";
    public string Description       { get; set; } = "";
    public string MACAddress        { get; set; } = "";
    public string ConnectionStatus  { get; set; } = "";
    public string Speed             { get; set; } = "";
    public bool   IsConnected       { get; set; }
    public string AdapterType       { get; set; } = "";

    public string StatusColor => IsConnected ? "#10B981" : "#64748B";
}
