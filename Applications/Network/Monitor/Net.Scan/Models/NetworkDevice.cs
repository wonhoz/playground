namespace NetScan.Models;

public class NetworkDevice : INotifyPropertyChanged
{
    // ── 기본 정보 ─────────────────────────────────────────────────
    public string IpAddress  { get; init; } = "";
    public string MacAddress { get; set; } = "";
    public string Hostname   { get; set; } = "";
    public string Vendor     { get; set; } = "알 수 없음";

    private string _alias = "";
    public string Alias
    {
        get => _alias;
        set { _alias = value; Notify(); Notify(nameof(DisplayName)); }
    }

    public string DeviceType { get; set; } = "💻";

    // ── 상태 ──────────────────────────────────────────────────────
    private bool _isOnline;
    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; Notify(); Notify(nameof(StatusDot)); Notify(nameof(StatusColor)); }
    }

    private long _pingMs = -1;
    public long PingMs
    {
        get => _pingMs;
        set { _pingMs = value; Notify(); Notify(nameof(PingText)); }
    }

    public DateTime LastSeen { get; set; }

    // ── 포트 ──────────────────────────────────────────────────────
    public List<int> OpenPorts { get; set; } = [];

    // ── 핑 히스토리 (최대 40개) ──────────────────────────────────
    public readonly Queue<long> PingHistory = new();
    public void AddPingSample(long ms)
    {
        if (PingHistory.Count >= 40) PingHistory.Dequeue();
        PingHistory.Enqueue(ms);
    }

    // ── 표시용 프로퍼티 ──────────────────────────────────────────
    public string DisplayName => !string.IsNullOrEmpty(Alias) ? Alias
                               : !string.IsNullOrEmpty(Hostname) ? Hostname.Split('.')[0]
                               : IpAddress;

    public string StatusDot   => IsOnline ? "●" : "●";
    public string StatusColor => IsOnline ? "#22C55E" : "#444455";
    public string PingText    => PingMs < 0 ? "—" : $"{PingMs}ms";
    public string MacDisplay  => MacAddress.ToUpperInvariant();
    public string VendorDisplay => !string.IsNullOrEmpty(Alias)   ? $"{Alias} ({Vendor})"
                                 : !string.IsNullOrEmpty(Hostname) ? $"{Hostname.Split('.')[0]} · {Vendor}"
                                 : Vendor;

    // ── INotifyPropertyChanged ────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
