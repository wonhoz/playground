using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenRecorder;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
        }
    }
}
