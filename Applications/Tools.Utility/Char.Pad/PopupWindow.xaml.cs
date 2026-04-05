using System.Windows.Input;
using System.Windows.Threading;

namespace CharPad;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly StorageService  _storage;
    private IntPtr    _targetHwnd;
    private string    _activeTab   = "recent";
    private bool      _initialized = false;
    private DispatcherTimer? _searchTimer;
    private double    _charFontSize = 20;
    private bool      _pinned       = false;

    // ItemSize = 문자 버튼 1칸 크기 (폰트 크기 기반으로 자동 계산)
    private double ItemSize => _charFontSize + 28;

    private static readonly (string Id, string Label, string StatusName)[] Tabs =
    {
        ("recent",   "🕐 최근",     "최근 사용"),
        ("favorite", "⭐ 즐겨찾기", "즐겨찾기"),
        ("arrow",    "→ 화살표",    "화살표"),
        ("math",     "∑ 수학",      "수학"),
        ("symbol",   "© 기호",      "기호"),
        ("currency", "$ 통화",      "통화"),
        ("super",    "² 위첨자",    "위첨자"),
        ("emoji",    "😀 이모지",   "이모지"),
    };

    public PopupWindow(StorageService storage)
    {
        _storage = storage;
        InitializeComponent();
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); RefreshGrid(); };
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        // 설정 복원
        if (double.TryParse(_storage.GetSetting("char_font_size"), out double fs))
            _charFontSize = Math.Clamp(fs, 14, 36);
        _pinned = _storage.GetSetting("pinned") == "1";

        BuildTabs();
        UpdatePinBtn();
        _initialized = true;
        var lastTab = _storage.GetSetting("last_tab") ?? "recent";
        SwitchTab(lastTab);
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
            ShowClipboardHint();
        }, DispatcherPriority.Loaded);
    }

    // ── 클립보드 단일 문자 인식 → 상태바 힌트 ──────────────────────────
    private void ShowClipboardHint()
    {
        try
        {
            var cb = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(cb) || cb.Length > 2) return;
            var entry = CharDatabase.AllByChar.GetValueOrDefault(cb);
            if (entry == null) return;

            var cp = new System.Text.StringBuilder();
            for (int i = 0; i < cb.Length; )
            {
                int code = char.ConvertToUtf32(cb, i);
                if (cp.Length > 0) cp.Append(' ');
                cp.Append($"U+{code:X4}");
                i += char.IsSurrogatePair(cb, i) ? 2 : 1;
            }
            StatusText.Text = $"클립보드: {cb}  {entry.Name}  {cp}";
        }
        catch { }
    }

    // ── 핀 고정 ──────────────────────────────────────────────────────────
    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        _storage.SetSetting("pinned", _pinned ? "1" : "0");
        UpdatePinBtn();
    }

    private void UpdatePinBtn()
    {
        PinBtn.Foreground = _pinned
            ? (SolidColorBrush)FindResource("AccentBrush")
            : (SolidColorBrush)FindResource("TextSecondary");
        PinBtn.ToolTip = _pinned ? "핀 고정 ON — 삽입 후 팝업 유지 (이전 창에 자동 붙여넣기)" : "핀 고정 — 삽입 후 팝업 유지 (이전 창에 자동 붙여넣기)";
    }

    // ── 탭 버튼 생성 ────────────────────────────────────────────────────
    private void BuildTabs()
    {
        TabPanel.Children.Clear();
        foreach (var (id, label, _) in Tabs)
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
        // 검색 중이면 검색어 유지 + 카테고리 필터만 적용
        SwitchTab((string)btn.Tag);
    }

    private void SwitchTab(string tabId)
    {
        _activeTab = tabId;
        _storage.SetSetting("last_tab", tabId);

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

        // ItemSize 동기화
        CharGrid.ItemWidth  = ItemSize;
        CharGrid.ItemHeight = ItemSize;

        var query = SearchBox.Text.Trim();
        IEnumerable<CharEntry> entries;
        bool isSearchResult = !string.IsNullOrEmpty(query);

        if (isSearchResult)
        {
            var results = CharDatabase.Search(query);
            // 카테고리 탭 선택 시 해당 카테고리만 필터링 (recent/favorite 제외)
            if (_activeTab != "recent" && _activeTab != "favorite")
                results = results.Where(e => e.Category == _activeTab);
            entries = results;

            var statusName = Tabs.FirstOrDefault(t => t.Id == _activeTab).StatusName ?? "";
            StatusText.Text = (_activeTab is "recent" or "favorite")
                ? "검색 결과"
                : $"{statusName} 검색";
        }
        else
        {
            entries = _activeTab switch
            {
                "recent"   => _storage.GetRecents()
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c))
                                .Where(e => e is not null).Select(e => e!),
                "favorite" => _storage.GetFavorites()
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c))
                                .Where(e => e is not null).Select(e => e!),
                _          => CharDatabase.GetByCategory(_activeTab),
            };
            StatusText.Text = Tabs.FirstOrDefault(t => t.Id == _activeTab).StatusName
                              ?? _activeTab;
        }

        var list = entries.ToList();
        StatusText.Text += $" ({list.Count}개)";

        // 결과 없음 안내
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // favorites를 한 번만 조회해서 HashSet으로 변환 (N+1 DB 쿼리 방지)
        var favSet = _storage.GetFavorites().ToHashSet();

        CharGrid.Children.Clear();
        foreach (var entry in list)
            CharGrid.Children.Add(MakeCharButton(entry, favSet, isSearchResult));
    }

    // ── Unicode 코드포인트 툴팁 생성 ────────────────────────────────────
    private static string GetUnicodeTooltip(CharEntry entry)
    {
        var codepoints = new System.Text.StringBuilder();
        for (int i = 0; i < entry.Char.Length; )
        {
            int cp = char.ConvertToUtf32(entry.Char, i);
            if (codepoints.Length > 0) codepoints.Append(' ');
            codepoints.Append($"U+{cp:X4}");
            i += char.IsSurrogatePair(entry.Char, i) ? 2 : 1;
        }
        return $"{entry.Char}  {entry.Name}  {codepoints}";
    }

    // ── 문자 버튼 생성 ──────────────────────────────────────────────────
    private UIElement MakeCharButton(CharEntry entry, HashSet<string> favSet, bool isSearchResult = false)
    {
        bool isFav = favSet.Contains(entry.Char);
        double size = ItemSize - 4;

        var grid = new Grid { Width = size, Height = size, Margin = new Thickness(2) };

        var border = new Border
        {
            Background      = (SolidColorBrush)FindResource("TabInactive"),
            BorderBrush     = isSearchResult ? (SolidColorBrush)FindResource("AccentBrush") : System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(isSearchResult ? 1 : 0),
            CornerRadius    = new CornerRadius(8),
            Cursor          = System.Windows.Input.Cursors.Hand,
            ToolTip         = GetUnicodeTooltip(entry),
            Focusable       = true,
            Tag             = entry,
        };

        var tb = new TextBlock
        {
            Text                = entry.Char,
            FontSize            = _charFontSize,
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
            case Key.F:
                if (border.Tag is CharEntry favEntry)
                {
                    // 즐겨찾기 토글 — 별표 TextBlock은 border 부모 Grid의 두 번째 Child
                    if (border.Parent is Grid g && g.Children.Count > 1 && g.Children[1] is TextBlock starTb)
                        ToggleFavorite(favEntry, starTb);
                }
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
        double w = CharGrid.ActualWidth > 0 ? CharGrid.ActualWidth : (Width - 24);
        return Math.Max(1, (int)(w / ItemSize));
    }

    // ── Ctrl+휠: 문자 크기 조절 ─────────────────────────────────────────
    protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            _charFontSize = Math.Clamp(_charFontSize + (e.Delta > 0 ? 2 : -2), 14, 36);
            _storage.SetSetting("char_font_size", _charFontSize.ToString());
            RefreshGrid();
            e.Handled = true;
            return;
        }
        base.OnPreviewMouseWheel(e);
    }

    // ── 문자 삽입 ────────────────────────────────────────────────────────
    private async void InsertChar(CharEntry entry)
    {
        System.Windows.Clipboard.SetText(entry.Char);
        _storage.AddRecent(entry.Char);

        if (_pinned)
        {
            // 핀 고정 모드: 팝업 유지 + 이전 창에 붙여넣기 후 팝업 다시 활성화
            StatusText.Text = $"삽입됨: {entry.Char}  {entry.Name}";
            if (_targetHwnd != IntPtr.Zero)
            {
                await Task.Delay(50);
                InputHelper.PasteToWindow(_targetHwnd);
                await Task.Delay(100);
                // 팝업을 다시 최상위로 가져옴
                SetForegroundWindow(new WindowInteropHelper(this).Handle);
            }
            return;
        }

        Hide();

        // 이전 창에 Ctrl+V (Hide 후 80ms 대기 → 포커스 전환 완료 보장)
        if (_targetHwnd != IntPtr.Zero)
        {
            await Task.Delay(80);
            InputHelper.PasteToWindow(_targetHwnd);
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
        _searchTimer?.Stop();
        if (string.IsNullOrEmpty(SearchBox.Text))
            RefreshGrid();   // 검색어 지울 때는 즉시 갱신
        else
            _searchTimer?.Start();
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

    public void RefreshIfRecentTab()
    {
        if (_activeTab == "recent") RefreshGrid();
    }

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
    {
        // 스크롤바 조작 시에는 오버레이 닫지 않음
        if (e.OriginalSource is DependencyObject src && IsInsideScrollBar(src)) return;
        HelpOverlay.Visibility = Visibility.Collapsed;
    }

    private static bool IsInsideScrollBar(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ScrollBar) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible && !_pinned) Hide();
    }
}
