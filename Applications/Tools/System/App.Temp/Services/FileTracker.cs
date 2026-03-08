namespace AppTemp.Services;

public class FileTracker : IDisposable
{
    public event Action<ChangeRecord>? Changed;

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string>         _seen     = [];
    private readonly object                  _lock     = new();

    private static readonly string[] WatchDirs =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),      // %APPDATA%
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), // %LOCALAPPDATA%
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),// %ProgramData%
        Path.GetTempPath(),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        @"C:\Program Files",
        @"C:\Program Files (x86)",
    ];

    public void Start()
    {
        foreach (var dir in WatchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size,
                    InternalBufferSize  = 65536,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error   += OnError;

                _watchers.Add(watcher);
            }
            catch { /* 접근 권한 없는 경우 무시 */ }
        }
    }

    public void Stop()
    {
        foreach (var w in _watchers)
            w.EnableRaisingEvents = false;
    }

    private void Emit(ChangeRecord rec)
    {
        // 동일 경로 중복 이벤트 억제 (1초 내)
        var key = $"{rec.Type}|{rec.Path}|{rec.Timestamp:HH:mm:ss}";
        lock (_lock)
        {
            if (_seen.Contains(key)) return;
            _seen.Add(key);
        }
        Changed?.Invoke(rec);
    }

    private void OnCreated(object s, FileSystemEventArgs e) =>
        Emit(new ChangeRecord { Type = ChangeType.Created, Category = ChangeCategory.File, Path = e.FullPath });

    private void OnChanged(object s, FileSystemEventArgs e) =>
        Emit(new ChangeRecord { Type = ChangeType.Modified, Category = ChangeCategory.File, Path = e.FullPath });

    private void OnDeleted(object s, FileSystemEventArgs e) =>
        Emit(new ChangeRecord { Type = ChangeType.Deleted, Category = ChangeCategory.File, Path = e.FullPath });

    private void OnRenamed(object s, RenamedEventArgs e) =>
        Emit(new ChangeRecord { Type = ChangeType.Renamed, Category = ChangeCategory.File,
                                Path = e.FullPath, OldPath = e.OldFullPath });

    private void OnError(object s, ErrorEventArgs e) { /* 버퍼 오버플로 등 — 무시 */ }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
}
