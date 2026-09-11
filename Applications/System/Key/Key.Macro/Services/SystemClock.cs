using System.Diagnostics;
using System.Security.Principal;

namespace KeyMacro.Services;

/// <summary>Windows 시스템 시각 변경 — 필요한 순간에만 권한을 올린다</summary>
public static class SystemClock
{
    private const string ElevateSwitch = "--apply-time-offset";

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemTime(ref SYSTEMTIME time);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
        ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previous, IntPtr returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    /// <summary>
    /// Win32 레이아웃은 PrivilegeCount(0) · Luid(4) · Attributes(12) 로 패딩이 없다.
    /// LUID 를 long 하나로 두면 8바이트 정렬 때문에 4바이트가 밀려 특권 활성화가 조용히 실패한다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES
    {
        public uint Count;
        public LUID Luid;
        public uint Attributes;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>권한 상승된 자식이 결과를 남기는 곳 (종료 코드만으로는 원인을 알 수 없으므로)</summary>
    private static string ResultPath => Path.Combine(
        Path.GetDirectoryName(MacroStore.FilePath)!, "timesync-result.txt");

    /// <summary>현재 프로세스가 관리자 권한으로 실행 중인지</summary>
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// 현재 시각에서 offset 만큼 옮긴 값으로 시스템 시계를 맞춘다.
    /// 권한이 없으면 이 순간에만 관리자로 자신을 다시 띄워 처리한다 (UAC 대기 시간이 오차로 남지 않도록 절대 시각이 아닌 '차이'를 넘긴다).
    /// </summary>
    public static (bool ok, string message) Apply(TimeSpan offset)
    {
        if (IsElevated)
        {
            var (ok, error) = SetNow(DateTime.UtcNow + offset);
            return ok ? (true, "PC 시간을 맞췄습니다") : (false, error);
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return (false, "실행 파일 경로를 찾을 수 없습니다");

            TryDelete(ResultPath);

            var info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"{ElevateSwitch} {offset.TotalMilliseconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}",
                UseShellExecute = true,
                Verb = "runas",           // 이 호출에서만 권한 상승 요청
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(info);
            if (process is null) return (false, "권한 상승에 실패했습니다");

            if (!process.WaitForExit(60000))
                return (false, "권한 상승된 프로세스가 응답하지 않습니다");

            // 자식이 남긴 상세 결과를 우선 사용 (종료 코드는 보조)
            var detail = ReadResult();
            if (detail is not null)
                return detail.Value.ok
                    ? (true, "관리자 권한으로 PC 시간을 맞췄습니다")
                    : (false, detail.Value.message);

            return process.ExitCode == 0
                ? (true, "관리자 권한으로 PC 시간을 맞췄습니다")
                : (false, $"시간 변경에 실패했습니다 (코드 {process.ExitCode})");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, "관리자 권한 요청이 취소되었습니다");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>권한 상승된 자식 프로세스로 실행됐는지 확인하고, 맞다면 시간만 바꾸고 종료 코드를 돌려준다</summary>
    public static int? HandleElevatedRequest(string[] args)
    {
        if (args.Length < 2 || args[0] != ElevateSwitch) return null;

        if (!double.TryParse(args[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double ms))
        {
            WriteResult(false, "보정값을 읽지 못했습니다");
            return 2;
        }

        var (ok, error) = SetNow(DateTime.UtcNow + TimeSpan.FromMilliseconds(ms));
        WriteResult(ok, error);
        return ok ? 0 : 1;
    }

    private static (bool ok, string error) SetNow(DateTime utc)
    {
        var (granted, privilegeError) = EnablePrivilege("SeSystemtimePrivilege");
        if (!granted) return (false, privilegeError);

        var st = new SYSTEMTIME
        {
            Year = (ushort)utc.Year,
            Month = (ushort)utc.Month,
            DayOfWeek = (ushort)utc.DayOfWeek,
            Day = (ushort)utc.Day,
            Hour = (ushort)utc.Hour,
            Minute = (ushort)utc.Minute,
            Second = (ushort)utc.Second,
            Milliseconds = (ushort)utc.Millisecond
        };

        if (SetSystemTime(ref st)) return (true, "");

        int code = Marshal.GetLastWin32Error();
        return (false, code == 1314
            ? "시간 변경 권한이 없습니다 (SE_PRIVILEGE_NOT_HELD)"
            : $"SetSystemTime 실패 (오류 코드 {code})");
    }

    /// <summary>관리자여도 기본은 꺼져 있는 특권을 켠다</summary>
    private static (bool ok, string error) EnablePrivilege(string name)
    {
        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020, TOKEN_QUERY = 0x0008;
        const uint SE_PRIVILEGE_ENABLED = 0x0002;
        const int ERROR_NOT_ALL_ASSIGNED = 1300;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return (false, $"프로세스 토큰을 열지 못했습니다 (오류 {Marshal.GetLastWin32Error()})");

        try
        {
            if (!LookupPrivilegeValue(null, name, out var luid))
                return (false, $"{name} 을(를) 찾지 못했습니다 (오류 {Marshal.GetLastWin32Error()})");

            var privileges = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            if (!AdjustTokenPrivileges(token, false, ref privileges, Marshal.SizeOf<TOKEN_PRIVILEGES>(),
                    IntPtr.Zero, IntPtr.Zero))
                return (false, $"권한 조정에 실패했습니다 (오류 {Marshal.GetLastWin32Error()})");

            // AdjustTokenPrivileges 는 성공을 반환하면서도 일부만 적용됐음을 여기로 알린다
            int code = Marshal.GetLastWin32Error();
            return code == ERROR_NOT_ALL_ASSIGNED
                ? (false, "이 계정에는 시간 변경 권한이 없습니다")
                : (true, "");
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static void WriteResult(bool ok, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            File.WriteAllText(ResultPath, ok ? "OK" : "ERR|" + message);
        }
        catch
        {
            // 결과 파일은 진단용이라 실패해도 흐름에 영향을 주지 않는다
        }
    }

    private static (bool ok, string message)? ReadResult()
    {
        try
        {
            if (!File.Exists(ResultPath)) return null;

            var text = File.ReadAllText(ResultPath).Trim();
            TryDelete(ResultPath);

            if (text == "OK") return (true, "");
            if (text.StartsWith("ERR|")) return (false, text[4..]);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 지우지 못해도 다음 실행에서 덮어쓴다 */ }
    }
}
