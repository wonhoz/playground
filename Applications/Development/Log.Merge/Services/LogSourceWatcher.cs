namespace LogMerge.Services;

/// <summary>단일 로그 소스 파일을 tail -f 방식으로 감시</summary>
public sealed class LogSourceWatcher : IDisposable
{
    public event Action<LogSource, IReadOnlyList<string>>? LinesReceived;

    private readonly LogSource         _source;
    private long                        _position;
    private FileSystemWatcher?          _watcher;
    private readonly object             _lock = new();
    private bool                        _disposed;

    public LogSourceWatcher(LogSource source)
    {
        _source = source;
    }

    public List<string> ReadInitialAndStart(int maxLines = 100_000)
    {
        var all = new List<string>(4096);

        try
        {
            using var fs = new FileStream(
                _source.FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);

            string? line;
            while ((line = sr.ReadLine()) != null)
                all.Add(line);

            _position = fs.Length;
        }
        catch { }

        StartWatcher();

        var start = Math.Max(0, all.Count - maxLines);
        return all.GetRange(start, all.Count - start);
    }

    private void StartWatcher()
    {
        try
        {
            var dir  = Path.GetDirectoryName(_source.FilePath) ?? ".";
            var file = Path.GetFileName(_source.FilePath);

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
        }
        catch { }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            if (_disposed) return;
            ReadNewLines();
        }
    }

    private void ReadNewLines()
    {
        try
        {
            using var fs = new FileStream(
                _source.FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (fs.Length < _position) _position = 0; // 로그 로테이션 감지

            fs.Seek(_position, SeekOrigin.Begin);
            using var sr = new StreamReader(fs,
                detectEncodingFromByteOrderMarks: _position == 0,
                leaveOpen: true);

            var newLines = new List<string>();
            string? line;
            while ((line = sr.ReadLine()) != null)
                newLines.Add(line);

            _position = fs.Position;

            if (newLines.Count > 0)
                LinesReceived?.Invoke(_source, newLines);
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_lock) _disposed = true;
        _watcher?.Dispose();
    }
}
