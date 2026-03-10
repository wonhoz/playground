namespace Timeline.Craft.ViewModels;

sealed class MainViewModel : INotifyPropertyChanged
{
    // ── 데이터 ────────────────────────────────────────────────────────────────
    public ObservableCollection<TimelineLane>  Lanes  { get; } = [];
    public ObservableCollection<TimelineEvent> Events { get; } = [];

    // ── 뷰포트 ────────────────────────────────────────────────────────────────
    DateTime _viewStart = DateTime.Today.AddDays(-7);
    double   _ppd       = 30;

    public DateTime ViewStart
    {
        get => _viewStart;
        set { _viewStart = value; Notify(); RequestRebuild(); }
    }

    public double PixelsPerDay
    {
        get => _ppd;
        set { _ppd = Math.Clamp(value, 2.0, 120.0); Notify(); Notify(nameof(ZoomLabel)); RequestRebuild(); }
    }

    public string ZoomLabel => _ppd switch
    {
        < 5   => "월 보기",
        < 15  => "주 보기",
        _     => "일 보기"
    };

    public double VisibleDays => 365;  // 충분히 넓게

    // ── 선택 ─────────────────────────────────────────────────────────────────
    TimelineEvent? _selectedEvent;
    public TimelineEvent? SelectedEvent
    {
        get => _selectedEvent;
        set { _selectedEvent = value; Notify(); Notify(nameof(HasSelection)); }
    }
    public bool HasSelection => _selectedEvent != null;

    // ── 프로젝트 제목 ─────────────────────────────────────────────────────────
    string _title = "새 타임라인";
    public string Title { get => _title; set { _title = value; Notify(); } }

    // ── 상태 ─────────────────────────────────────────────────────────────────
    string _status = "준비 — 드래그하여 이벤트를 생성하세요";
    public string StatusText { get => _status; set { _status = value; Notify(); } }

    // ── 재빌드 요청 이벤트 ────────────────────────────────────────────────────
    public event Action? RebuildRequested;
    public void RequestRebuild() => RebuildRequested?.Invoke();

    // ── 현재 파일 경로 ────────────────────────────────────────────────────────
    public string? CurrentPath { get; set; }

    // ── 레인 관리 ─────────────────────────────────────────────────────────────

    public void AddLane(string name = "")
    {
        Lanes.Add(new TimelineLane { Index = Lanes.Count, Name = string.IsNullOrEmpty(name) ? $"레인 {Lanes.Count + 1}" : name });
        RequestRebuild();
    }

    public void RemoveLane(TimelineLane lane)
    {
        var idx = lane.Index;
        Lanes.Remove(lane);
        foreach (var l in Lanes.Where(l => l.Index > idx)) l.Index--;
        foreach (var ev in Events.Where(e => e.LaneIndex >= idx && e.LaneIndex > 0)) ev.LaneIndex--;
        RequestRebuild();
    }

    // ── 이벤트 관리 ──────────────────────────────────────────────────────────

    int _nextId = 1;

    public TimelineEvent AddEvent(DateTime start, DateTime end, int laneIndex)
    {
        var ev = new TimelineEvent
        {
            Id         = _nextId++,
            Title      = "새 이벤트",
            Start      = start,
            End        = end > start ? end : start.AddDays(3),
            LaneIndex  = Math.Clamp(laneIndex, 0, Math.Max(0, Lanes.Count - 1)),
            Color      = DefaultColors[Events.Count % DefaultColors.Length]
        };
        Events.Add(ev);
        SelectedEvent = ev;
        StatusText = $"이벤트 추가됨: {ev.Title}";
        return ev;
    }

    public void DeleteSelected()
    {
        if (_selectedEvent == null) return;
        Events.Remove(_selectedEvent);
        SelectedEvent = null;
        RequestRebuild();
        StatusText = "이벤트 삭제됨";
    }

    static readonly string[] DefaultColors =
    [
        "#3B82F6","#10B981","#F59E0B","#EF4444","#8B5CF6",
        "#06B6D4","#EC4899","#F97316","#14B8A6","#6366F1"
    ];

    // ── 줌 ───────────────────────────────────────────────────────────────────

    public void ZoomIn()  => PixelsPerDay = Math.Min(120, _ppd * 1.25);
    public void ZoomOut() => PixelsPerDay = Math.Max(2,   _ppd / 1.25);

    // ── 프로젝트 직렬화 ──────────────────────────────────────────────────────

    public TimelineProject ToProject() => new()
    {
        Title        = _title,
        ViewStart    = _viewStart,
        PixelsPerDay = _ppd,
        Lanes        = [.. Lanes],
        Events       = [.. Events]
    };

    public void LoadProject(TimelineProject p)
    {
        Title        = p.Title;
        _viewStart   = p.ViewStart;
        _ppd         = p.PixelsPerDay;
        Lanes.Clear();  foreach (var l in p.Lanes)  Lanes.Add(l);
        Events.Clear(); foreach (var e in p.Events) Events.Add(e);
        _nextId      = Events.Count > 0 ? Events.Max(e => e.Id) + 1 : 1;
        SelectedEvent = null;
        RequestRebuild();
    }

    public void NewProject()
    {
        Title     = "새 타임라인";
        _viewStart = DateTime.Today.AddDays(-7);
        _ppd       = 30;
        Lanes.Clear();
        Events.Clear();
        SelectedEvent = null;
        CurrentPath   = null;
        AddLane("Phase 1");
        AddLane("Phase 2");
        StatusText = "새 프로젝트 생성됨";
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
