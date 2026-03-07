namespace Key.Map.ViewModels;

sealed class MainViewModel : INotifyPropertyChanged
{
    // ── 프리셋 ───────────────────────────────────────────────────────────────
    public List<AppPreset>           AllPresets  { get; } = PresetLibrary.All;
    public ObservableCollection<ShortcutEntry> Shortcuts { get; } = [];

    AppPreset? _activePreset;
    public AppPreset? ActivePreset
    {
        get => _activePreset;
        set
        {
            _activePreset = value;
            OnPropertyChanged();
            LoadPreset(value);
        }
    }

    // ── 선택된 단축키 ─────────────────────────────────────────────────────────
    ShortcutEntry? _selected;
    public ShortcutEntry? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected != null;

    // ── 새 단축키 입력 ────────────────────────────────────────────────────────
    string _newKeys    = "";
    string _newDesc    = "";
    string _newCat     = "Other";
    public string NewKeys    { get => _newKeys;    set { _newKeys = value;    OnPropertyChanged(); } }
    public string NewDesc    { get => _newDesc;    set { _newDesc = value;    OnPropertyChanged(); } }
    public string NewCat     { get => _newCat;     set { _newCat = value;     OnPropertyChanged(); } }

    public List<string> Categories { get; } = ["File", "Edit", "View", "Run", "Navigate", "Other"];

    // ── 상태 ─────────────────────────────────────────────────────────────────
    string _status = "준비";
    public string StatusText { get => _status; set { _status = value; OnPropertyChanged(); } }

    public MainViewModel() => ActivePreset = AllPresets.FirstOrDefault();

    void LoadPreset(AppPreset? preset)
    {
        Shortcuts.Clear();
        if (preset == null) return;
        foreach (var s in preset.Shortcuts) Shortcuts.Add(s);
        Selected = null;
        StatusText = $"프리셋 로드: {preset.Name} — {preset.Shortcuts.Count}개 단축키";
    }

    public void AddShortcut()
    {
        if (string.IsNullOrWhiteSpace(_newKeys)) return;
        var entry = new ShortcutEntry
        {
            Keys        = _newKeys.Trim(),
            Description = _newDesc.Trim(),
            Category    = _newCat
        };
        Shortcuts.Add(entry);
        _activePreset?.Shortcuts.Add(entry);
        NewKeys = ""; NewDesc = "";
        Selected = entry;
        StatusText = $"단축키 추가됨: {entry.Keys}";
    }

    public void RemoveSelected()
    {
        if (_selected == null) return;
        _activePreset?.Shortcuts.Remove(_selected);
        Shortcuts.Remove(_selected);
        Selected = null;
        StatusText = "단축키 삭제됨";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
