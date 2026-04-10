using System.Globalization;
using System.Windows.Data;

namespace CopyPath;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly UsageService _usage;
    private Dictionary<string, int> _usageCounts = [];
    private HashSet<string> _hiddenFormats = [];
    private HashSet<string> _pinnedFormats = [];
    private string[] _multiPaths = [];   // 탐색기 복수 선택 경로
    private bool _closing;               // Hide 중복 방어
    private bool _initialized;
    private int _hideDelay = 400;        // 복사 후 자동 숨김 딜레이(ms)
    private CancellationTokenSource? _hideCts;

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

    internal async Task ShowAndActivateAsync()
    {
        _closing = false;
        _hideCts?.Cancel();
        _hideCts = null;
        PositionNearCursor();   // 핫키 누른 직후 커서 위치 즉시 캡처
        _usageCounts   = await _usage.GetAllAsync();
        _hiddenFormats = await _usage.GetHiddenFormatsAsync();
        _pinnedFormats = await _usage.GetPinnedFormatsAsync();
        _hideDelay     = await _usage.GetHideDelayAsync();
        PathFormatter.BasePathForRelative = await _usage.GetBasePathAsync();
        if (!IsVisible) Show();
        base.Activate();
        TryGetExplorerPath();
        await LoadRecentPathsAsync();
        PathBox.Focus();
        PathBox.SelectAll();
    }

    private void TryGetExplorerPath()
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
    }

    private void TryLoadFromClipboard()
    {
        try
        {
            var clip = System.Windows.Clipboard.GetText().Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(clip)) { SetNoPathStatus(); return; }

            // file:/// URL 감지
            if (clip.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var path = Uri.UnescapeDataString(new Uri(clip).LocalPath);
                    PathBox.Text = path;
                    PathBox.SelectAll();
                    StatusText.Text = "클립보드의 file:/// URL을 경로로 변환했습니다";
                    return;
                }
                catch { }
            }

            // 일반 경로 감지 (드라이브 문자 또는 UNC)
            if (clip.Length >= 2 && (clip[1] == ':' || clip.StartsWith(@"\\")))
            {
                PathBox.Text = clip;
                PathBox.SelectAll();
                StatusText.Text = "클립보드 경로를 자동으로 읽었습니다";
                return;
            }
        }
        catch { }
        SetNoPathStatus();
    }

    private void SetNoPathStatus()
        => StatusText.Text = "탐색기 선택 없음 — 경로를 입력하거나 파일을 드래그하세요";

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

            _multiPaths = files;
            if (files.Length > 1)
            {
                MultiCopyBtn.Content = $"복수 복사 ({files.Length})";
                MultiCopyBtn.Visibility = Visibility.Visible;
            }
            else
            {
                MultiCopyBtn.Visibility = Visibility.Collapsed;
            }

            StatusText.Text = files.Length > 1
                ? $"{files.Length}개 파일을 드래그로 입력했습니다"
                : "드래그로 경로를 입력했습니다";
        }
    }

    // ── 최근 경로 이벤트 ──────────────────────────────────────────────────
    private void RecentItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 별표/삭제 버튼 클릭은 부모 핸들러로 버블링되지 않도록 처리됨
        if (e.Handled) return;
        if (sender is Border b && b.DataContext is RecentPath rp)
        {
            PathBox.Text = rp.Path;
            PathBox.Focus();
            PathBox.SelectAll();
            RecentSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void RecentStar_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is System.Windows.FrameworkElement fe && fe.DataContext is RecentPath rp)
        {
            await _usage.ToggleStarAsync(rp.Path);
            await LoadRecentPathsAsync();
        }
    }

    private async void RecentDelete_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is System.Windows.FrameworkElement fe && fe.DataContext is RecentPath rp)
        {
            await _usage.DeleteRecentAsync(rp.Path);
            await LoadRecentPathsAsync();
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

    // ── 외부 갱신 요청 ──────────────────────────────────────────────────────
    internal void RefreshFormats(HashSet<string> hiddenFormats)
    {
        if (!IsVisible) return;
        _hiddenFormats = hiddenFormats;
        RenderResults(PathBox.Text);
        StatusText.Text = "모든 포맷이 복원되었습니다";
        StatusText.Foreground = (SolidColorBrush)FindResource("SuccessColor");
    }

    // ── 결과 렌더링 ──────────────────────────────────────────────────────
    private void RenderResults(string rawPath)
    {
        ResultPanel.Children.Clear();

        var path  = rawPath.Trim().Trim('"');
        bool valid = !string.IsNullOrWhiteSpace(path);

        // 경로 유효성 표시 (PathBox 테두리 색)
        if (valid)
        {
            bool exists = File.Exists(path) || Directory.Exists(path);
            PathBox.BorderBrush = exists
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3ACF7A"))
                : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6B6B"));
        }
        else
        {
            PathBox.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
        }

        var results = PathFormatter.FormatAll(path);
        var visible = results.Where(r => !_hiddenFormats.Contains(r.Key)).ToArray();

        // 핀된 포맷 먼저(원래 순서 유지), 이후 사용 빈도 내림차순
        var pinned   = visible.Where(r =>  _pinnedFormats.Contains(r.Key)).ToArray();
        var unpinned = visible.Where(r => !_pinnedFormats.Contains(r.Key))
                              .OrderByDescending(r => _usageCounts.GetValueOrDefault(r.Key, 0))
                              .ToArray();

        foreach (var (label, key, value) in pinned.Concat(unpinned))
            ResultPanel.Children.Add(MakeRow(label, key, value, valid, _pinnedFormats.Contains(key)));

        if (!valid) StatusText.Text = "경로를 입력하세요";
    }

    private UIElement MakeRow(string label, string key, string value, bool hasValue, bool isPinned)
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
            Text              = isPinned ? $"📌 {label}" : label,
            Foreground        = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily        = new WpfFontFamily("Segoe UI"),
            FontSize          = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var val = new TextBlock
        {
            Text              = hasValue ? (string.IsNullOrEmpty(value) ? "—" : value) : "—",
            Foreground        = hasValue && !string.IsNullOrEmpty(value)
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
                if (_closing || string.IsNullOrEmpty(copyVal)) return;
                System.Windows.Clipboard.SetText(copyVal);
                await _usage.IncrementAsync(copyKey);
                await _usage.AddRecentPathAsync(PathBox.Text.Trim().Trim('"'));
                _usageCounts[copyKey] = _usageCounts.GetValueOrDefault(copyKey, 0) + 1;
                StatusText.Text       = $"✓ {copyLabel} 복사됨!";
                StatusText.Foreground = (SolidColorBrush)FindResource("SuccessColor");
                ScheduleHide();
            };

            // 우클릭 → 컨텍스트 메뉴 (핀 토글 / 숨기기)
            border.MouseRightButtonUp += async (_, _) =>
            {
                var cm = new System.Windows.Controls.ContextMenu
                {
                    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1E2A")),
                    BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                };

                bool pinned = _pinnedFormats.Contains(copyKey);
                var pinItem = new System.Windows.Controls.MenuItem
                {
                    Header = pinned ? "📌 상단 고정 해제" : "📌 상단 고정",
                    Foreground = (SolidColorBrush)FindResource("TextPrimary"),
                    Background = System.Windows.Media.Brushes.Transparent,
                };
                pinItem.Click += async (_, _) =>
                {
                    await _usage.ToggleFormatPinAsync(copyKey);
                    _pinnedFormats = await _usage.GetPinnedFormatsAsync();
                    RenderResults(PathBox.Text);
                    StatusText.Text = pinned ? $"'{copyLabel}' 고정 해제됨" : $"'{copyLabel}' 상단 고정됨";
                    StatusText.Foreground = (SolidColorBrush)FindResource("SuccessColor");
                };

                var hideItem = new System.Windows.Controls.MenuItem
                {
                    Header = "🙈 이 포맷 숨기기",
                    Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                    Background = System.Windows.Media.Brushes.Transparent,
                };
                hideItem.Click += async (_, _) =>
                {
                    _hiddenFormats.Add(copyKey);
                    await _usage.SetFormatHiddenAsync(copyKey, true);
                    RenderResults(PathBox.Text);
                    StatusText.Text = $"'{copyLabel}' 숨김 (트레이 우클릭 → 포맷 복원)";
                    StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondary");
                };

                cm.Items.Add(pinItem);
                cm.Items.Add(new System.Windows.Controls.Separator());
                cm.Items.Add(hideItem);
                cm.IsOpen = true;
            };
        }

        return border;
    }

    private void ScheduleHide()
    {
        if (_closing) return;
        _closing = true;
        _hideCts?.Cancel();
        if (_hideDelay == 0) { Hide(); return; }
        var cts = new CancellationTokenSource();
        _hideCts = cts;
        Task.Delay(_hideDelay, cts.Token).ContinueWith(
            t => { if (!t.IsCanceled) Dispatcher.BeginInvoke(Hide); },
            CancellationToken.None);
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

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_Deactivated(object sender, EventArgs e) { if (IsVisible) Hide(); }
}

// ── 별표 상태를 텍스트로 변환 ─────────────────────────────────────────────
public sealed class StarConverter : IValueConverter
{
    public static readonly StarConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "★" : "☆";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
