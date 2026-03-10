namespace Sched.Cast.Models;

public enum TriggerKind { Once, Daily, Weekly, AtLogon, AtStartup }

/// <summary>COM TASK_STATE 값에 대응하는 자체 열거형</summary>
public enum SchedTaskState { Unknown = 0, Disabled = 1, Queued = 2, Ready = 3, Running = 4 }

public class TaskInfo
{
    public string          Name        { get; set; } = "";
    public string          Path        { get; set; } = "";
    public string          Description { get; set; } = "";
    public string          ExePath     { get; set; } = "";
    public string          Arguments   { get; set; } = "";
    public string          WorkDir     { get; set; } = "";
    public SchedTaskState  State       { get; set; }
    public DateTime?       LastRun     { get; set; }
    public DateTime?       NextRun     { get; set; }
    public int             LastResult  { get; set; }
    public bool            Enabled     { get; set; } = true;

    // 등록 시 사용
    public TriggerKind TriggerType { get; set; } = TriggerKind.Daily;
    public DateTime    StartTime   { get; set; } = DateTime.Today.AddHours(9);
    public TimeSpan    DailyTime   { get; set; } = TimeSpan.FromHours(9);
    public DayOfWeek   WeeklyDay   { get; set; } = DayOfWeek.Monday;
    public TimeSpan    WeeklyTime  { get; set; } = TimeSpan.FromHours(9);
    public bool        RunAsHighest { get; set; } = false;
}
