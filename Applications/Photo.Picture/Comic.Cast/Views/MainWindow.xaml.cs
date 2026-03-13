using System.Runtime.InteropServices;
using ComicCast.ViewModels;

namespace ComicCast.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        var hwnd  = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int value = 1;
        DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
    }
}
