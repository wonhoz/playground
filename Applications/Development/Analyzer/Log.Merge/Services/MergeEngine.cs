namespace LogMerge.Services;

/// <summary>여러 소스의 로그 항목을 타임스탬프 기준으로 병합</summary>
public class MergeEngine
{
    private int _nextId;

    public LogEntry CreateEntry(LogSource source, string rawLine)
    {
        var entry = new LogEntry
        {
            Id             = System.Threading.Interlocked.Increment(ref _nextId),
            Source         = source,
            Raw            = rawLine,
            Timestamp      = TimestampParser.ParseTimestamp(rawLine),
            Level          = TimestampParser.ParseLevel(rawLine),
            CorrelationIds = TimestampParser.ExtractCorrelationIds(rawLine),
        };
        return entry;
    }

    /// <summary>두 정렬된 리스트를 타임스탬프 기준으로 병합 (없는 항목은 뒤로)</summary>
    public static List<LogEntry> MergeSorted(IEnumerable<LogEntry> existing, IEnumerable<LogEntry> incoming)
    {
        var list = new List<LogEntry>(existing);
        foreach (var entry in incoming)
            InsertSorted(list, entry);
        return list;
    }

    /// <summary>정렬된 리스트에 이진 삽입</summary>
    public static void InsertSorted(List<LogEntry> list, LogEntry entry)
    {
        if (list.Count == 0 || !entry.Timestamp.HasValue)
        {
            list.Add(entry);
            return;
        }

        int lo = 0, hi = list.Count;
        var ts = entry.Timestamp.Value;

        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            var midTs = list[mid].Timestamp;
            if (!midTs.HasValue || midTs.Value <= ts)
                lo = mid + 1;
            else
                hi = mid;
        }

        list.Insert(lo, entry);
    }
}
