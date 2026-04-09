namespace CharPad;

public partial class AddCustomCharDialog : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public string ResultChar { get; private set; } = "";
    public string ResultName { get; private set; } = "";

    /// <param name="editChar">수정 모드: 기존 문자 (null = 추가 모드)</param>
    /// <param name="editName">수정 모드: 기존 이름</param>
    public AddCustomCharDialog(string? editChar = null, string? editName = null)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

            if (editChar != null)
            {
                // 수정 모드: 초기값 설정, 문자 입력창 잠금
                Title = "사용자 정의 문자 수정";
                TitleText.Text = "✏ 사용자 정의 문자 수정";
                AddBtn.Content = "수정";
                CharBox.Text = editChar;
                CharBox.IsReadOnly = true;
                NameBox.Text = editName ?? "";
                NameBox.Focus();
                NameBox.SelectAll();
            }
            else
            {
                CharBox.Focus();
            }
        };
    }

    private void CharBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CharPlaceholder.Visibility = string.IsNullOrEmpty(CharBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        // grapheme cluster 수 검증 — 1개 초과 시 경고 표시
        var text = CharBox.Text;
        if (!string.IsNullOrEmpty(text))
        {
            var info = new System.Globalization.StringInfo(text);
            CharWarning.Visibility = info.LengthInTextElements > 1
                ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            CharWarning.Visibility = Visibility.Collapsed;
        }

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
        var raw = CharBox.Text.Trim();
        // 다중 grapheme cluster 입력 시 첫 번째 문자만 사용
        var info = new System.Globalization.StringInfo(raw);
        ResultChar = info.LengthInTextElements > 1
            ? System.Globalization.StringInfo.GetNextTextElement(raw)
            : raw;
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
