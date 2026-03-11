namespace MemLens.Services;

/// <summary>5분간 샘플링 후 지속 증가 추세 감지</summary>
public class MemoryLeakDetector
{
    private const int SampleWindow = 20;   // 샘플 수 (5분 = 15초×20)
    private const double RisingThreshold  = 0.05;  // 5% 증가
    private const double FallingThreshold = 0.03;  // 3% 감소

    private readonly Dictionary<int, Queue<long>> _history = [];

    public MemoryTrend Record(int pid, long privateBytes)
    {
        if (!_history.TryGetValue(pid, out var q))
        {
            q = new Queue<long>(SampleWindow);
            _history[pid] = q;
        }

        q.Enqueue(privateBytes);
        while (q.Count > SampleWindow)
            q.Dequeue();

        if (q.Count < 5) return MemoryTrend.Stable;

        var arr   = q.ToArray();
        var first = arr[0];
        var last  = arr[^1];

        if (first == 0) return MemoryTrend.Stable;

        double change = (double)(last - first) / first;

        return change > RisingThreshold
            ? MemoryTrend.Rising
            : change < -FallingThreshold
                ? MemoryTrend.Falling
                : MemoryTrend.Stable;
    }

    public void Remove(int pid) => _history.Remove(pid);

    public void Clear() => _history.Clear();
}
