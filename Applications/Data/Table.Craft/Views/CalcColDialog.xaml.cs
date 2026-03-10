using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace TableCraft.Views;

public partial class CalcColDialog : Window
{
    private readonly string[] _headers;

    public string ColumnName  { get; private set; } = "";
    public string Expression  { get; private set; } = "";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public CalcColDialog(string[] headers)
    {
        InitializeComponent();
        _headers = headers;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // 컬럼 힌트 생성 (A, B, C ... 형식 + [컬럼명] 형식)
        var hints = new List<string>();
        for (int i = 0; i < Math.Min(_headers.Length, 26); i++)
            hints.Add(((char)('A' + i)).ToString());
        foreach (var h in _headers)
            hints.Add($"[{h}]");
        ColHints.ItemsSource = hints;

        TxtName.Focus();
    }

    private void ColHint_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is string hint)
        {
            TxtExpr.Text += hint;
            TxtExpr.CaretIndex = TxtExpr.Text.Length;
            TxtExpr.Focus();
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        string expr = TxtExpr.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("컬럼 이름을 입력하세요.", "Table.Craft",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }
        if (string.IsNullOrEmpty(expr))
        {
            MessageBox.Show("수식을 입력하세요.", "Table.Craft",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtExpr.Focus();
            return;
        }

        ColumnName   = name;
        Expression   = expr;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
