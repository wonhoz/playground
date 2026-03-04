using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseFlick.Core;

internal sealed class GestureCompletedEventArgs : EventArgs
{
    public List<Point> Points { get; }
    public GestureCompletedEventArgs(List<Point> pts) => Points = pts;
}

internal sealed class GlobalMouseHook : IDisposable
{
    private const int WH_MOUSE_LL    = 14;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP   = 0x0205;
    private const int WM_MOUSEMOVE   = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint  mouseData;
        public uint  flags;
        public uint  time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // ── 필드 ──────────────────────────────────────────────────────────────────
    // _hookCallback 반드시 필드에 보관 — GC에 의한 조기 해제 방지 (핵심!)
    private readonly LowLevelMouseProc _hookCallback;
    private IntPtr   _hookId;
    private bool     _tracking;
    private bool     _gestureStarted;
    private Point    _startPoint;
    private readonly List<Point> _points = [];

    public int Threshold { get; set; } = 30;

    public event EventHandler<List<Point>>?            GestureStarted;
    public event EventHandler<Point>?                  GestureUpdated;
    public event EventHandler<GestureCompletedEventArgs>? GestureCompleted;

    public GlobalMouseHook()
    {
        _hookCallback = HookProc;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule  = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookCallback,
            GetModuleHandle(curModule.ModuleName!), 0);
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        var hs  = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        var pt  = new Point(hs.pt.X, hs.pt.Y);
        int msg = (int)wParam;

        switch (msg)
        {
            case WM_RBUTTONDOWN:
                _tracking       = true;
                _gestureStarted = false;
                _startPoint     = pt;
                _points.Clear();
                _points.Add(pt);
                break;

            case WM_MOUSEMOVE:
                if (_tracking)
                {
                    _points.Add(pt);
                    if (!_gestureStarted)
                    {
                        if (Distance(_startPoint, pt) >= Threshold)
                        {
                            _gestureStarted = true;
                            GestureStarted?.Invoke(this, new List<Point>(_points));
                        }
                    }
                    else
                    {
                        GestureUpdated?.Invoke(this, pt);
                    }
                }
                break;

            case WM_RBUTTONUP:
                if (_tracking)
                {
                    _tracking = false;
                    _points.Add(pt);
                    if (_gestureStarted)
                    {
                        _gestureStarted = false;
                        GestureCompleted?.Invoke(this,
                            new GestureCompletedEventArgs(new List<Point>(_points)));
                        return new IntPtr(1);  // 컨텍스트 메뉴 억제
                    }
                }
                break;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose() => Uninstall();
}
