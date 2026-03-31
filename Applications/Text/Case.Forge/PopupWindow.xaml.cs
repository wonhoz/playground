namespace CaseForge;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private bool _initialized;

    public PopupWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        PositionNearCursor();
        _initialized = true;
        TryLoadClipboard();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void PositionNearCursor()
    {
        var pt = System.Windows.Forms.Cursor.Position;
        var wa = System.Windows.Forms.Screen.FromPoint(pt).WorkingArea;
        double wx = pt.X + 12, wy = pt.Y + 12;
        if (wx + Width  > wa.Right)  wx = wa.Right  - Width  - 12;
        if (wy + Height > wa.Bottom) wy = wa.Bottom - Height - 12;
        Left = wx; Top = wy;
    }

    internal void ShowAndActivate()
    {
        if (!IsVisible) Show();
        base.Activate();
        PositionNearCursor();
        TryLoadClipboard();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void TryLoadClipboard()
    {
        try
        {
            var text = System.Windows.Clipboard.GetText().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                InputBox.Text = text;
                InputBox.SelectAll();
            }
        }
        catch { }
    }

    // ── 결과 렌더링 ──────────────────────────────────────────────────────
    private void RenderResults(string input)
    {
        ResultPanel.Children.Clear();
        var results = CaseConverter.ConvertAll(input);
        bool hasInput = !string.IsNullOrWhiteSpace(input);

        foreach (var (label, key, value) in results)
        {
            var row = MakeRow(label, value, hasInput);
            ResultPanel.Children.Add(row);
        }

        StatusText.Text = hasInput
            ? $"단어 {CaseConverter.ParseWords(input).Count}개 — 클릭하여 복사"
            : "텍스트를 입력하세요";
    }

    private UIElement MakeRow(string label, string value, bool hasValue)
    {
        var border = new Border
        {
            Margin       = new Thickness(0, 2, 0, 2),
            Padding      = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6),
            Background   = (SolidColorBrush)FindResource("SurfaceBrush"),
            Cursor       = hasValue ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblText = new TextBlock
        {
            Text       = label,
            Foreground = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var valText = new TextBlock
        {
            Text       = hasValue ? value : "—",
            Foreground = hasValue
                ? (SolidColorBrush)FindResource("TextPrimary")
                : (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize   = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(lblText, 0);
        Grid.SetColumn(valText, 1);
        grid.Children.Add(lblText);
        grid.Children.Add(valText);
        border.Child = grid;

        if (hasValue)
        {
            string copyValue = value;
            border.MouseEnter += (_, _) => border.Background = (SolidColorBrush)FindResource("RowHover");
            border.MouseLeave += (_, _) => border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            border.MouseLeftButtonUp += (_, _) => CopyAndClose(copyValue, label);
        }

        return border;
    }

    private void CopyAndClose(string text, string label)
    {
        System.Windows.Clipboard.SetText(text);
        StatusText.Text = $"✓ {label} 복사됨!";
        Task.Delay(400).ContinueWith(_ => Dispatcher.Invoke(() => Hide()));
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        Placeholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        RenderResults(InputBox.Text);
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_Deactivated(object sender, EventArgs e) { if (IsVisible) Hide(); }
}
