using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Stock.Catch.Services;

/// <summary>윈도우 타이틀바를 다크 모드로 전환하는 DWM 헬퍼.</summary>
public static class NativeTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;            // Win11: 창 테두리 색
    private const int BorderColorRef = 0x001A1A1A;        // COLORREF 0x00BBGGRR (#1A1A1A, 창 배경과 동일)

    public static void ApplyDarkTitleBar(Window window)
    {
        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            // 밝은 시스템 기본 테두리 → 다크. Win11 22000+ 에서만 동작(구버전은 무시).
            int border = BorderColorRef;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }

        if (window.IsLoaded) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }
}
