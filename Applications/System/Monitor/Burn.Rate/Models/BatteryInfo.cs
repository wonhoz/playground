namespace BurnRate.Models;

public enum ChargingStatus { Unknown, Discharging, Charging, FullyCharged, NotPresent }

public class BatteryInfo
{
    public int    ChargePercent       { get; init; }
    public int    DesignCapMwh        { get; init; }
    public int    FullChargeCapMwh    { get; init; }
    public int    CycleCount         { get; init; }
    public int    EstRunTimeMinutes   { get; init; }   // -1 = unknown
    public ChargingStatus Status     { get; init; }
    public DateTime Timestamp        { get; init; } = DateTime.Now;

    /// <summary>설계 용량 대비 실충전 용량 (%)</summary>
    public int HealthPercent =>
        DesignCapMwh > 0
            ? Math.Min(100, (int)(FullChargeCapMwh * 100.0 / DesignCapMwh))
            : -1;

    public bool IsCharging   => Status == ChargingStatus.Charging || Status == ChargingStatus.FullyCharged;
    public bool IsOnAc       => Status != ChargingStatus.Discharging && Status != ChargingStatus.Unknown;
    public bool NeedsReplace => HealthPercent >= 0 && HealthPercent < 80;
}

public record ChargePoint(DateTime Time, int Percent, bool IsCharging);

public class SessionRecord
{
    public DateTime StartTime  { get; init; }
    public DateTime EndTime    { get; set; }
    public int      StartPct   { get; init; }
    public int      EndPct     { get; set; }
    public bool     WasCharging { get; init; }

    public int    DeltaPct     => EndPct - StartPct;
    public double DurationMin  => (EndTime - StartTime).TotalMinutes;
    public string Summary =>
        WasCharging
            ? $"+{DeltaPct}%  ({DurationMin:F0}분 충전)"
            : $"{DeltaPct}%  ({DurationMin:F0}분 방전)";
}
