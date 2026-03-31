namespace CopyPath;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly UsageService _usage;
    private Dictionary<string, int> _usageCounts = [];
    private bool _initialized;

    public PopupWindow(UsageService usage)
    {
        _usage = usage;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        _usageCounts = _usage.GetAll();
        PositionNearCursor();
        _initialized = true;
        TryGetExplorerPath();
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
        _usageCounts = _usage.GetAll();
        PositionNearCursor();
        if (!IsVisible) Show();
        base.Activate();
        TryGetExplorerPath();
        PathBox.Focus();
        PathBox.SelectAll();
    }

    private void TryGetExplorerPath()
    {
        var path = ExplorerHelper.GetSelectedPath()
                ?? ExplorerHelper.GetCurrentFolderPath();

        if (!string.IsNullOrEmpty(path))
        {
            PathBox.Text = path;
            PathBox.SelectAll();
            StatusText.Text = "탐색기에서 경로를 가져왔습니다";
        }
        else
        {
            StatusText.Text = "탐색기 선택 없음 — 경로를 직접 입력하세요";
        }
    }

    // ── 결과 렌더링 ──────────────────────────────────────────────────────
    private void RenderResults(string rawPath)
    {
        ResultPanel.Children.Clear();

        var path = rawPath.Trim().Trim('"');
        bool valid = !string.IsNullOrWhiteSpace(path);

        var results = PathFormatter.FormatAll(path);

        // 사용 빈도 기준 정렬 (빈도 높은 것 상단)
        var sorted = results.OrderByDescending(r => _usageCounts.GetValueOrDefault(r.Key, 0)).ToArray();

        foreach (var (label, key, value) in sorted)
            ResultPanel.Children.Add(MakeRow(label, key, value, valid));

        if (!valid)
            StatusText.Text = "경로를 입력하세요";
    }

    private UIElement MakeRow(string label, string key, string value, bool hasValue)
    {
        var border = new Border
        {
            Margin       = new Thickness(0, 2, 0, 2),
            Padding      = new Thickness(12, 7, 12, 7),
            CornerRadius = new CornerRadius(6),
            Background   = (SolidColorBrush)FindResource("SurfaceBrush"),
            Cursor       = hasValue ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock
        {
            Text = label,
            Foreground = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily = new WpfFontFamily("Segoe UI"),
            FontSize   = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var val = new TextBlock
        {
            Text = hasValue ? value : "—",
            Foreground = hasValue
                ? (SolidColorBrush)FindResource("TextPrimary")
                : (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily   = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize     = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(val);
        border.Child = grid;

        if (hasValue)
        {
            string copyKey = key, copyVal = value, copyLabel = label;
            border.MouseEnter += (_, _) => border.Background = (SolidColorBrush)FindResource("RowHover");
            border.MouseLeave += (_, _) => border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            border.MouseLeftButtonUp += (_, _) =>
            {
                System.Windows.Clipboard.SetText(copyVal);
                _usage.Increment(copyKey);
                _usageCounts[copyKey] = _usageCounts.GetValueOrDefault(copyKey, 0) + 1;
                StatusText.Text = $"✓ {copyLabel} 복사됨!";
                Task.Delay(400).ContinueWith(_ => Dispatcher.Invoke(() => Hide()));
            };
        }

        return border;
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────
    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        Placeholder.Visibility = string.IsNullOrEmpty(PathBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        RenderResults(PathBox.Text);
    }

    private void PathBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
