using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FolderPurge;

public partial class HelpWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public HelpWindow(List<ScanHistoryEntry> history)
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape ||
                e.Key == System.Windows.Input.Key.F1) Close();
        };

        if (history.Count > 0)
        {
            HistoryList.ItemsSource = history;
        }
        else
        {
            HistoryEmpty.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
