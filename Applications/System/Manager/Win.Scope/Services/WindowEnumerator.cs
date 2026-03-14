using System.Runtime.InteropServices;
using System.Text;

namespace WinScope.Services;

public class WindowInfo
{
    public nint Handle { get; init; }
    public string Title { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsVisible { get; set; }
    public bool IsMinimized { get; set; }
    public int ZOrder { get; set; }   // 낮을수록 위 (0 = topmost)
    public byte Opacity { get; set; } = 255;
    public bool IsTopMost { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"[{ClassName}]" : Title;
}

public static class WindowEnumerator
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(nint hWnd);
    [DllImport("user32.dll")] private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(nint hWnd, out uint crKey, out byte bAlpha, out uint dwFlags);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x80000;
    private const long WS_EX_TOPMOST = 0x8;
    private const uint LWA_ALPHA = 0x2;

    private static readonly string[] _skipClasses =
    [
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman",
        "WorkerW", "DV2ControlHost", "MsgrIMEWindowClass",
        "SysShadow", "Button", "tooltips_class32"
    ];

    public static List<WindowInfo> GetWindows()
    {
        var list = new List<WindowInfo>();
        int z = 0;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var cls = new StringBuilder(256);
            GetClassName(hWnd, cls, cls.Capacity);
            var className = cls.ToString();

            if (_skipClasses.Contains(className)) return true;

            var len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var title = new StringBuilder(len + 1);
            GetWindowText(hWnd, title, title.Capacity);

            GetWindowThreadProcessId(hWnd, out uint pidUint);
            var pid = (int)pidUint;
            var procName = string.Empty;
            try { procName = System.Diagnostics.Process.GetProcessById(pid).ProcessName; }
            catch { }

            var exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            byte alpha = 255;
            if ((exStyle & WS_EX_LAYERED) != 0)
            {
                if (GetLayeredWindowAttributes(hWnd, out uint _, out byte a, out uint flags))
                    if ((flags & LWA_ALPHA) != 0) alpha = a;
            }

            list.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title.ToString(),
                ClassName = className,
                ProcessName = procName,
                ProcessId = pid,
                IsVisible = true,
                IsMinimized = IsIconic(hWnd),
                ZOrder = z++,
                Opacity = alpha,
                IsTopMost = (exStyle & WS_EX_TOPMOST) != 0
            });
            return true;
        }, nint.Zero);

        return list;
    }
}
