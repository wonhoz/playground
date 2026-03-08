namespace BurnRate.Services;

public class BatteryService : IDisposable
{
    // ── 이벤트 ─────────────────────────────────────────────────
    public event Action<BatteryInfo>? BatteryUpdated;
    public event Action<ChargingStatus>? StatusChanged;  // AC plug/unplug
    public event Action? LowHealthDetected;              // health < 80%

    // ── 상태 ───────────────────────────────────────────────────
    public BatteryInfo?             Latest       { get; private set; }
    public List<ChargePoint>        History      { get; } = [];
    public List<SessionRecord>      Sessions     { get; } = [];
    public const int                MaxHistory   = 1440; // 24h @1분 샘플

    private ChargingStatus _prevStatus = ChargingStatus.Unknown;
    private SessionRecord? _currentSession;
    private System.Timers.Timer? _timer;
    private int _wmiDesignCap;
    private int _wmiFullCap;
    private int _wmiCycleCount;
    private bool _wmiLoaded;
    private bool _healthAlerted;

    public void Start()
    {
        LoadWmiCapacityData();
        Poll();
        _timer = new System.Timers.Timer(10_000); // 10초 폴링
        _timer.Elapsed += (_, _) => Poll();
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void Poll()
    {
        var info = BuildBatteryInfo();
        if (info == null) return;

        Latest = info;

        // 히스토리 추가 (최대 1440개)
        lock (History)
        {
            History.Add(new ChargePoint(info.Timestamp, info.ChargePercent, info.IsCharging));
            if (History.Count > MaxHistory) History.RemoveAt(0);
        }

        // AC 플러그 이벤트
        if (_prevStatus != ChargingStatus.Unknown && _prevStatus != info.Status)
        {
            UpdateSession(info);
            StatusChanged?.Invoke(info.Status);
        }
        else if (_currentSession == null)
        {
            StartSession(info);
        }

        _prevStatus = info.Status;

        // 배터리 건강도 경고 (1회)
        if (!_healthAlerted && info.NeedsReplace)
        {
            _healthAlerted = true;
            LowHealthDetected?.Invoke();
        }

        BatteryUpdated?.Invoke(info);
    }

    private BatteryInfo? BuildBatteryInfo()
    {
        try
        {
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            if (ps.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
                return null;

            int pct = ps.BatteryLifePercent >= 0 && ps.BatteryLifePercent <= 1.0f
                ? (int)(ps.BatteryLifePercent * 100)
                : -1;

            int runMin = ps.BatteryLifeRemaining > 0
                ? ps.BatteryLifeRemaining / 60
                : -1;

            var charging = ps.BatteryChargeStatus;
            ChargingStatus status;
            if (ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
            {
                status = charging.HasFlag(System.Windows.Forms.BatteryChargeStatus.Charging)
                    ? ChargingStatus.Charging
                    : ChargingStatus.FullyCharged;
            }
            else
            {
                status = pct >= 0 ? ChargingStatus.Discharging : ChargingStatus.Unknown;
            }

            // WMI 용량 데이터 (캐시)
            if (!_wmiLoaded) LoadWmiCapacityData();

            return new BatteryInfo
            {
                ChargePercent    = pct >= 0 ? pct : 0,
                DesignCapMwh     = _wmiDesignCap,
                FullChargeCapMwh = _wmiFullCap,
                CycleCount       = _wmiCycleCount,
                EstRunTimeMinutes = runMin,
                Status           = status
            };
        }
        catch { return null; }
    }

    private void LoadWmiCapacityData()
    {
        try
        {
            using var s1 = new ManagementObjectSearcher("SELECT DesignCapacity, FullChargeCapacity FROM Win32_Battery");
            foreach (ManagementObject o in s1.Get())
            {
                _wmiDesignCap = Convert.ToInt32(o["DesignCapacity"]);
                _wmiFullCap   = Convert.ToInt32(o["FullChargeCapacity"]);
                break;
            }
        }
        catch { }

        try
        {
            using var s2 = new ManagementObjectSearcher("SELECT CycleCount FROM Win32_PortableBattery");
            foreach (ManagementObject o in s2.Get())
            {
                _wmiCycleCount = Convert.ToInt32(o["CycleCount"]);
                break;
            }
        }
        catch { }

        _wmiLoaded = true;
    }

    private void StartSession(BatteryInfo info)
    {
        _currentSession = new SessionRecord
        {
            StartTime   = info.Timestamp,
            EndTime     = info.Timestamp,
            StartPct    = info.ChargePercent,
            EndPct      = info.ChargePercent,
            WasCharging = info.IsCharging
        };
    }

    private void UpdateSession(BatteryInfo info)
    {
        if (_currentSession != null)
        {
            _currentSession.EndTime = info.Timestamp;
            _currentSession.EndPct  = info.ChargePercent;
            if (Math.Abs(_currentSession.DeltaPct) >= 1 || _currentSession.DurationMin >= 5)
            {
                Sessions.Insert(0, _currentSession);
                if (Sessions.Count > 100) Sessions.RemoveAt(Sessions.Count - 1);
            }
        }
        StartSession(info);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
