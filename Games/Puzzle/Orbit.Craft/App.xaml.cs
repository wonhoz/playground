using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OrbitCraft;

public partial class App : Application
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(
            typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window w && PresentationSource.FromVisual(w) is HwndSource src)
        { int v = 1; DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int)); }
    }
}
