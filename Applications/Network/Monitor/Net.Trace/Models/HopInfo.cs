namespace Net.Trace.Models;

public class HopInfo : INotifyPropertyChanged
{
    string? _hostname;
    string? _country;
    string? _city;

    public int     HopNumber { get; init; }
    public string? Ip        { get; set; }
    public bool    IsTimeout { get; set; }

    public string? Hostname { get => _hostname; set { _hostname = value; Notify(); Notify(nameof(HostDisplay)); } }
    public string? Country  { get => _country;  set { _country = value;  Notify(); Notify(nameof(Location)); } }
    public string? City     { get => _city;     set { _city = value;     Notify(); Notify(nameof(Location)); } }

    public double? Latitude  { get; set; }
    public double? Longitude { get; set; }

    public double? RttMin { get; set; }
    public double? RttMax { get; set; }
    public double? RttAvg { get; set; }
    public int     Sent   { get; set; }
    public int     Lost   { get; set; }

    public string HostDisplay  => Hostname ?? Ip ?? "* * *";
    public string Location     => City != null && Country != null ? $"{City}, {Country}" : Country ?? (IsTimeout ? "—" : "조회 중...");
    public string RttText      => RttAvg.HasValue ? $"{RttAvg:F1} ms" : (IsTimeout ? "* * *" : "—");
    public string LossText     => Sent > 0 ? $"{(double)Lost / Sent * 100:F0}%" : "—";
    public bool   HasLocation  => Latitude.HasValue && Longitude.HasValue;

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new(p));
}
