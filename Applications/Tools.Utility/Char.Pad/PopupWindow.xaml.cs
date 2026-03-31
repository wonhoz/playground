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

        // 마우스 커서 근처, 화면 경계 보정
        var pt = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(pt);
        var wa = screen.WorkingArea;

        double dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        double wx = pt.X + 10;
        double wy = pt.Y + 10;

        if (wx + Width  > wa.Right)  wx = wa.Right  - Width  - 10;
        if (wy + Height > wa.Bottom) wy = wa.Bottom - Height - 10;

        Left = wx;
        Top  = wy;

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

        // 탭 버튼 활성화 스타일
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

        CharGrid.Children.Clear();
        foreach (var entry in list)
            CharGrid.Children.Add(MakeCharButton(entry));
    }

    // ── 문자 버튼 생성 ──────────────────────────────────────────────────
    private UIElement MakeCharButton(CharEntry entry)
    {
        bool isFav = _storage.IsFavorite(entry.Char);

        var grid = new Grid { Width = 48, Height = 48, Margin = new Thickness(2) };

        var border = new Border
        {
            Background    = (SolidColorBrush)FindResource("TabInactive"),
            CornerRadius  = new CornerRadius(8),
            Cursor        = System.Windows.Input.Cursors.Hand,
            ToolTip       = $"{entry.Char}  {entry.Name}",
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

        // 호버
        border.MouseEnter += (_, _) => border.Background = (SolidColorBrush)FindResource("CharHover");
        border.MouseLeave += (_, _) => border.Background = (SolidColorBrush)FindResource("TabInactive");

        // 좌클릭: 문자 삽입
        border.MouseLeftButtonUp += (_, _) => InsertChar(entry);

        // 우클릭: 즐겨찾기 토글
        border.MouseRightButtonUp += (_, ev) =>
        {
            ev.Handled = true;
            ToggleFavorite(entry, star);
        };

        return grid;
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

        // 즐겨찾기 탭 보고 있는 경우 새로고침
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
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
                SearchBox.Clear();
            else
                Hide();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            // 첫 번째 문자 선택
            if (CharGrid.Children.Count > 0)
            {
                var first = CharGrid.Children[0];
                if (first is Grid g && g.Children[0] is Border b)
                {
                    var entry = (b.ToolTip as string)?.Split("  ")[0];
                    if (entry != null)
                    {
                        var found = CharDatabase.All.FirstOrDefault(c => c.Char == entry);
                        if (found != null) InsertChar(found);
                    }
                }
            }
            e.Handled = true;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // 포커스 잃으면 숨김
        if (IsVisible) Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
