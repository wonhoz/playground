namespace CharPad;

public partial class AddCustomCharDialog : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public string ResultChar { get; private set; } = "";
    public string ResultName { get; private set; } = "";

    public AddCustomCharDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
            CharBox.Focus();
        };
    }

    private void CharBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CharPlaceholder.Visibility = string.IsNullOrEmpty(CharBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateAddBtn();
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        NamePlaceholder.Visibility = string.IsNullOrEmpty(NameBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateAddBtn();
    }

    private void UpdateAddBtn()
    {
        AddBtn.IsEnabled = !string.IsNullOrWhiteSpace(CharBox.Text)
                        && !string.IsNullOrWhiteSpace(NameBox.Text);
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        ResultChar = CharBox.Text.Trim();
        ResultName = NameBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            DialogResult = false;
        else if (e.Key == System.Windows.Input.Key.Enter && AddBtn.IsEnabled)
            AddBtn_Click(sender, e);
    }
}
