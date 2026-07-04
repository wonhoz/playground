using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Stock.Watch.Services;

/// <summary>윈도우 타이틀바를 다크 모드로 전환하는 DWM 헬퍼.</summary>
public static class NativeTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyDarkTitleBar(Window window)
    {
        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }

        if (window.IsLoaded) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }
}
