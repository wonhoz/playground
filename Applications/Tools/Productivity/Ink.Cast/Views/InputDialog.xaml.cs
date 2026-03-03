using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace InkCast.Views;

public partial class InputDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public string Result { get; private set; } = "";

    public InputDialog(string prompt, string defaultValue = "")
    {
        InitializeComponent();
        TxtPrompt.Text  = prompt;
        TxtInput.Text   = defaultValue;
        TxtInput.SelectAll();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource s)
            { int v = 1; DwmSetWindowAttribute(s.Handle, 20, ref v, sizeof(int)); }
            TxtInput.Focus();
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)     { Result = TxtInput.Text; DialogResult = true; }
    private void BtnCancel_Click(object sender, RoutedEventArgs e)  { DialogResult = false; }
    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { Result = TxtInput.Text; DialogResult = true; }
        if (e.Key == Key.Escape) { DialogResult = false; }
    }
}
