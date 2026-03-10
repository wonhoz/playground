using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QuickCalc;

public partial class App : System.Windows.Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.Message, "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper((Window)sender).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }
}
