using System.Runtime.InteropServices;
using System.Windows;

namespace ApiProbe;

public partial class ShortcutsWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public ShortcutsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var dark   = 1;
            DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
        };
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
