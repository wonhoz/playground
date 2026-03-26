namespace CharArt;

public partial class HelpWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public HelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        };
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
