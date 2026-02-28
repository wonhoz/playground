using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HexPeek.Dialogs;

public partial class GotoDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public long Offset { get; private set; }

    public GotoDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            TxtOffset.Focus();
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => Confirm();
    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TxtOffset_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)  Confirm();
        if (e.Key == Key.Escape)  DialogResult = false;
    }

    private void Confirm()
    {
        string raw = TxtOffset.Text.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(raw[2..], System.Globalization.NumberStyles.HexNumber, null, out long v))
            { Offset = v; DialogResult = true; return; }
        }
        else if (raw.All(char.IsAsciiHexDigit) && raw.Length > 0)
        {
            // 6자 이상 & 모두 hex digit이면 HEX로 간주
            if (raw.Length >= 4 && long.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out long vh))
            { Offset = vh; DialogResult = true; return; }
        }
        if (long.TryParse(raw, out long dec))
        { Offset = dec; DialogResult = true; return; }

        MessageBox.Show("유효한 오프셋을 입력하세요.\n예: 0xFF, 255, 1A4C", "Hex.Peek",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
