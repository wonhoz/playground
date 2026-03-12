namespace WinEvent.Services;

public sealed class EventLogService : IDisposable
{
    private EventLogWatcher? _watcher;
    private bool _disposed;

    public event Action<EventItem>? NewEventArrived;

    // ── 로그 소스 목록 ────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> BuiltInSources =
        ["Application", "System", "Security", "Setup"];

    // ── 이벤트 로드 ──────────────────────────────────────────────────

    /// <summary>지정된 로그 채널에서 최신 <paramref name="maxCount"/>개 이벤트를 읽어 반환합니다.</summary>
    public async Task<List<EventItem>> LoadEventsAsync(
        string logName, int maxCount = 2000,
        CancellationToken ct = default)
    {
        return await Task.Run(() => LoadEvents(logName, maxCount), ct);
    }

    private static List<EventItem> LoadEvents(string logName, int maxCount)
    {
        var results = new List<EventItem>(maxCount);
        var query = new EventLogQuery(logName, PathType.LogName)
        {
            ReverseDirection = true   // 최신 순
        };

        using var reader = new EventLogReader(query);
        int count = 0;
        while (count < maxCount)
        {
            using var record = reader.ReadEvent();
            if (record is null) break;
            var item = ToEventItem(record);
            if (item is not null) results.Add(item);
            count++;
        }
        return results;
    }

    /// <summary>EVTX 파일에서 이벤트를 읽어 반환합니다.</summary>
    public async Task<List<EventItem>> LoadFromFileAsync(
        string filePath, int maxCount = 5000,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<EventItem>(maxCount);
            var query = new EventLogQuery(filePath, PathType.FilePath)
            {
                ReverseDirection = true
            };
            using var reader = new EventLogReader(query);
            int count = 0;
            while (count < maxCount)
            {
                using var record = reader.ReadEvent();
                if (record is null) break;
                var item = ToEventItem(record);
                if (item is not null) results.Add(item);
                count++;
            }
            return results;
        }, ct);
    }

    // ── 실시간 감시 ──────────────────────────────────────────────────

    public void StartWatching(string logName)
    {
        StopWatching();
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName);
            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;
        }
        catch
        {
            // 접근 권한 부족 등의 경우 무시
        }
    }

    public void StopWatching()
    {
        if (_watcher is null) return;
        _watcher.Enabled = false;
        _watcher.EventRecordWritten -= OnEventRecordWritten;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord is null) return;
        var item = ToEventItem(e.EventRecord);
        if (item is not null) NewEventArrived?.Invoke(item);
    }

    // ── 변환 ─────────────────────────────────────────────────────────

    private static EventItem? ToEventItem(EventRecord record)
    {
        try
        {
            string fullMsg = "";
            try { fullMsg = record.FormatDescription() ?? ""; }
            catch { fullMsg = "(메시지 없음)"; }

            int level = record.Level ?? 4;

            return new EventItem
            {
                TimeCreated  = record.TimeCreated?.ToLocalTime() ?? DateTime.Now,
                Level        = level,
                LevelName    = record.LevelDisplayName ?? "",
                EventId      = record.Id,
                ProviderName = record.ProviderName ?? "",
                LogName      = record.LogName ?? "",
                MessageFull  = fullMsg,
                MessageShort = fullMsg.Length > 200
                    ? string.Concat(fullMsg.AsSpan(0, 200), "…")
                    : fullMsg,
                MachineName  = record.MachineName ?? "",
                RecordId     = record.RecordId ?? 0
            };
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopWatching();
        _disposed = true;
    }
}
