using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NetScan;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyDarkTitle();
    }

    private void ApplyDarkTitle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    public void SetStatus(string text) => TxtStatus.Text = text;

    public void ShowProgress(bool visible, bool indeterminate = false, double value = 0, double max = 100)
    {
        PbMain.Visibility      = visible ? Visibility.Visible : Visibility.Collapsed;
        PbMain.IsIndeterminate = indeterminate;
        if (!indeterminate) { PbMain.Maximum = max; PbMain.Value = value; }
    }
}
