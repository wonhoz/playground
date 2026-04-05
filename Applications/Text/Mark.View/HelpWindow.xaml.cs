using System.Windows;
using System.Runtime.InteropServices;

namespace MarkView;

public partial class HelpWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public HelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int val = 1;
            DwmSetWindowAttribute(handle, 20, ref val, sizeof(int));
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
