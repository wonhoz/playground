namespace CaseForge;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private bool _initialized;
    private AppSettings _settings = new();

    // 키보드 네비게이션
    private readonly List<Border> _rows = [];
    private readonly Dictionary<Border, (string value, string label)> _rowData = [];
    private int _selectedIndex = -1;

    public PopupWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
        PositionNearCursor();
        _initialized = true;
        _settings = SettingsService.Load();
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
        _settings = SettingsService.Load();
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
        _rows.Clear();
        _rowData.Clear();
        _selectedIndex = -1;

        bool hasInput = !string.IsNullOrWhiteSpace(input);

        if (!hasInput)
        {
            RenderHistory();
            StatusText.Text = "텍스트를 입력하세요";
            return;
        }

        var results = CaseConverter.ConvertAll(input);
        var pinned  = _settings.PinnedCases.Count > 0
            ? results.Where(r => _settings.PinnedCases.Contains(r.Key)).ToArray()
            : [];

        if (pinned.Length > 0)
        {
            AddSectionLabel("★  즐겨찾기");
            foreach (var (label, _, value) in pinned)
                AddResultRow(label, value);
            AddSectionLabel("전체");
        }

        foreach (var (label, key, value) in results)
        {
            if (!_settings.PinnedCases.Contains(key))
                AddResultRow(label, value);
        }

        int wordCount = CaseConverter.ParseWords(input).Count;
        StatusText.Text = $"단어 {wordCount}개 — 클릭 또는 ↑↓ Enter";
    }

    private void AddSectionLabel(string text)
        => ResultPanel.Children.Add(new TextBlock
        {
            Text              = text,
            Foreground        = (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily        = new WpfFontFamily("Segoe UI"),
            FontSize          = 10,
            Margin            = new Thickness(2, 6, 0, 2),
        });

    private void AddResultRow(string label, string value)
    {
        var border = MakeRow(label, value);
        _rows.Add(border);
        _rowData[border] = (value, label);
        ResultPanel.Children.Add(border);
    }

    private void RenderHistory()
    {
        var history = _settings.RecentHistory;
        if (history.Count == 0) return;

        AddSectionLabel("최근 입력");
        foreach (var h in history.Take(8))
        {
            var border = new Border
            {
                Margin       = new Thickness(0, 2, 0, 2),
                Padding      = new Thickness(12, 7, 12, 7),
                CornerRadius = new CornerRadius(6),
                Background   = (SolidColorBrush)FindResource("SurfaceBrush"),
                Cursor       = System.Windows.Input.Cursors.Hand,
            };
            border.Child = new TextBlock
            {
                Text         = h,
                Foreground   = (SolidColorBrush)FindResource("TextPrimary"),
                FontFamily   = new WpfFontFamily("Consolas, Segoe UI"),
                FontSize     = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            string t = h;
            border.MouseEnter        += (_, _) => border.Background = (SolidColorBrush)FindResource("RowHover");
            border.MouseLeave        += (_, _) => border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            border.MouseLeftButtonUp += (_, _) =>
            {
                InputBox.Text = t;
                InputBox.CaretIndex = t.Length;
                InputBox.Focus();
            };
            ResultPanel.Children.Add(border);
        }
    }

    private Border MakeRow(string label, string value)
    {
        bool hasValue = !string.IsNullOrEmpty(value);

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
            Text              = label,
            Foreground        = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily        = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valText = new TextBlock
        {
            Text             = hasValue ? value : "—",
            Foreground       = hasValue
                ? (SolidColorBrush)FindResource("TextPrimary")
                : (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily       = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize         = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping     = TextWrapping.Wrap,
        };
        Grid.SetColumn(lblText, 0);
        Grid.SetColumn(valText, 1);
        grid.Children.Add(lblText);
        grid.Children.Add(valText);
        border.Child = grid;

        if (hasValue)
        {
            string copyValue = value;
            border.MouseEnter += (_, _) =>
            {
                if (_rows.IndexOf(border) != _selectedIndex)
                    border.Background = (SolidColorBrush)FindResource("RowHover");
            };
            border.MouseLeave += (_, _) =>
            {
                if (_rows.IndexOf(border) != _selectedIndex)
                    border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            };
            border.MouseLeftButtonUp += (_, _) => CopyAndClose(copyValue, label);
        }

        return border;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            bool sel = i == _selectedIndex;
            _rows[i].Background      = sel
                ? (SolidColorBrush)FindResource("RowHover")
                : (SolidColorBrush)FindResource("SurfaceBrush");
            _rows[i].BorderThickness = sel ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            _rows[i].BorderBrush     = sel ? (SolidColorBrush)FindResource("AccentBrush") : null;
        }
        if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
            _rows[_selectedIndex].BringIntoView();
    }

    private async void CopyAndClose(string text, string label)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            SettingsService.AddHistory(InputBox.Text.Trim());
            var tb = (SolidColorBrush)FindResource("CopyFeedback");
            StatusText.Foreground = tb;
            StatusText.Text = $"✓ {label} 복사됨!";
        }
        catch
        {
            StatusText.Text = "⚠ 클립보드 복사 실패";
        }
        await Task.Delay(500);
        Hide();
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        Placeholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        RenderResults(InputBox.Text);
    }

    private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when _rows.Count > 0:
                _selectedIndex = Math.Min(_selectedIndex + 1, _rows.Count - 1);
                UpdateSelection();
                e.Handled = true;
                break;

            case Key.Up:
                if (_selectedIndex > 0)      _selectedIndex--;
                else if (_selectedIndex == 0) _selectedIndex = -1;
                UpdateSelection();
                e.Handled = true;
                break;

            case Key.Enter when _selectedIndex >= 0 && _selectedIndex < _rows.Count:
                var (v, l) = _rowData[_rows[_selectedIndex]];
                CopyAndClose(v, l);
                e.Handled = true;
                break;

            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible) Hide();
        StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondary"); // 피드백 색상 초기화
    }
}
