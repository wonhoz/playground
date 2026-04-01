using System.Windows.Input;
using System.Windows.Threading;

namespace CharPad;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly StorageService _storage;
    private IntPtr    _targetHwnd;
    private string    _activeTab   = "recent";
    private bool      _initialized = false;

    private static readonly (string Id, string Label)[] Tabs =
    {
        ("recent",   "🕐 최근"),
        ("favorite", "⭐ 즐겨찾기"),
        ("arrow",    "→ 화살표"),
        ("math",     "∑ 수학"),
        ("symbol",   "© 기호"),
        ("currency", "$ 통화"),
        ("super",    "² 위첨자"),
        ("emoji",    "😀 이모지"),
    };

    public PopupWindow(StorageService storage)
    {
        _storage = storage;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        BuildTabs();
        _initialized = true;
        SwitchTab("recent");
        SearchBox.Focus();
    }

    // ── 팝업 위치 결정 및 표시 ──────────────────────────────────────────
    public void ShowAt(IntPtr targetHwnd)
    {
        _targetHwnd = targetHwnd;

        var pt = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(pt);
        var wa = screen.WorkingArea;

        // DPI 스케일 획득: window 표시 전이면 PresentationSource가 null → Graphics 폴백
        double dpiScale;
        if (PresentationSource.FromVisual(this) is { } src)
            dpiScale = src.CompositionTarget.TransformToDevice.M11;
        else
        {
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiScale = g.DpiX / 96.0;
        }

        // 물리 픽셀 → DIP 변환 후 경계 보정 (Left/Top/Width/Height 모두 DIP 단위)
        double wxDip = (pt.X + 10) / dpiScale;
        double wyDip = (pt.Y + 10) / dpiScale;
        double rightDip  = wa.Right  / dpiScale;
        double bottomDip = wa.Bottom / dpiScale;

        if (wxDip + Width  > rightDip)  wxDip = rightDip  - Width  - 10;
        if (wyDip + Height > bottomDip) wyDip = bottomDip - Height - 10;

        Left = wxDip;
        Top  = wyDip;

        if (!IsLoaded || !IsVisible)
        {
            Show();
        }
        else
        {
            Activate();
        }

        Dispatcher.BeginInvoke(() =>
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
            if (_initialized) RefreshGrid();
            SearchBox.Clear();
            SearchBox.Focus();
        }, DispatcherPriority.Loaded);
    }

    // ── 탭 버튼 생성 ────────────────────────────────────────────────────
    private void BuildTabs()
    {
        TabPanel.Children.Clear();
        foreach (var (id, label) in Tabs)
        {
            var btn = new WpfButton
            {
                Content     = label,
                Padding     = new Thickness(10, 0, 10, 0),
                Height      = 28,
                FontFamily  = new WpfFontFamily("Segoe UI"),
                FontSize    = 12,
                Tag         = id,
                Margin      = new Thickness(0, 0, 4, 0),
            };
            btn.Click += TabBtn_Click;
            TabPanel.Children.Add(btn);
        }
    }

    private void TabBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        var btn = (WpfButton)sender;
        SearchBox.Clear();
        SwitchTab((string)btn.Tag);
    }

    private void SwitchTab(string tabId)
    {
        _activeTab = tabId;

        foreach (WpfButton b in TabPanel.Children)
        {
            bool active = (string)b.Tag == tabId;
            b.Background = active
                ? (SolidColorBrush)FindResource("AccentBrush")
                : (SolidColorBrush)FindResource("TabInactive");
            b.Foreground = new SolidColorBrush(Colors.White);
        }

        RefreshGrid();
    }

    // ── 문자 그리드 갱신 ────────────────────────────────────────────────
    private void RefreshGrid()
    {
        if (!_initialized) return;

        var query = SearchBox.Text.Trim();
        IEnumerable<CharEntry> entries;

        if (!string.IsNullOrEmpty(query))
        {
            entries = CharDatabase.Search(query);
            StatusText.Text = $"검색 결과";
        }
        else
        {
            entries = _activeTab switch
            {
                "recent"   => _storage.GetRecents().Select(c => CharDatabase.All.FirstOrDefault(e => e.Char == c)).Where(e => e is not null).Select(e => e!),
                "favorite" => _storage.GetFavorites().Select(c => CharDatabase.All.FirstOrDefault(e => e.Char == c)).Where(e => e is not null).Select(e => e!),
                _          => CharDatabase.GetByCategory(_activeTab),
            };
            StatusText.Text = _activeTab switch
            {
                "recent"   => "최근 사용",
                "favorite" => "즐겨찾기",
                _          => Tabs.FirstOrDefault(t => t.Id == _activeTab).Label,
            };
        }

        var list = entries.ToList();
        StatusText.Text += $" ({list.Count}개)";

        // 결과 없음 안내
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // favorites를 한 번만 조회해서 HashSet으로 변환 (N+1 DB 쿼리 방지)
        var favSet = _storage.GetFavorites().ToHashSet();

        CharGrid.Children.Clear();
        foreach (var entry in list)
            CharGrid.Children.Add(MakeCharButton(entry, favSet));
    }

    // ── 문자 버튼 생성 ──────────────────────────────────────────────────
    private UIElement MakeCharButton(CharEntry entry, HashSet<string> favSet)
    {
        bool isFav = favSet.Contains(entry.Char);

        var grid = new Grid { Width = 48, Height = 48, Margin = new Thickness(2) };

        var border = new Border
        {
            Background    = (SolidColorBrush)FindResource("TabInactive"),
            CornerRadius  = new CornerRadius(8),
            Cursor        = System.Windows.Input.Cursors.Hand,
            ToolTip       = $"{entry.Char}  {entry.Name}",
            Focusable     = true,
            Tag           = entry,
        };

        var tb = new TextBlock
        {
            Text                = entry.Char,
            FontSize            = 20,
            FontFamily          = new WpfFontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"),
            Foreground          = (SolidColorBrush)FindResource("TextPrimary"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        border.Child = tb;

        // 즐겨찾기 별표
        var star = new TextBlock
        {
            Text                = "★",
            FontSize            = 9,
            Foreground          = (SolidColorBrush)FindResource("FavColor"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(0, 2, 3, 0),
            Visibility          = isFav ? Visibility.Visible : Visibility.Collapsed,
        };

        grid.Children.Add(border);
        grid.Children.Add(star);

        // 호버 / 포커스 시각 피드백
        border.MouseEnter += (_, _) => border.Background = (SolidColorBrush)FindResource("CharHover");
        border.MouseLeave += (_, _) =>
        {
            if (!border.IsKeyboardFocused)
                border.Background = (SolidColorBrush)FindResource("TabInactive");
        };
        border.GotKeyboardFocus  += (_, _) => border.Background = (SolidColorBrush)FindResource("CharHover");
        border.LostKeyboardFocus += (_, _) => border.Background = (SolidColorBrush)FindResource("TabInactive");

        // 좌클릭: 삽입 / Shift+클릭: 복사만
        border.MouseLeftButtonUp += (_, _) =>
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                CopyCharOnly(entry);
            else
                InsertChar(entry);
        };

        // 우클릭: 즐겨찾기 토글
        border.MouseRightButtonUp += (_, ev) =>
        {
            ev.Handled = true;
            ToggleFavorite(entry, star);
        };

        // 키보드 탐색
        border.KeyDown += CharButton_KeyDown;

        return grid;
    }

    // ── 키보드 탐색 (그리드 내) ──────────────────────────────────────────
    private void CharButton_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not Border border) return;
        int idx = GetBorderIndex(border);
        if (idx < 0) return;

        switch (e.Key)
        {
            case Key.Enter:
                if (border.Tag is CharEntry entry) InsertChar(entry);
                e.Handled = true;
                break;
            case Key.Left:
                FocusCharAt(idx - 1);
                e.Handled = true;
                break;
            case Key.Right:
                FocusCharAt(idx + 1);
                e.Handled = true;
                break;
            case Key.Up:
                int up = idx - GetItemsPerRow();
                if (up < 0) SearchBox.Focus();
                else FocusCharAt(up);
                e.Handled = true;
                break;
            case Key.Down:
                FocusCharAt(idx + GetItemsPerRow());
                e.Handled = true;
                break;
            case Key.Escape:
                SearchBox.Focus();
                e.Handled = true;
                break;
        }
    }

    private int GetBorderIndex(Border border)
    {
        for (int i = 0; i < CharGrid.Children.Count; i++)
        {
            if (CharGrid.Children[i] is Grid g && g.Children.Count > 0 && g.Children[0] == border)
                return i;
        }
        return -1;
    }

    private void FocusCharAt(int idx)
    {
        if (idx < 0 || idx >= CharGrid.Children.Count) return;
        if (CharGrid.Children[idx] is Grid g && g.Children.Count > 0 && g.Children[0] is Border b)
            b.Focus();
    }

    private void FocusFirstCharButton()
    {
        if (CharGrid.Children.Count > 0 &&
            CharGrid.Children[0] is Grid g && g.Children.Count > 0 && g.Children[0] is Border b)
            b.Focus();
    }

    private int GetItemsPerRow()
    {
        double w = CharGrid.ActualWidth > 0 ? CharGrid.ActualWidth : 546; // 570 - 2×12 margin
        return Math.Max(1, (int)(w / 52));
    }

    // ── 문자 삽입 ────────────────────────────────────────────────────────
    private void InsertChar(CharEntry entry)
    {
        System.Windows.Clipboard.SetText(entry.Char);
        _storage.AddRecent(entry.Char);
        Hide();

        // 이전 창에 Ctrl+V
        if (_targetHwnd != IntPtr.Zero)
        {
            Task.Run(() =>
            {
                System.Threading.Thread.Sleep(80);
                InputHelper.PasteToWindow(_targetHwnd);
            });
        }
    }

    // ── 복사 전용 (Shift+클릭) ──────────────────────────────────────────
    private void CopyCharOnly(CharEntry entry)
    {
        System.Windows.Clipboard.SetText(entry.Char);
        _storage.AddRecent(entry.Char);
        StatusText.Text = $"복사됨: {entry.Char}  {entry.Name}";
    }

    // ── 즐겨찾기 토글 ────────────────────────────────────────────────────
    private void ToggleFavorite(CharEntry entry, TextBlock star)
    {
        if (_storage.IsFavorite(entry.Char))
        {
            _storage.RemoveFavorite(entry.Char);
            star.Visibility = Visibility.Collapsed;
        }
        else
        {
            _storage.AddFavorite(entry.Char);
            star.Visibility = Visibility.Visible;
        }

        if (_activeTab == "favorite") RefreshGrid();
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        RefreshGrid();
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
                SearchBox.Clear();
            else
                Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // 첫 번째 문자 삽입
            if (CharGrid.Children.Count > 0 &&
                CharGrid.Children[0] is Grid g && g.Children[0] is Border b &&
                b.Tag is CharEntry entry)
            {
                InsertChar(entry);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down ||
                 (e.Key == Key.Tab && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            // 검색창 → 그리드 첫 번째 문자로 포커스 이동
            FocusFirstCharButton();
            e.Handled = true;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    public void ShowHelpOverlay()
        => Dispatcher.BeginInvoke(() => HelpOverlay.Visibility = Visibility.Visible,
                                  DispatcherPriority.Loaded);

    private void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        HelpOverlay.Visibility = HelpOverlay.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void HelpOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        => HelpOverlay.Visibility = Visibility.Collapsed;

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible) Hide();
    }
}
