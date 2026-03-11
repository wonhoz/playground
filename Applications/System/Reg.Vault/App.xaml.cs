using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RegVault;

public partial class App : Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyDarkTitlebar(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd   = helper.EnsureHandle();
        int value  = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }
}
