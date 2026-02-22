using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Music.Player.Services;

/// <summary>
/// 창 드래그 중 모니터 가장자리/모서리에 자석처럼 붙이는 스냅 서비스.
/// WM_MOVING 메시지를 후킹해서 DragMove()와 완전 호환됩니다.
/// </summary>
public sealed class WindowSnapService : IDisposable
{
    // ── Win32 ─────────────────────────────────────────────────
    private const int WM_MOVING = 0x0216;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // ── 설정 ──────────────────────────────────────────────────
    /// <summary>스냅 임계값 (픽셀). 이 거리 이내면 가장자리에 붙임.</summary>
    public int Threshold { get; set; } = 18;

    private readonly Window    _window;
    private          HwndSource? _source;
    private          bool        _disposed;

    public WindowSnapService(Window window)
    {
        _window = window;

        if (_window.IsLoaded)
            Attach();
        else
            _window.Loaded += (_, _) => Attach();
    }

    private void Attach()
    {
        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                           ref bool handled)
    {
        if (msg == WM_MOVING)
        {
            var rect = Marshal.PtrToStructure<RECT>(lParam);
            ApplySnap(ref rect);
            Marshal.StructureToPtr(rect, lParam, false);
            // handled = false → OS 기본 처리도 유지 (창 이동 정상 작동)
        }
        return IntPtr.Zero;
    }

    private void ApplySnap(ref RECT r)
    {
        int w = r.Right  - r.Left;
        int h = r.Bottom - r.Top;

        // 현재 창 중심 기준으로 해당 모니터 작업 영역 가져오기
        var center    = new System.Drawing.Point((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
        var screen    = System.Windows.Forms.Screen.FromPoint(center);
        var wa        = screen.WorkingArea;   // taskbar 제외 실제 작업 영역

        // ── 수평 스냅 ────────────────────────────────────────
        if (Math.Abs(r.Left - wa.Left) <= Threshold)
        {
            r.Left  = wa.Left;
            r.Right = r.Left + w;
        }
        else if (Math.Abs(r.Right - wa.Right) <= Threshold)
        {
            r.Right = wa.Right;
            r.Left  = r.Right - w;
        }

        // ── 수직 스냅 ────────────────────────────────────────
        if (Math.Abs(r.Top - wa.Top) <= Threshold)
        {
            r.Top    = wa.Top;
            r.Bottom = r.Top + h;
        }
        else if (Math.Abs(r.Bottom - wa.Bottom) <= Threshold)
        {
            r.Bottom = wa.Bottom;
            r.Top    = r.Bottom - h;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _source?.RemoveHook(WndProc);
        _disposed = true;
    }
}
