namespace KeyMacro.Services;

/// <summary>모든 Window · Dialog 공통 다크 타이틀바 적용</summary>
public static class DarkTitle
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void Apply(Window window)
    {
        void Set()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int on = 1;
            NativeMethods.DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }

        if (window.IsLoaded) Set();
        window.SourceInitialized += (_, _) => Set();
    }
}
