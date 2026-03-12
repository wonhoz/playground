using System.Runtime.InteropServices;
using System.Windows;

namespace ExtBoss;

public partial class App : Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int size);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window w)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            int val = 1;
            DwmSetWindowAttribute(handle, 20, ref val, sizeof(int));
        }
    }
}
