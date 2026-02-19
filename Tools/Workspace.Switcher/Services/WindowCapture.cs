using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WorkspaceSwitcher.Models;

namespace WorkspaceSwitcher.Services;

/// <summary>현재 실행 중인 창/앱 목록 캡처 (워크스페이스 빠른 등록용)</summary>
public static class WindowCapture
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int  GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int  GetWindowText(IntPtr hWnd, StringBuilder sb, int maxCount);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>현재 표시 중인 앱 창 목록 반환</summary>
    public static List<WorkspaceApp> GetRunningApps()
    {
        var result = new List<WorkspaceApp>();
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (string.IsNullOrEmpty(title)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                var proc = Process.GetProcessById((int)pid);
                var exe  = proc.MainModule?.FileName;
                if (string.IsNullOrEmpty(exe) || !seen.Add(exe)) return true;

                var name = System.IO.Path.GetFileNameWithoutExtension(exe);
                result.Add(new WorkspaceApp { Name = name, Path = exe });
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return result.OrderBy(a => a.Name).ToList();
    }
}
