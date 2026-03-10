namespace CodeSnap.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT   = 0x0004;
    private const int VK_C        = 0x43;  // Ctrl+Shift+C
    private const int HOTKEY_ID   = 9001;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwnd;
    private readonly Action _onHotkey;

    public HotkeyService(Window owner, Action onHotkey)
    {
        _onHotkey = onHotkey;
        owner.SourceInitialized += (_, _) =>
        {
            _hwnd = HwndSource.FromHwnd(new WindowInteropHelper(owner).Handle);
            if (_hwnd is not null)
            {
                _hwnd.AddHook(WndProc);
                RegisterHotKey(_hwnd.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_C);
            }
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _onHotkey();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != null)
        {
            UnregisterHotKey(_hwnd.Handle, HOTKEY_ID);
            _hwnd.RemoveHook(WndProc);
        }
    }
}
