using System.Runtime.InteropServices;

namespace ClipboardStacker.Services;

/// <summary>
/// WM_CLIPBOARDUPDATE 메시지로 클립보드 변경 감지.
/// AddClipboardFormatListener API 사용 — 폴링 없이 이벤트 기반.
/// </summary>
public sealed class ClipboardMonitor : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public const int WM_CLIPBOARDUPDATE = 0x031D;

    public event Action<string>? ClipboardChanged;

    private IntPtr _hwnd;
    private bool   _attached;
    private bool   _ignore;   // 프로그래밍 설정 시 이벤트 무시 플래그

    public void Attach(IntPtr hwnd)
    {
        _hwnd     = hwnd;
        _attached = AddClipboardFormatListener(hwnd);
    }

    /// <summary>다음 WM_CLIPBOARDUPDATE 한 번 무시 (자체 붙여넣기 시)</summary>
    public void IgnoreOnce() => _ignore = true;

    /// <summary>WndProc에서 호출 — true 반환 시 handled</summary>
    public bool HandleMessage(int msg)
    {
        if (msg != WM_CLIPBOARDUPDATE) return false;

        if (_ignore) { _ignore = false; return true; }

        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                    ClipboardChanged?.Invoke(text);
            }
        }
        catch { }

        return true;
    }

    public void Dispose()
    {
        if (_attached && _hwnd != IntPtr.Zero)
            RemoveClipboardFormatListener(_hwnd);
    }
}
