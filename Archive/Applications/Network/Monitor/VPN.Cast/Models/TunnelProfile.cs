namespace VpnCast.Models;

public enum TunnelType { WireGuard, OpenVPN }

public enum TunnelStatus { Disconnected, Connected, Connecting, Error }

public class TunnelProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public TunnelType Type { get; set; }
    public string ConfigPath { get; set; } = "";
    public string? ServerAddress { get; set; }
    public TunnelStatus Status { get; set; } = TunnelStatus.Disconnected;
    public DateTime? ConnectedAt { get; set; }

    public string TypeLabel => Type == TunnelType.WireGuard ? "WireGuard" : "OpenVPN";
    public string TypeIcon  => Type == TunnelType.WireGuard ? "🔒" : "🛡";
}
