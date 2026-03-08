using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Layout.Forge;

public partial class InputDialog : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public string? Result => TxtInput.Text;

    public InputDialog(string title, string label, string initial = "")
    {
        InitializeComponent();
        Title          = title;
        TxtLabel.Text  = label;
        TxtInput.Text  = initial;
        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
            TxtInput.Focus();
            TxtInput.SelectAll();
        };
    }

    void Ok_Click(object s, RoutedEventArgs e)     { DialogResult = true; }
    void Cancel_Click(object s, RoutedEventArgs e)  { DialogResult = false; }
    void TxtInput_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { DialogResult = true; }
        if (e.Key == Key.Escape) { DialogResult = false; }
    }
}
