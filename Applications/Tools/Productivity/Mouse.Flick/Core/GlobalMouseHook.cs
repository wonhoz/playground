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
    private const int  WH_MOUSE_LL    = 14;
    private const int  WM_RBUTTONDOWN = 0x0204;
    private const int  WM_RBUTTONUP   = 0x0205;
    private const int  WM_MOUSEMOVE   = 0x0200;

    private const uint INPUT_MOUSE           = 0;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP   = 0x0010;

    // ReplayRightClick이 주입한 이벤트임을 식별하는 고유 값 (재진입 방지)
    // LLMHF_INJECTED 전체 차단 금지 — 터치패드/마우스 드라이버도 SendInput을 사용하므로
    private static readonly IntPtr OurExtraInfo = new IntPtr(unchecked((int)0x4D465447));  // "MFTG"

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT  pt;
        public uint   mouseData;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    // INPUT(마우스 전용) — 64-bit Windows INPUT 구조체 = 40바이트
    // type(4) + padding(4) + MOUSEINPUT(32: dx,dy,mouseData,dwFlags,time,pad,dwExtraInfo)
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct MOUSE_INPUT
    {
        [FieldOffset(0)]  public uint   type;
        [FieldOffset(8)]  public int    dx;
        [FieldOffset(12)] public int    dy;
        [FieldOffset(16)] public uint   mouseData;
        [FieldOffset(20)] public uint   dwFlags;
        [FieldOffset(24)] public uint   time;
        [FieldOffset(32)] public IntPtr dwExtraInfo;
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, MOUSE_INPUT[] pInputs, int cbSize);

    // ── 필드 ──────────────────────────────────────────────────────────────────
    // _hookCallback 반드시 필드에 보관 — GC에 의한 조기 해제 방지 (핵심!)
    private readonly LowLevelMouseProc _hookCallback;
    private IntPtr   _hookId;
    private bool     _tracking;
    private bool     _gestureStarted;
    private Point    _startPoint;
    private Point    _downPoint;       // RBUTTONDOWN 원래 위치 (재생용)
    private readonly List<Point> _points = [];

    public int Threshold { get; set; } = 30;

    public event EventHandler<List<Point>>?               GestureStarted;
    public event EventHandler<Point>?                     GestureUpdated;
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

        // 우리가 ReplayRightClick으로 주입한 이벤트만 통과 (재진입 방지)
        // LLMHF_INJECTED 전체 차단 금지: 터치패드·마우스 드라이버도 SendInput을 사용하여
        // LLMHF_INJECTED가 설정될 수 있으므로 전체 차단 시 제스처가 인식되지 않음
        if (hs.dwExtraInfo == OurExtraInfo)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        switch (msg)
        {
            case WM_RBUTTONDOWN:
                // 실제 오른쪽 버튼 누름: 항상 차단하고 추적 시작
                // (나중에 제스처 없으면 ReplayRightClick으로 재생)
                _tracking       = true;
                _gestureStarted = false;
                _startPoint     = pt;
                _downPoint      = pt;
                _points.Clear();
                _points.Add(pt);
                return new IntPtr(1);   // 항상 차단 — 앱에 전달하지 않음

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
                        // 제스처 완료 → UP도 차단 (DOWN도 이미 차단됨)
                        _gestureStarted = false;
                        GestureCompleted?.Invoke(this,
                            new GestureCompletedEventArgs(new List<Point>(_points)));
                        return new IntPtr(1);
                    }
                    else
                    {
                        // 제스처 없음 → DOWN+UP을 재생하여 컨텍스트 메뉴 표시
                        ReplayRightClick();
                        return new IntPtr(1);
                    }
                }
                break;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// SendInput으로 RBUTTONDOWN + RBUTTONUP 이벤트를 주입한다.
    /// 주입된 이벤트는 LLMHF_INJECTED 플래그가 설정되므로 HookProc에서 통과 처리된다.
    /// </summary>
    private static void ReplayRightClick()
    {
        var inputs = new MOUSE_INPUT[2];
        inputs[0].type        = INPUT_MOUSE;
        inputs[0].dwFlags     = MOUSEEVENTF_RIGHTDOWN;
        inputs[0].dwExtraInfo = OurExtraInfo;   // 우리가 주입한 이벤트임을 표시
        inputs[1].type        = INPUT_MOUSE;
        inputs[1].dwFlags     = MOUSEEVENTF_RIGHTUP;
        inputs[1].dwExtraInfo = OurExtraInfo;
        SendInput(2, inputs, Marshal.SizeOf<MOUSE_INPUT>());
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose() => Uninstall();
}
