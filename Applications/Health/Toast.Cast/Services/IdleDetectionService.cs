using System.Runtime.InteropServices;

namespace ToastCast.Services;

public static class IdleDetectionService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>마지막 입력 이후 경과 시간(분)을 반환합니다.</summary>
    public static double GetIdleMinutes()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        var idleMs = (uint)Environment.TickCount - info.dwTime;
        return idleMs / 60000.0;
    }

    /// <summary>지정 임계값(분) 이상 유휴 상태인지 확인합니다.</summary>
    public static bool IsIdle(int thresholdMinutes) =>
        GetIdleMinutes() >= thresholdMinutes;
}
