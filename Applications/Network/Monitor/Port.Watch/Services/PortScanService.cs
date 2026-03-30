using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using PortWatch.Models;

namespace PortWatch.Services;

/// <summary>
/// Windows IPHelper API(iphlpapi.dll)를 직접 호출하여 TCP/UDP 포트 목록을 가져온다.
/// netstat 텍스트 파싱 대비 언어/환경 독립적이고 안정적.
/// </summary>
public static class PortScanService
{
    private const int  TCP_TABLE_OWNER_PID_ALL = 5;
    private const int  UDP_TABLE_OWNER_PID     = 1;
    private const uint AF_INET                 = 2;
    private const uint AF_INET6                = 23;

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(
        nint pTcpTable, ref uint dwOutBufLen, bool bOrder,
        uint ulAf, int TableClass, uint Reserved);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedUdpTable(
        nint pUdpTable, ref uint dwOutBufLen, bool bOrder,
        uint ulAf, int TableClass, uint Reserved);

    // IPv4 TCP 행: State·LocalAddr·LocalPort·RemoteAddr·RemotePort·PID
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW
    {
        public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort, Pid;
    }

    // IPv6 TCP 행
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId, LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] RemoteAddr;
        public uint RemoteScopeId, RemotePort, State, Pid;
    }

    // IPv4 UDP 행
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW
    {
        public uint LocalAddr, LocalPort, Pid;
    }

    // IPv6 UDP 행
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDP6ROW
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId, LocalPort, Pid;
    }

    // MIB_TCP_STATE 값 → 문자열 (인덱스 = 상태 코드)
    private static readonly string[] _tcpStates =
    [
        "",            // 0 unused
        "CLOSED",      // 1
        "LISTENING",   // 2
        "SYN_SENT",    // 3
        "SYN_RCVD",    // 4
        "ESTABLISHED", // 5
        "FIN_WAIT1",   // 6
        "FIN_WAIT2",   // 7
        "CLOSE_WAIT",  // 8
        "CLOSING",     // 9
        "LAST_ACK",    // 10
        "TIME_WAIT",   // 11
        "DELETE_TCB",  // 12
    ];

    public static Task<List<PortEntry>> ScanAsync() => Task.Run(Scan);

    private static List<PortEntry> Scan()
    {
        var entries = new List<PortEntry>();
        var cache   = new Dictionary<int, (string name, string path)>();

        ReadTcpTable<MIB_TCPROW> (AF_INET,  entries, cache, AddTcp4);
        ReadTcpTable<MIB_TCP6ROW>(AF_INET6, entries, cache, AddTcp6);
        ReadUdpTable<MIB_UDPROW> (AF_INET,  entries, cache, AddUdp4);
        ReadUdpTable<MIB_UDP6ROW>(AF_INET6, entries, cache, AddUdp6);

        return [.. entries.OrderBy(e => e.LocalPort)];
    }

    // ── 테이블 읽기 헬퍼 ─────────────────────────────────────────────

    private static void ReadTcpTable<T>(uint af, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache,
        Action<T, List<PortEntry>, Dictionary<int, (string, string)>> addRow) where T : struct
    {
        uint size = 0;
        GetExtendedTcpTable(nint.Zero, ref size, false, af, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size == 0) return;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetExtendedTcpTable(buf, ref size, true, af, TCP_TABLE_OWNER_PID_ALL, 0) != 0) return;
            int count   = Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<T>();
            for (int i = 0; i < count; i++)
                addRow(Marshal.PtrToStructure<T>(buf + 4 + i * rowSize)!, entries, cache);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static void ReadUdpTable<T>(uint af, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache,
        Action<T, List<PortEntry>, Dictionary<int, (string, string)>> addRow) where T : struct
    {
        uint size = 0;
        GetExtendedUdpTable(nint.Zero, ref size, false, af, UDP_TABLE_OWNER_PID, 0);
        if (size == 0) return;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetExtendedUdpTable(buf, ref size, true, af, UDP_TABLE_OWNER_PID, 0) != 0) return;
            int count   = Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<T>();
            for (int i = 0; i < count; i++)
                addRow(Marshal.PtrToStructure<T>(buf + 4 + i * rowSize)!, entries, cache);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    // ── 행 변환 ──────────────────────────────────────────────────────

    private static void AddTcp4(MIB_TCPROW r, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache)
    {
        var state  = r.State < _tcpStates.Length ? _tcpStates[r.State] : r.State.ToString();
        var remote = r.RemotePort == 0 ? ""
            : $"{new IPAddress(r.RemoteAddr)}:{NtoHS(r.RemotePort)}";
        var (name, path) = GetProcessInfo((int)r.Pid, cache);
        entries.Add(new PortEntry {
            Protocol = "TCP",  LocalPort = NtoHS(r.LocalPort),
            LocalAddr = new IPAddress(r.LocalAddr).ToString(),
            RemoteAddr = remote, State = state,
            Pid = (int)r.Pid, ProcessName = name, ProcessPath = path
        });
    }

    private static void AddTcp6(MIB_TCP6ROW r, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache)
    {
        var state  = r.State < _tcpStates.Length ? _tcpStates[r.State] : r.State.ToString();
        var remote = r.RemotePort == 0 ? ""
            : $"[{new IPAddress(r.RemoteAddr)}]:{NtoHS(r.RemotePort)}";
        var (name, path) = GetProcessInfo((int)r.Pid, cache);
        entries.Add(new PortEntry {
            Protocol = "TCP6", LocalPort = NtoHS(r.LocalPort),
            LocalAddr = new IPAddress(r.LocalAddr).ToString(),
            RemoteAddr = remote, State = state,
            Pid = (int)r.Pid, ProcessName = name, ProcessPath = path
        });
    }

    private static void AddUdp4(MIB_UDPROW r, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache)
    {
        var (name, path) = GetProcessInfo((int)r.Pid, cache);
        entries.Add(new PortEntry {
            Protocol = "UDP",  LocalPort = NtoHS(r.LocalPort),
            LocalAddr = new IPAddress(r.LocalAddr).ToString(),
            Pid = (int)r.Pid, ProcessName = name, ProcessPath = path
        });
    }

    private static void AddUdp6(MIB_UDP6ROW r, List<PortEntry> entries,
        Dictionary<int, (string, string)> cache)
    {
        var (name, path) = GetProcessInfo((int)r.Pid, cache);
        entries.Add(new PortEntry {
            Protocol = "UDP6", LocalPort = NtoHS(r.LocalPort),
            LocalAddr = new IPAddress(r.LocalAddr).ToString(),
            Pid = (int)r.Pid, ProcessName = name, ProcessPath = path
        });
    }

    // ── 유틸 ─────────────────────────────────────────────────────────

    // 네트워크 바이트 순서(빅엔디안) → 호스트 포트 번호
    private static int NtoHS(uint p) => ((int)(p & 0xFF) << 8) | (int)((p >> 8) & 0xFF);

    private static (string name, string path) GetProcessInfo(int pid,
        Dictionary<int, (string, string)> cache)
    {
        if (cache.TryGetValue(pid, out var info)) return info;
        try
        {
            var p = Process.GetProcessById(pid);
            string path = "";
            try { path = p.MainModule?.FileName ?? ""; } catch { }
            info = (p.ProcessName, path);
        }
        catch { info = ("System", ""); }
        cache[pid] = info;
        return info;
    }
}
