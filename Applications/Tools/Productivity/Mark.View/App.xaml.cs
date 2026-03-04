using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MarkView;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(
            typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window w)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);
}
