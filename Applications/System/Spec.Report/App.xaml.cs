using System.Windows.Interop;

namespace SpecReport;

public partial class App : Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int v, int sz);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 다크 타이틀바 전역 등록
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler((s, _) =>
            {
                var hwnd = new WindowInteropHelper((Window)s).Handle;
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            }));
    }
}
