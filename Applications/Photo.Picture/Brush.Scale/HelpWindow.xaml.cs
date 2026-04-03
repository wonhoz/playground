using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Brush.Scale;

public partial class HelpWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public HelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        };
    }

    void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
