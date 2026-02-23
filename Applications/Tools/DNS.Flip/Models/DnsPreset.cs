namespace DnsFlip.Models;

public sealed class DnsPreset
{
    public string Name { get; set; } = "";
    public string Primary { get; set; } = "";
    public string Secondary { get; set; } = "";
    public string Icon { get; set; } = "🌐";

    public override string ToString() => $"{Icon} {Name} ({Primary})";

    public static List<DnsPreset> GetDefaults() =>
    [
        new() { Name = "DHCP (자동)", Primary = "", Secondary = "", Icon = "🔄" },
        new() { Name = "Cloudflare", Primary = "1.1.1.1", Secondary = "1.0.0.1", Icon = "☁️" },
        new() { Name = "Cloudflare (보안)", Primary = "1.1.1.2", Secondary = "1.0.0.2", Icon = "🛡️" },
        new() { Name = "Google DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4", Icon = "🔍" },
        new() { Name = "Quad9 (보안)", Primary = "9.9.9.9", Secondary = "149.112.112.112", Icon = "🔒" },
        new() { Name = "OpenDNS", Primary = "208.67.222.222", Secondary = "208.67.220.220", Icon = "🌐" },
        new() { Name = "AdGuard DNS", Primary = "94.140.14.14", Secondary = "94.140.15.15", Icon = "🚫" },
    ];
}
