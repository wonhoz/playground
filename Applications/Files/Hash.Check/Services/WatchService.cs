namespace HashCheck.Services;

public class WatchEntry : INotifyPropertyChanged
{
    public string FilePath   { get; init; } = "";
    public string FileName   => Path.GetFileName(FilePath);
    public string BaseHash   { get; init; } = "";   // 스냅샷 시점 해시
    public HashAlgorithmKind Algorithm { get; init; }

    private string _currentHash = "";
    public string CurrentHash
    {
        get => _currentHash;
        set { _currentHash = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsChanged)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
    }

    private DateTime _lastChecked = DateTime.Now;
    public DateTime LastChecked
    {
        get => _lastChecked;
        set { _lastChecked = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastCheckedText)); }
    }

    public bool IsChanged   => CurrentHash.Length > 0 && !HashService.HashEquals(BaseHash, CurrentHash);
    public string StatusIcon  => CurrentHash.Length == 0 ? "—" : IsChanged ? "⚠" : "✔";
    public string StatusColor => CurrentHash.Length == 0 ? "#888899" : IsChanged ? "#EF4444" : "#22C55E";
    public string LastCheckedText => LastChecked.ToString("HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class WatchService : IDisposable
{
    private readonly HashService _hashSvc = new();
    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, WatchEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public event Action<WatchEntry>? FileChanged;

    public void StartWatch(string folderPath, string filter = "*.*")
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(folderPath, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
    }

    public void StopWatch()
    {
        if (_watcher != null) _watcher.EnableRaisingEvents = false;
    }

    public async Task AddSnapshotAsync(string filePath, HashAlgorithmKind algo = HashAlgorithmKind.SHA256)
    {
        var hash = await _hashSvc.ComputeAsync(filePath, algo);
        var entry = new WatchEntry
        {
            FilePath = filePath, BaseHash = hash, Algorithm = algo,
            CurrentHash = hash
        };
        _entries[filePath] = entry;
    }

    public async Task<List<WatchEntry>> VerifyAllAsync(IProgress<int>? progress = null)
    {
        var list = _entries.Values.ToList();
        int done = 0;
        foreach (var entry in list)
        {
            try
            {
                entry.CurrentHash = await _hashSvc.ComputeAsync(entry.FilePath, entry.Algorithm);
                entry.LastChecked = DateTime.Now;
            }
            catch { entry.CurrentHash = "(읽기 오류)"; }
            progress?.Report(++done * 100 / list.Count);
        }
        return list;
    }

    public List<WatchEntry> GetEntries() => [.. _entries.Values];

    public void Remove(string filePath) => _entries.Remove(filePath);
    public void Clear() => _entries.Clear();

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!_entries.TryGetValue(e.FullPath, out var entry)) return;
        await Task.Delay(200); // 파일 쓰기 완료 대기
        try
        {
            entry.CurrentHash = await _hashSvc.ComputeAsync(e.FullPath, entry.Algorithm);
            entry.LastChecked = DateTime.Now;
            if (entry.IsChanged) FileChanged?.Invoke(entry);
        }
        catch { }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_entries.Remove(e.OldFullPath, out var entry))
        {
            entry.CurrentHash = "(파일 이름 변경됨)";
            entry.LastChecked = DateTime.Now;
            FileChanged?.Invoke(entry);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        GC.SuppressFinalize(this);
    }
}
