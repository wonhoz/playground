using System.IO;
using System.Text;
using LogLens.Models;

namespace LogLens.Services;

public sealed class LogTailer : IDisposable
{
    private readonly string _filePath;
    private readonly FileSystemWatcher _watcher;
    private long _lastPosition;
    private int _lineCount;
    private readonly object _lock = new();
    private bool _disposed;

    public event Action<List<LogEntry>>? NewLines;

    public LogTailer(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath) ?? ".";
        var name = Path.GetFileName(filePath);

        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    public List<LogEntry> ReadAll()
    {
        var entries = new List<LogEntry>();
        if (!File.Exists(_filePath)) return entries;

        lock (_lock)
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            _lineCount = 0;
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                _lineCount++;
                entries.Add(LogParser.Parse(line, _lineCount));
            }
            _lastPosition = fs.Position;
        }

        return entries;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;

        List<LogEntry> newEntries;
        lock (_lock)
        {
            try
            {
                using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (fs.Length < _lastPosition)
                {
                    _lastPosition = 0;
                    _lineCount = 0;
                }

                fs.Seek(_lastPosition, SeekOrigin.Begin);
                using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                newEntries = [];
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    _lineCount++;
                    newEntries.Add(LogParser.Parse(line, _lineCount));
                }
                _lastPosition = fs.Position;
            }
            catch
            {
                return;
            }
        }

        if (newEntries.Count > 0)
            NewLines?.Invoke(newEntries);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
