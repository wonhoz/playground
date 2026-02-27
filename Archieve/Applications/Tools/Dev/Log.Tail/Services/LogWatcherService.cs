namespace LogTail.Services;

/// <summary>파일을 tail -f 방식으로 감시하며 새 줄을 이벤트로 전달</summary>
public sealed class LogWatcherService : IDisposable
{
    public event Action<IReadOnlyList<string>>? LinesReceived;

    private readonly string _filePath;
    private long             _position;
    private FileSystemWatcher? _watcher;
    private readonly object  _lock = new();
    private bool             _disposed;

    public LogWatcherService(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>초기 전체 로드 후 watcher 시작. 마지막 maxLines 줄 반환.</summary>
    public List<string> ReadInitialAndStart(int maxLines = 50_000)
    {
        var all = new List<string>(capacity: 4096);

        using (var fs = new FileStream(
            _filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        using (var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true))
        {
            string? line;
            while ((line = sr.ReadLine()) != null)
                all.Add(line);

            // StreamReader 내부 버퍼 소진 후 파일 끝 위치 기록
            _position = fs.Length;
        }

        StartWatcher();

        var start = Math.Max(0, all.Count - maxLines);
        return all.GetRange(start, all.Count - start);
    }

    private void StartWatcher()
    {
        var dir  = Path.GetDirectoryName(_filePath) ?? ".";
        var file = Path.GetFileName(_filePath);

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent; // 로그 로테이션 후 재생성 감지
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
                _filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // 파일이 잘렸거나 교체된 경우 처음부터 다시 읽기
            if (fs.Length < _position)
                _position = 0;

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
                LinesReceived?.Invoke(newLines);
        }
        catch (IOException)
        {
            // 파일 잠금 등 일시적 오류 — 다음 이벤트 때 재시도
        }
        catch (Exception)
        {
            // 예상치 못한 오류 무시
        }
    }

    public void Dispose()
    {
        lock (_lock)
            _disposed = true;
        _watcher?.Dispose();
    }
}
