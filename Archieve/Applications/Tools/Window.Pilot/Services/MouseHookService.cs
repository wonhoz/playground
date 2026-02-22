using System.Runtime.InteropServices;

namespace WindowPilot.Services;

/// <summary>
/// 저수준 마우스 훅(WH_MOUSE_LL)으로 Ctrl+Shift+휠 감지.
/// 휠 이벤트를 소비하지 않고 전달 시 다른 앱에 영향을 주지 않도록
/// 플래그 기반으로 구동함.
/// </summary>
public sealed class MouseHookService : IDisposable
{
    private const int WH_MOUSE_LL   = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int VK_CONTROL    = 0x11;
    private const int VK_SHIFT      = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int   mouseData;
        public int   flags;
        public int   time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern short  GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>Ctrl+Shift+휠 감지: +1 (위) / -1 (아래)</summary>
    public event Action<int>? WheelWithCtrlShift;

    private HookProc?  _proc;
    private IntPtr     _hook = IntPtr.Zero;

    public void Install()
    {
        _proc = HookCallback;
        var mod = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, mod, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
        {
            bool ctrl  = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(VK_SHIFT)   & 0x8000) != 0;

            if (ctrl && shift)
            {
                var info  = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int delta = (short)((info.mouseData >> 16) & 0xFFFF);
                WheelWithCtrlShift?.Invoke(delta > 0 ? 1 : -1);
                // 이벤트 소비 (1 반환) → 다른 앱에 휠 전달하지 않음
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
