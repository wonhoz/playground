namespace Sched.Cast.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    readonly TaskSchedulerService _svc = new();

    public ObservableCollection<TaskViewModel> Tasks { get; } = new();

    TaskViewModel? _selected;
    public TaskViewModel? Selected
    {
        get => _selected;
        set { _selected = value; Notify(); Notify(nameof(HasSelection)); }
    }

    string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; Notify(); }
    }

    public bool HasSelection => _selected != null;

    // ── 로드 ────────────────────────────────────────────────────────

    public void Refresh()
    {
        Tasks.Clear();
        try
        {
            foreach (var info in _svc.GetTasks())
                Tasks.Add(new TaskViewModel(info));
            StatusText = $"SchedCast 작업 {Tasks.Count}개 로드됨";
        }
        catch (Exception ex)
        {
            StatusText = $"로드 실패: {ex.Message}";
        }
    }

    // ── 작업 제어 ────────────────────────────────────────────────────

    public void RunSelected()
    {
        if (_selected == null) return;
        try
        {
            _svc.RunTask(_selected.Name);
            StatusText = $"'{_selected.Name}' 실행 요청됨";
        }
        catch (Exception ex) { StatusText = $"실행 실패: {ex.Message}"; }
    }

    public void StopSelected()
    {
        if (_selected == null) return;
        try
        {
            _svc.StopTask(_selected.Name);
            StatusText = $"'{_selected.Name}' 중지 요청됨";
        }
        catch (Exception ex) { StatusText = $"중지 실패: {ex.Message}"; }
    }

    public void DeleteSelected()
    {
        if (_selected == null) return;
        try
        {
            _svc.DeleteTask(_selected.Name);
            Tasks.Remove(_selected);
            Selected   = null;
            StatusText = "작업 삭제됨";
        }
        catch (Exception ex) { StatusText = $"삭제 실패: {ex.Message}"; }
    }

    public void ToggleEnabled()
    {
        if (_selected == null) return;
        try
        {
            var newState = !_selected.Enabled;
            _svc.SetEnabled(_selected.Name, newState);
            _selected.Info.Enabled = newState;
            _selected.Refresh();
            StatusText = $"'{_selected.Name}' {(newState ? "활성화" : "비활성화")}됨";
        }
        catch (Exception ex) { StatusText = $"실패: {ex.Message}"; }
    }

    // ── 등록 ─────────────────────────────────────────────────────────

    public void Register(TaskInfo info)
    {
        try
        {
            _svc.RegisterTask(info);
            StatusText = $"'{info.Name}' 등록됨";
            Refresh();
        }
        catch (Exception ex) { StatusText = $"등록 실패: {ex.Message}"; }
    }

    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
