using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Prompt.Forge;

public partial class App : System.Windows.Application
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    /// <summary>타이틀바 다크 모드 적용 — 모든 Window/Dialog에서 Loaded 이벤트에서 호출</summary>
    public static void ApplyDarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }
}
