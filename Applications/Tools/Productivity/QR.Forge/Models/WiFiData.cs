namespace QrForge.Models;

public enum WifiEncryption { WPA, WEP, None }

public class WiFiData
{
    public string SSID           { get; set; } = string.Empty;
    public string Password       { get; set; } = string.Empty;
    public WifiEncryption Crypto { get; set; } = WifiEncryption.WPA;

    public string ToWifiString()
    {
        var enc = Crypto switch
        {
            WifiEncryption.WPA  => "WPA",
            WifiEncryption.WEP  => "WEP",
            _                   => "nopass"
        };
        var pass = string.IsNullOrEmpty(Password) ? "" : EscapeWifi(Password);
        return $"WIFI:T:{enc};S:{EscapeWifi(SSID)};P:{pass};;";
    }

    private static string EscapeWifi(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\"", "\\\"");
}
