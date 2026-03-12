using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinEvent.Windows;

public partial class EventDetailWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly EventItem _item;

    public EventDetailWindow(EventItem item)
    {
        InitializeComponent();
        _item = item;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        LblTime.Text    = _item.TimeDisplay;
        LblEventId.Text = _item.EventId.ToString();
        LblSource.Text  = _item.ProviderName;
        LblLevel.Text   = _item.LevelTag;
        LblLevel.Foreground = _item.LevelColor;
        TxtMessage.Text = _item.MessageFull;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(_item.MessageFull);

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
