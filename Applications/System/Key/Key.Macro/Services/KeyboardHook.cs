using static KeyMacro.Services.NativeMethods;

namespace KeyMacro.Services;

/// <summary>키 이벤트 한 건</summary>
public readonly record struct HookedKey(int Vk, bool IsDown);

/// <summary>전역 저수준 키보드 훅 (WH_KEYBOARD_LL)</summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly HookProc _proc;   // GC 수거 방지를 위해 필드로 보관
    private IntPtr _handle = IntPtr.Zero;

    /// <summary>키 이벤트 발생. true 를 반환하면 해당 입력을 다른 프로그램에 전달하지 않는다</summary>
    public event Func<HookedKey, bool>? KeyEvent;

    public bool IsInstalled => _handle != IntPtr.Zero;

    public KeyboardHook() => _proc = HookCallback;

    public void Install()
    {
        if (IsInstalled) return;
        _handle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException($"키보드 훅 설치 실패 (오류 코드 {Marshal.GetLastWin32Error()})");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // 이 앱이 합성한 입력은 그대로 통과 — 매크로가 자기 자신을 다시 트리거하지 않도록
                if (info.dwExtraInfo != InputSender.Signature)
                {
                    int msg = (int)wParam;
                    bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                    bool isUp   = msg is WM_KEYUP or WM_SYSKEYUP;

                    if ((isDown || isUp) && KeyEvent is { } handler
                        && handler(new HookedKey((int)info.vkCode, isDown)))
                        return 1;   // 입력 차단
                }
            }
            catch
            {
                // 콜백에서 예외가 새어 나가면 훅이 해제되므로 무시하고 통과시킨다
            }
        }
        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
