namespace CopyPath;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly UsageService _usage;
    private Dictionary<string, int> _usageCounts = [];
    private string[] _multiPaths = [];   // 탐색기 복수 선택 경로
    private bool _closing;               // Hide 중복 방어
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

        _initialized = true;
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

    internal async void ShowAndActivate()
    {
        _closing = false;
        _usageCounts = await _usage.GetAllAsync();
        PositionNearCursor();
        if (!IsVisible) Show();
        base.Activate();
        await TryGetExplorerPathAsync();
        await LoadRecentPathsAsync();
        PathBox.Focus();
        PathBox.SelectAll();
    }

    private async Task TryGetExplorerPathAsync()
    {
        var result = ExplorerHelper.GetPaths();
        _multiPaths = result.AllSelectedPaths;

        // 복수 선택 버튼 표시 여부
        if (_multiPaths.Length > 1)
        {
            MultiCopyBtn.Content = $"복수 복사 ({_multiPaths.Length})";
            MultiCopyBtn.Visibility = Visibility.Visible;
        }
        else
        {
            MultiCopyBtn.Visibility = Visibility.Collapsed;
        }

        string? path = result.SelectedPath ?? result.FolderPath;

        if (!string.IsNullOrEmpty(path))
        {
            PathBox.Text = path;
            PathBox.SelectAll();
            StatusText.Text = result.SelectedPath != null
                ? "탐색기에서 선택 경로를 가져왔습니다"
                : "탐색기 현재 폴더를 가져왔습니다";
        }
        else
        {
            // 탐색기 경로 없으면 클립보드 자동 읽기
            TryLoadFromClipboard();
        }
        await Task.CompletedTask;
    }

    private void TryLoadFromClipboard()
    {
        try
        {
            var clip = System.Windows.Clipboard.GetText().Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(clip) &&
                (clip.Length >= 2 && (clip[1] == ':' || clip.StartsWith(@"\\"))))
            {
                PathBox.Text = clip;
                PathBox.SelectAll();
                StatusText.Text = "클립보드 경로를 자동으로 읽었습니다";
                return;
            }
        }
        catch { }
        StatusText.Text = "탐색기 선택 없음 — 경로를 입력하거나 파일을 드래그하세요";
    }

    private async Task LoadRecentPathsAsync()
    {
        var recent = await _usage.GetRecentPathsAsync();
        if (recent.Count == 0) { RecentSection.Visibility = Visibility.Collapsed; return; }
        RecentList.ItemsSource = recent;
        RecentSection.Visibility = Visibility.Visible;
    }

    // ── 복수 선택 복사 ────────────────────────────────────────────────────
    private void MultiCopyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_multiPaths.Length == 0) return;
        var joined = string.Join("\r\n", _multiPaths);
        System.Windows.Clipboard.SetText(joined);
        StatusText.Text = $"✓ {_multiPaths.Length}개 경로 복사됨!";
        StatusText.Foreground = (SolidColorBrush)FindResource("SuccessColor");
        ScheduleHide();
    }

    // ── 드래그&드롭 ──────────────────────────────────────────────────────
    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        if (files?.Length > 0)
        {
            PathBox.Text = files[0];
            PathBox.Focus();
            PathBox.SelectAll();
            StatusText.Text = "드래그로 경로를 입력했습니다";
        }
    }

    // ── 최근 경로 이벤트 ──────────────────────────────────────────────────
    private void RecentItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is string path)
        {
            PathBox.Text = path;
            PathBox.Focus();
            PathBox.SelectAll();
            RecentSection.Visibility = Visibility.Collapsed;
        }
    }

    private void RecentItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b) b.Background = (SolidColorBrush)FindResource("RowHover");
    }

    private void RecentItem_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b) b.Background = System.Windows.Media.Brushes.Transparent;
    }

    // ── 결과 렌더링 ──────────────────────────────────────────────────────
    private void RenderResults(string rawPath)
    {
        ResultPanel.Children.Clear();

        var path  = rawPath.Trim().Trim('"');
        bool valid = !string.IsNullOrWhiteSpace(path);

        var results = PathFormatter.FormatAll(path);
        var sorted  = results.OrderByDescending(r => _usageCounts.GetValueOrDefault(r.Key, 0)).ToArray();

        foreach (var (label, key, value) in sorted)
            ResultPanel.Children.Add(MakeRow(label, key, value, valid));

        if (!valid) StatusText.Text = "경로를 입력하세요";
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
            Text              = label,
            Foreground        = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily        = new WpfFontFamily("Segoe UI"),
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var val = new TextBlock
        {
            Text              = hasValue ? value : "—",
            Foreground        = hasValue
                ? (SolidColorBrush)FindResource("TextPrimary")
                : (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily        = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping      = TextWrapping.Wrap,
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
            border.MouseLeftButtonUp += async (_, _) =>
            {
                if (_closing) return;
                System.Windows.Clipboard.SetText(copyVal);
                await _usage.IncrementAsync(copyKey);
                await _usage.AddRecentPathAsync(PathBox.Text.Trim().Trim('"'));
                _usageCounts[copyKey] = _usageCounts.GetValueOrDefault(copyKey, 0) + 1;
                StatusText.Text       = $"✓ {copyLabel} 복사됨!";
                StatusText.Foreground = (SolidColorBrush)FindResource("SuccessColor");
                ScheduleHide();
            };
        }

        return border;
    }

    private void ScheduleHide()
    {
        if (_closing) return;
        _closing = true;
        Task.Delay(400).ContinueWith(_ => Dispatcher.BeginInvoke(Hide));
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────
    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        Placeholder.Visibility = string.IsNullOrEmpty(PathBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondary");
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
