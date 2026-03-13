namespace WiFiCast.Models;

public class WifiNetwork
{
    public string Ssid     { get; set; } = "";
    public string Bssid    { get; set; } = "";
    public int    Signal   { get; set; }   // 0-100 (link quality)
    public int    Rssi     { get; set; }   // dBm (negative)
    public int    Channel  { get; set; }
    public string Band     { get; set; } = "";
    public int    FreqMhz  { get; set; }

    // 2.4GHz: 채널 폭 ~22MHz → 인접 ±5채널과 간섭
    // 5GHz  : 20MHz non-overlapping (채널 간격 4)
    public int ChannelWidth => Band == "5GHz" ? 4 : 5;

    public override string ToString() => $"{Ssid} [{Bssid}] ch{Channel} {Signal}%";
}
