namespace MemLens.Services;

/// <summary>PSAPI + WMI를 통한 프로세스 메모리 정보 수집</summary>
public static class ProcessMemoryService
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr hProcess,
        out PROCESS_MEMORY_COUNTERS_EX pmc,
        uint cb);

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    private struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint  cb;
        public uint  PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    public static List<ProcessInfo> GetAll()
    {
        var result = new List<ProcessInfo>();
        var procs  = Process.GetProcesses();

        foreach (var p in procs)
        {
            try
            {
                var info = GetInfo(p);
                if (info != null) result.Add(info);
            }
            catch { }
            finally { p.Dispose(); }
        }

        result.Sort((a, b) => b.PrivateBytes.CompareTo(a.PrivateBytes));
        return result;
    }

    public static ProcessInfo? GetInfo(Process p)
    {
        try
        {
            var pmc = new PROCESS_MEMORY_COUNTERS_EX { cb = 80 };
            GetProcessMemoryInfo(p.Handle, out pmc, pmc.cb);

            var info = new ProcessInfo
            {
                Pid          = p.Id,
                Name         = p.ProcessName,
                PrivateBytes = (long)pmc.PrivateUsage,
                WorkingSet   = (long)pmc.WorkingSetSize,
                VirtualBytes = p.VirtualMemorySize64,
                PagedPool    = (long)pmc.QuotaPagedPoolUsage,
                NonPagedPool = (long)pmc.QuotaNonPagedPoolUsage,
                PageFaults   = pmc.PageFaultCount,
            };

            // .NET GC 힙 정보 (관리 프로세스일 때)
            TryGetDotNetInfo(p, info);

            return info;
        }
        catch
        {
            return null;
        }
    }

    private static void TryGetDotNetInfo(Process p, ProcessInfo info)
    {
        try
        {
            // PerformanceCounter로 .NET GC 힙 조회
            using var gen0  = new PerformanceCounter(".NET CLR Memory", "Gen 0 heap size", p.ProcessName, readOnly: true);
            using var gen1  = new PerformanceCounter(".NET CLR Memory", "Gen 1 heap size", p.ProcessName, readOnly: true);
            using var gen2  = new PerformanceCounter(".NET CLR Memory", "Gen 2 heap size", p.ProcessName, readOnly: true);
            using var loh   = new PerformanceCounter(".NET CLR Memory", "Large Object Heap size", p.ProcessName, readOnly: true);

            info.GcGen0Size  = (long)gen0.RawValue;
            info.GcGen1Size  = (long)gen1.RawValue;
            info.GcGen2Size  = (long)gen2.RawValue;
            info.GcLohSize   = (long)loh.RawValue;
            info.GcTotalHeap = info.GcGen0Size + info.GcGen1Size + info.GcGen2Size + info.GcLohSize;
            info.IsDotNet    = info.GcTotalHeap > 0;
        }
        catch { }
    }

    public static bool TrimWorkingSet(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
        }
        catch { return false; }
    }
}
