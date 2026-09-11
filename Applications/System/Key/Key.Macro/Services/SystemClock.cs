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
    private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
        ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previous, IntPtr returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint Count;
        public long Luid;
        public uint Attributes;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

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

            process.WaitForExit(20000);
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
            return 2;

        var (ok, _) = SetNow(DateTime.UtcNow + TimeSpan.FromMilliseconds(ms));
        return ok ? 0 : 1;
    }

    private static (bool ok, string error) SetNow(DateTime utc)
    {
        if (!EnablePrivilege("SeSystemtimePrivilege"))
            return (false, "시간 변경 권한을 얻지 못했습니다");

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

        return SetSystemTime(ref st)
            ? (true, "")
            : (false, $"SetSystemTime 실패 (오류 코드 {Marshal.GetLastWin32Error()})");
    }

    /// <summary>관리자여도 기본은 꺼져 있는 특권을 켠다</summary>
    private static bool EnablePrivilege(string name)
    {
        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020, TOKEN_QUERY = 0x0008;
        const uint SE_PRIVILEGE_ENABLED = 0x0002;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return false;

        try
        {
            if (!LookupPrivilegeValue(null, name, out long luid)) return false;

            var privileges = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero)) return false;

            return Marshal.GetLastWin32Error() == 0;   // ERROR_NOT_ALL_ASSIGNED 면 실패로 본다
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
