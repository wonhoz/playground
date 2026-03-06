using System.Runtime.InteropServices;
using System.Windows;
using WinKey = System.Windows.Input.Key;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LocaleForge;

public partial class AddKeyDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public string NewKey { get; private set; } = string.Empty;

    public AddKeyDialog()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TxtKey.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => Confirm();
    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TxtKey_KeyDown(object sender, WinKeyEventArgs e)
    {
        if (e.Key == WinKey.Enter) Confirm();
        else if (e.Key == WinKey.Escape) DialogResult = false;
    }

    private void Confirm()
    {
        var key = TxtKey.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            MessageBox.Show("키 이름을 입력하세요.", "Locale.Forge",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        NewKey = key;
        DialogResult = true;
    }
}
