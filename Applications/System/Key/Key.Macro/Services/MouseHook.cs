using static KeyMacro.Services.NativeMethods;

namespace KeyMacro.Services;

/// <summary>전역 저수준 마우스 훅 — 화면에서 좌표를 찍을 때만 사용</summary>
public sealed class MouseHook : IDisposable
{
    private readonly HookProc _proc;   // GC 수거 방지
    private IntPtr _handle = IntPtr.Zero;

    /// <summary>마우스 버튼이 눌렸을 때. true 를 반환하면 클릭을 다른 프로그램에 전달하지 않는다</summary>
    public event Func<int, int, bool>? ButtonDown;

    public MouseHook() => _proc = HookCallback;

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException($"마우스 훅 설치 실패 (오류 코드 {Marshal.GetLastWin32Error()})");
    }

    public void Uninstall()
    {
        if (_handle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int msg = (int)wParam;
                if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (ButtonDown is { } handler && handler(info.pt.X, info.pt.Y))
                        return 1;   // 지점만 찍고 실제 클릭은 전달하지 않는다
                }
                // 버튼을 누른 뒤의 UP 메시지도 함께 막아 짝이 맞도록 한다
                if (_swallowUp && msg is WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP)
                {
                    _swallowUp = false;
                    return 1;
                }
            }
            catch
            {
                // 콜백에서 예외가 새어 나가면 훅이 해제되므로 무시한다
            }
        }
        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    private bool _swallowUp;

    /// <summary>다음 버튼 뗌(UP) 이벤트도 차단하도록 표시</summary>
    public void SwallowNextUp() => _swallowUp = true;

    public void Dispose() => Uninstall();
}
