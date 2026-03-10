using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Tray.Stats.Services;

sealed class StatsSnapshot
{
    public float CpuPercent   { get; init; }
    public float RamUsedGb    { get; init; }
    public float RamTotalGb   { get; init; }
    public float RamPercent   { get; init; }
    public float DiskPercent  { get; init; }
    public float NetSendKBs   { get; init; }   // KB/s
    public float NetRecvKBs   { get; init; }   // KB/s
    public float GpuPercent   { get; init; }
}

sealed class StatsCollector : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // PerformanceCounter
    readonly PerformanceCounter _cpu;
    readonly PerformanceCounter _disk;
    PerformanceCounter? _gpu;

    // 이력 (60초)
    const int HistLen = 60;
    public readonly CircularBuffer<float> CpuHistory  = new(HistLen);
    public readonly CircularBuffer<float> RamHistory  = new(HistLen);
    public readonly CircularBuffer<float> DiskHistory = new(HistLen);
    public readonly CircularBuffer<float> NetHistory  = new(HistLen);
    public readonly CircularBuffer<float> GpuHistory  = new(HistLen);

    // 네트워크 델타
    long _prevNetSend;
    long _prevNetRecv;
    DateTime _prevNetTime = DateTime.UtcNow;

    public StatsSnapshot Latest { get; private set; } = new();

    public StatsCollector()
    {
        _cpu  = new PerformanceCounter("Processor",    "% Processor Time", "_Total");
        _disk = new PerformanceCounter("PhysicalDisk", "% Disk Time",      "_Total");

        // GPU (NVIDIA/AMD/Intel — GPU Engine 카테고리)
        try
        {
            var cat   = new PerformanceCounterCategory("GPU Engine");
            var names = cat.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (names.Length > 0)
                _gpu = new PerformanceCounter("GPU Engine", "Utilization Percentage", names[0]);
        }
        catch { }

        // 네트워크 초기값
        SampleNetBytes(out _prevNetSend, out _prevNetRecv);

        // 첫 번째 CPU/Disk 샘플은 0을 반환하므로 pre-warm
        _ = _cpu.NextValue();
        _ = _disk.NextValue();
    }

    public StatsSnapshot Sample()
    {
        float cpu  = _cpu.NextValue();
        float disk = Math.Min(100f, _disk.NextValue());

        // RAM (Win32 GlobalMemoryStatusEx)
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        float ramTotalGb = 0, ramUsedGb = 0, ramPct = 0;
        if (GlobalMemoryStatusEx(ref ms))
        {
            ramTotalGb = ms.ullTotalPhys / 1024f / 1024f / 1024f;
            float availGb = ms.ullAvailPhys / 1024f / 1024f / 1024f;
            ramUsedGb  = ramTotalGb - availGb;
            ramPct     = ramTotalGb > 0 ? ramUsedGb / ramTotalGb * 100f : 0;
        }

        // 네트워크 속도
        SampleNetBytes(out long curSend, out long curRecv);
        double elapsed = (DateTime.UtcNow - _prevNetTime).TotalSeconds;
        float netSend = elapsed > 0 ? (float)((curSend - _prevNetSend) / elapsed / 1024f) : 0;
        float netRecv = elapsed > 0 ? (float)((curRecv - _prevNetRecv) / elapsed / 1024f) : 0;
        _prevNetSend = curSend;
        _prevNetRecv = curRecv;
        _prevNetTime = DateTime.UtcNow;

        float gpu = 0;
        try { if (_gpu != null) gpu = Math.Min(100f, _gpu.NextValue()); } catch { }

        var snap = new StatsSnapshot
        {
            CpuPercent  = Math.Min(100f, cpu),
            RamUsedGb   = ramUsedGb,
            RamTotalGb  = ramTotalGb,
            RamPercent  = Math.Min(100f, ramPct),
            DiskPercent = disk,
            NetSendKBs  = Math.Max(0, netSend),
            NetRecvKBs  = Math.Max(0, netRecv),
            GpuPercent  = gpu
        };

        CpuHistory.Push(snap.CpuPercent);
        RamHistory.Push(snap.RamPercent);
        DiskHistory.Push(snap.DiskPercent);
        NetHistory.Push(snap.NetSendKBs + snap.NetRecvKBs);
        GpuHistory.Push(snap.GpuPercent);

        Latest = snap;
        return snap;
    }

    static void SampleNetBytes(out long send, out long recv)
    {
        send = 0; recv = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            var stats = nic.GetIPv4Statistics();
            send += stats.BytesSent;
            recv += stats.BytesReceived;
        }
    }

    public void Dispose()
    {
        _cpu.Dispose();
        _disk.Dispose();
        _gpu?.Dispose();
    }
}

sealed class CircularBuffer<T>(int capacity)
{
    readonly T[] _buf = new T[capacity];
    int _head;
    public int Count { get; private set; }

    public void Push(T value)
    {
        _buf[_head] = value;
        _head = (_head + 1) % capacity;
        if (Count < capacity) Count++;
    }

    /// 인덱스 0 = 가장 최근, 큰 인덱스 = 오래된 값
    public T this[int index]
    {
        get
        {
            int i = (_head - 1 - index + capacity * 2) % capacity;
            return _buf[i];
        }
    }
}
