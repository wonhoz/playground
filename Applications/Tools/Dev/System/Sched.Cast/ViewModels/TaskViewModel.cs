namespace Sched.Cast.ViewModels;

using Sched.Cast.Models;

public class TaskViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskInfo Info { get; }

    public TaskViewModel(TaskInfo info) => Info = info;

    public string Name        => Info.Name;
    public string Description => Info.Description;
    public bool   Enabled     => Info.Enabled;

    public string StateText => Info.State switch
    {
        SchedTaskState.Running  => "실행 중",
        SchedTaskState.Ready    => "준비",
        SchedTaskState.Disabled => "비활성",
        SchedTaskState.Queued   => "대기",
        _                       => Info.State.ToString(),
    };

    public string StateColor => Info.State switch
    {
        SchedTaskState.Running  => "#4ADE80",
        SchedTaskState.Ready    => "#60A5FA",
        SchedTaskState.Disabled => "#6B7280",
        _                       => "#9CA3AF",
    };

    public string LastRunText => Info.LastRun.HasValue
        ? Info.LastRun.Value.ToString("yyyy-MM-dd HH:mm:ss")
        : "없음";

    public string NextRunText => Info.NextRun.HasValue
        ? Info.NextRun.Value.ToString("yyyy-MM-dd HH:mm:ss")
        : "-";

    public string LastResultText => Info.LastResult == 0 ? "✓ 성공" : $"✗ {Info.LastResult:X8}";
    public string LastResultColor => Info.LastResult == 0 ? "#4ADE80" : "#F87171";

    public string ExeShort => Path.GetFileName(Info.ExePath);

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
