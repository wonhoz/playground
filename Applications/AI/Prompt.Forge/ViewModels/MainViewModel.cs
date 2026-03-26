using System.Windows.Threading;

namespace Prompt.Forge.ViewModels;

sealed class MainViewModel : INotifyPropertyChanged
{
    readonly Database _db;
    DispatcherTimer? _searchDebounce;

    // ── 목록 ─────────────────────────────────────────────────────────────────
    public ObservableCollection<PromptItem> Items  { get; } = [];
    public ObservableCollection<string>     Tags   { get; } = [];
    public ObservableCollection<string>     Services { get; } = [];

    PromptItem? _selected;
    public PromptItem? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected != null;

    // ── 필터 ─────────────────────────────────────────────────────────────────
    string _search = "";
    public string Search
    {
        get => _search;
        set
        {
            _search = value;
            OnPropertyChanged();
            // 300ms 디바운싱 — 타이핑 중 연속 DB 쿼리 방지
            if (_searchDebounce == null)
            {
                _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); Refresh(); };
            }
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
    }

    string? _filterTag;
    public string? FilterTag
    {
        get => _filterTag;
        set { _filterTag = value; OnPropertyChanged(); Refresh(); }
    }

    string? _filterService;
    public string? FilterService
    {
        get => _filterService;
        set { _filterService = value; OnPropertyChanged(); Refresh(); }
    }

    bool _favOnly;
    public bool FavOnly
    {
        get => _favOnly;
        set { _favOnly = value; OnPropertyChanged(); Refresh(); }
    }

    string _sortOrder = "updated";
    public string SortOrder
    {
        get => _sortOrder;
        set { _sortOrder = value; OnPropertyChanged(); Refresh(); }
    }

    // ── 상태 ─────────────────────────────────────────────────────────────────
    string _statusText = "준비";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    public MainViewModel(Database db)
    {
        _db = db;
        Refresh();
    }

    public void IncrementUseCount(int id)
    {
        _db.IncrementUseCount(id);
        var item = Items.FirstOrDefault(x => x.Id == id);
        if (item != null) item.UseCount++;
    }

    public void Refresh()
    {
        var results = _db.Search(
            string.IsNullOrWhiteSpace(_search) ? "" : _search,
            _filterTag,
            _filterService,
            _favOnly ? true : null,
            _sortOrder);

        Items.Clear();
        foreach (var r in results) Items.Add(r);

        // 태그/서비스 콤보 갱신
        var tags = _db.GetAllTags();
        Tags.Clear();
        Tags.Add("모든 태그");
        foreach (var t in tags) Tags.Add(t);

        var svcs = _db.GetAllServices();
        Services.Clear();
        Services.Add("모든 서비스");
        foreach (var s in svcs) Services.Add(s);

        StatusText = $"총 {Items.Count:N0}개";
    }

    public PromptItem CreateNew()
    {
        var p = new PromptItem { Title = "새 프롬프트", Content = "" };
        int id = _db.Insert(p);
        p.Id = id;
        Refresh();
        Selected = Items.FirstOrDefault(x => x.Id == id);
        return p;
    }

    public void Save(PromptItem p)
    {
        p.UpdatedAt = DateTime.UtcNow;
        _db.Update(p);
        Refresh();
        Selected = Items.FirstOrDefault(x => x.Id == p.Id);
        StatusText = $"저장 완료: {p.Title}";
    }

    public void Delete(PromptItem p)
    {
        var currentIndex = Items.IndexOf(p);
        _db.Delete(p.Id);
        Selected = null;
        Refresh();
        // 삭제 후 인접 항목 자동 선택
        if (Items.Count > 0)
            Selected = Items[Math.Min(currentIndex, Items.Count - 1)];
        StatusText = "삭제 완료";
    }

    public void ToggleFavorite(PromptItem p)
    {
        _db.ToggleFavorite(p.Id);
        Refresh();
        Selected = Items.FirstOrDefault(x => x.Id == p.Id);
    }

    public void Duplicate(PromptItem p)
    {
        var copy = new PromptItem
        {
            Title   = p.Title + " (복사)",
            Content = p.Content,
            Tags    = p.Tags,
            Service = p.Service,
            Notes   = p.Notes
        };
        int id = _db.Insert(copy);
        copy.Id = id;
        Refresh();
        Selected = Items.FirstOrDefault(x => x.Id == id);
        StatusText = $"복제 완료: {copy.Title}";
    }

    /// 현재 버전을 히스토리로 복사 후 새 버전 저장
    public void SaveAsNewVersion(PromptItem p)
    {
        var old = _db.GetById(p.Id);
        if (old == null) return;

        // 기존 내용을 자식(히스토리) 레코드로 복사
        var hist = new PromptItem
        {
            Title     = old.Title,
            Content   = old.Content,
            Tags      = old.Tags,
            Service   = old.Service,
            IsFavorite = false,
            Version   = old.Version,
            Notes     = old.Notes,
            ParentId  = old.Id
        };
        _db.Insert(hist);

        // 현재 레코드를 새 버전으로 업데이트
        p.Version++;
        Save(p);
        StatusText = $"v{p.Version} 저장 완료";
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
