using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseFlick.Core;

internal static class WindowDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>현재 포그라운드 창의 프로세스 이름 (소문자)</summary>
    public static string GetForegroundProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return string.Empty;

            return Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
