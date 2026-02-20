using System.Runtime.InteropServices;

namespace WindowPilot.Services;

/// <summary>
/// Win32 API를 통해 임의의 창 속성을 제어.
/// - Always-on-Top 토글
/// - 투명도 (WS_EX_LAYERED + SetLayeredWindowAttributes)
/// - 크기 프리셋 (1/4 / 1/3 / 1/2 화면 / 미니 200×150)
/// </summary>
public class WindowManager
{
    // ─── Win32 상수 ───────────────────────────────
    private static readonly IntPtr HWND_TOPMOST    = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST  = new(-2);
    private const uint SWP_NOMOVE    = 0x0002;
    private const uint SWP_NOSIZE    = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int  GWL_EXSTYLE   = -20;
    private const int  WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA     = 0x00000002;

    // ─── Win32 API ────────────────────────────────
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    // ─── 상태 추적 ───────────────────────────────
    private readonly Dictionary<IntPtr, bool>               _topmost   = [];
    private readonly Dictionary<IntPtr, int>                _opacity   = [];  // 10~100 (%)
    private readonly Dictionary<IntPtr, (int x,int y,int w,int h)> _origRect = [];

    // ─────────────────────────────────────────────
    // Always-on-Top 토글
    // ─────────────────────────────────────────────

    public (bool IsTop, string Msg) ToggleAlwaysOnTop()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (false, "창을 찾을 수 없습니다");

        bool nowTop = _topmost.TryGetValue(hwnd, out var cur) ? !cur : true;
        _topmost[hwnd] = nowTop;

        SetWindowPos(hwnd,
            nowTop ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        return (nowTop, nowTop ? "📌 항상 위: ON" : "📌 항상 위: OFF");
    }

    // ─────────────────────────────────────────────
    // 투명도 조절 (Ctrl+Shift+Wheel)
    // ─────────────────────────────────────────────

    public string AdjustOpacity(int delta)  // delta = +1(밝게) or -1(어둡게)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "창을 찾을 수 없습니다";

        int pct = _opacity.TryGetValue(hwnd, out var cur) ? cur : 100;
        pct = Math.Clamp(pct + delta * 10, 10, 100);
        _opacity[hwnd] = pct;

        EnsureLayered(hwnd);
        byte alpha = (byte)(pct * 255 / 100);
        SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);

        return $"🔆 투명도: {pct}%";
    }

    public string SetOpacityPct(int pct)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "";
        pct = Math.Clamp(pct, 10, 100);
        _opacity[hwnd] = pct;
        EnsureLayered(hwnd);
        SetLayeredWindowAttributes(hwnd, 0, (byte)(pct * 255 / 100), LWA_ALPHA);
        return $"🔆 투명도: {pct}%";
    }

    private static void EnsureLayered(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((style & WS_EX_LAYERED) == 0)
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
    }

    // ─────────────────────────────────────────────
    // 크기 프리셋
    // ─────────────────────────────────────────────

    public enum SizePreset { Quarter, Third, Half, Mini, Restore }

    public string ApplyPreset(SizePreset preset)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "창을 찾을 수 없습니다";

        // 화면 크기
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        int sw = screen.Width, sh = screen.Height;
        int sx = screen.Left,  sy = screen.Top;

        if (preset == SizePreset.Restore)
        {
            if (_origRect.TryGetValue(hwnd, out var r))
            {
                MoveWindow(hwnd, r.x, r.y, r.w, r.h, true);
                _origRect.Remove(hwnd);
                return "↩ 크기 복원";
            }
            return "저장된 크기 없음";
        }

        // 원본 크기 저장 (최초 1회)
        if (!_origRect.ContainsKey(hwnd) && GetWindowRect(hwnd, out var orig))
            _origRect[hwnd] = (orig.Left, orig.Top, orig.Right - orig.Left, orig.Bottom - orig.Top);

        (int x, int y, int w, int h, string label) = preset switch
        {
            SizePreset.Quarter => (sx,      sy,      sw / 2, sh / 2, "1/4 화면"),
            SizePreset.Third   => (sx,      sy,      sw / 3, sh,     "1/3 화면"),
            SizePreset.Half    => (sx,      sy,      sw / 2, sh,     "1/2 화면"),
            SizePreset.Mini    => (sx + sw - 220, sy + 40, 200, 150, "미니 (200×150)"),
            _                  => (sx, sy, sw, sh, ""),
        };

        MoveWindow(hwnd, x, y, w, h, true);
        return $"⬛ {label}";
    }
}
