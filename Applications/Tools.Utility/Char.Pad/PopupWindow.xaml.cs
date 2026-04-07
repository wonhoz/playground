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
    private bool      _suppressSizeChanged = false;
    private string    _lastStatusText = "";
    private bool      _customSortByName = false;

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
        ("box",      "─ 선/박스",   "선·박스"),
        ("emoji",    "😀 이모지",   "이모지"),
        ("custom",   "+ 사용자",    "사용자 정의"),
    };

    public PopupWindow(StorageService storage)
    {
        _storage = storage;
        InitializeComponent();
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); RefreshGrid(); };
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
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
        _suppressSizeChanged = true;
        if (double.TryParse(_storage.GetSetting("popup_width"), out double pw))
            Width = Math.Clamp(pw, 400, 900);
        if (double.TryParse(_storage.GetSetting("popup_height"), out double ph))
            Height = Math.Clamp(ph, 300, 700);
        _suppressSizeChanged = false;

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

        // 핀 고정 상태: 팝업 테두리 색상으로 시각적 강조
        RootBorder.BorderBrush = _pinned
            ? (SolidColorBrush)FindResource("AccentBrush")
            : (SolidColorBrush)FindResource("BorderBrush");
        RootBorder.BorderThickness = new Thickness(_pinned ? 2 : 1);
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
            // 활성 탭이 스크롤 영역 밖에 있을 때 자동 스크롤
            if (active) b.BringIntoView();
        }

        // 탭별 헤더 버튼 표시/숨김
        ClearRecentsBtn.Visibility = tabId == "recent"   ? Visibility.Visible : Visibility.Collapsed;
        FavMoveUpBtn.Visibility    = tabId == "favorite" ? Visibility.Visible : Visibility.Collapsed;
        FavMoveDownBtn.Visibility  = tabId == "favorite" ? Visibility.Visible : Visibility.Collapsed;
        SortCustomBtn.Visibility   = tabId == "custom"   ? Visibility.Visible : Visibility.Collapsed;

        RefreshGrid();
    }

    // ── 문자 그리드 갱신 ────────────────────────────────────────────────
    private void RefreshGrid()
    {
        if (!_initialized) return;

        // ItemSize 동기화
        CharGrid.ItemWidth  = ItemSize;
        CharGrid.ItemHeight = ItemSize;

        // recents/favorites를 한 번씩만 조회 — 탭 entries 계산 + 즐겨찾기 별표 표시에 모두 재사용
        var recentsList   = _storage.GetRecents();
        var favoritesList = _storage.GetFavorites();  // sort_order 순서 보장
        var favSet        = favoritesList.ToHashSet();

        var query = SearchBox.Text.Trim();
        IEnumerable<CharEntry> entries;
        bool isSearchResult = !string.IsNullOrEmpty(query);

        if (isSearchResult)
        {
            // 커스텀 탭: CharDatabase가 아닌 StorageService에서 검색
            if (_activeTab == "custom")
            {
                var lowerQuery = query.ToLowerInvariant();
                entries = _storage.GetCustomChars()
                    .Where(t => t.Name.ToLowerInvariant().Contains(lowerQuery)
                             || t.Char.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(t => new CharEntry(t.Char, t.Name, "custom"));
                StatusText.Text = $"{Tabs.FirstOrDefault(t => t.Id == "custom").StatusName ?? "사용자 정의"} 검색";
            }
            else
            {
                var results = CharDatabase.Search(query);
                // 카테고리 탭 선택 시 해당 카테고리만 필터링 (recent/favorite 제외)
                if (_activeTab != "recent" && _activeTab != "favorite")
                    results = results.Where(e => e.Category == _activeTab);
                entries = results;

                // 전체 탭 검색 시 카테고리별 통계 표시
                if (_activeTab is "recent" or "favorite")
                {
                    var grouped = results.GroupBy(e => e.Category)
                        .OrderByDescending(g => g.Count())
                        .Select(g =>
                        {
                            var tabLabel = Tabs.FirstOrDefault(t => t.Id == g.Key).StatusName ?? g.Key;
                            return $"{tabLabel} {g.Count()}";
                        });
                    StatusText.Text = string.Join(" · ", grouped.Take(4));
                    if (!StatusText.Text.Any()) StatusText.Text = "검색 결과";
                }
                else
                {
                    var statusName = Tabs.FirstOrDefault(t => t.Id == _activeTab).StatusName ?? "";
                    StatusText.Text = $"{statusName} 검색";
                }
            }
        }
        else
        {
            entries = _activeTab switch
            {
                "recent"   => recentsList
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c))
                                .Where(e => e is not null).Select(e => e!),
                "favorite" => favoritesList  // sort_order 순서 유지
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c))
                                .Where(e => e is not null).Select(e => e!),
                "custom"   => (_customSortByName
                                ? (IEnumerable<(string Char, string Name)>)_storage.GetCustomChars().OrderBy(t => t.Name)
                                : _storage.GetCustomChars())
                                .Select(t => new CharEntry(t.Char, t.Name, "custom")),
                // 카테고리 탭: 최근 사용한 문자를 앞에 표시
                _ => SortByRecent(CharDatabase.GetByCategory(_activeTab), recentsList),
            };
            StatusText.Text = Tabs.FirstOrDefault(t => t.Id == _activeTab).StatusName
                              ?? _activeTab;
        }

        var list = entries.ToList();
        StatusText.Text += $" ({list.Count}개)";
        _lastStatusText = StatusText.Text;

        // 결과 없음 안내
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        CharGrid.Children.Clear();
        foreach (var entry in list)
            CharGrid.Children.Add(MakeCharButton(entry, favSet, isSearchResult));
    }

    // ── 카테고리 탭: 최근 사용 문자를 앞으로 정렬 ──────────────────────
    private static IEnumerable<CharEntry> SortByRecent(IEnumerable<CharEntry> entries, List<string> recents)
    {
        var recentRank = recents
            .Select((c, i) => (c, i))
            .ToDictionary(x => x.c, x => x.i);
        return entries.OrderBy(e =>
            recentRank.TryGetValue(e.Char, out int rank) ? rank : int.MaxValue);
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

        // 검색 결과 카테고리 배지 (하단 좌측 소형 레이블)
        var catLabel = new TextBlock
        {
            Text                = Tabs.FirstOrDefault(t => t.Id == entry.Category).StatusName ?? entry.Category,
            FontSize            = 7,
            FontFamily          = new WpfFontFamily("Segoe UI"),
            Foreground          = (SolidColorBrush)FindResource("AccentHover"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Bottom,
            Margin              = new Thickness(3, 0, 0, 2),
            IsHitTestVisible    = false,
            Visibility          = isSearchResult ? Visibility.Visible : Visibility.Collapsed,
        };

        grid.Children.Add(border);
        grid.Children.Add(star);
        grid.Children.Add(catLabel);

        // 호버 / 포커스 시각 피드백 + 상태바 미리보기
        border.MouseEnter += (_, _) =>
        {
            border.Background = (SolidColorBrush)FindResource("CharHover");
            StatusText.Text = GetUnicodeTooltip(entry);
        };
        border.MouseLeave += (_, _) =>
        {
            if (!border.IsKeyboardFocused)
                border.Background = (SolidColorBrush)FindResource("TabInactive");
            StatusText.Text = _lastStatusText;
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

        // 우클릭: 즐겨찾기 토글 / 사용자 정의 탭에서는 편집/삭제 선택
        border.MouseRightButtonUp += (_, ev) =>
        {
            ev.Handled = true;
            if (entry.Category == "custom")
                ShowCustomCharContextMenu(entry, border);
            else
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
        try
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
                    await InputHelper.PasteToWindowAsync(_targetHwnd);
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
                await InputHelper.PasteToWindowAsync(_targetHwnd);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삽입 실패: {ex.Message}";
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
            StatusText.Text = $"즐겨찾기 제거: {entry.Char}  {entry.Name}";
        }
        else
        {
            _storage.AddFavorite(entry.Char);
            star.Visibility = Visibility.Visible;
            StatusText.Text = $"즐겨찾기 추가: {entry.Char}  {entry.Name}";
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
        // Alt+1~9: 탭 전환 (Alt 키는 e.SystemKey로 확인)
        else if (e.Key == Key.System && e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
        {
            int idx = e.SystemKey switch
            {
                Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4,
                Key.D6 => 5, Key.D7 => 6, Key.D8 => 7, Key.D9 => 8,
                _ => -1
            };
            if (idx >= 0 && idx < Tabs.Length)
            {
                SwitchTab(Tabs[idx].Id);
                e.Handled = true;
            }
        }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressSizeChanged || !_initialized) return;
        _storage.SetSetting("popup_width",  Width.ToString());
        _storage.SetSetting("popup_height", Height.ToString());
    }

    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_SIZE_BOTTOM_RIGHT = 0xF008;

    private void ResizeGrip_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            ReleaseMouseCapture();
            SendMessage(hwnd, WM_SYSCOMMAND, new IntPtr(0xF008), IntPtr.Zero);
        }
    }

    public void RefreshIfRecentTab()
    {
        if (_activeTab == "recent") RefreshGrid();
    }

    public void Refresh() => RefreshGrid();

    public void ShowHelpOverlay()
        => Dispatcher.BeginInvoke(() => HelpOverlay.Visibility = Visibility.Visible,
                                  DispatcherPriority.Loaded);

    // ── 사용자 정의 문자 삭제 ────────────────────────────────────────────
    private void DeleteCustomChar(CharEntry entry)
    {
        _storage.RemoveCustomChar(entry.Char);
        StatusText.Text = $"삭제됨: {entry.Char}  {entry.Name}";
        RefreshGrid();
    }

    // ── 커스텀 문자 우클릭 컨텍스트 메뉴 (편집 / 삭제) ──────────────────
    private void ShowCustomCharContextMenu(CharEntry entry, UIElement anchor)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Background    = (SolidColorBrush)FindResource("SurfaceBrush");
        menu.BorderBrush   = (SolidColorBrush)FindResource("BorderBrush");
        menu.BorderThickness = new Thickness(1);

        var editItem = new System.Windows.Controls.MenuItem
        {
            Header     = $"✏  이름 수정  ({entry.Char})",
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            Background = System.Windows.Media.Brushes.Transparent,
        };
        editItem.Click += (_, _) =>
        {
            var dlg = new AddCustomCharDialog(entry.Char, entry.Name) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _storage.UpdateCustomChar(dlg.ResultChar, dlg.ResultName);
                StatusText.Text = $"수정됨: {dlg.ResultChar}  {dlg.ResultName}";
                RefreshGrid();
            }
        };

        var deleteItem = new System.Windows.Controls.MenuItem
        {
            Header     = $"🗑  삭제  ({entry.Char})",
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            Background = System.Windows.Media.Brushes.Transparent,
        };
        deleteItem.Click += (_, _) => DeleteCustomChar(entry);

        menu.Items.Add(editItem);
        menu.Items.Add(deleteItem);
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    // ── 커스텀 문자 추가 ─────────────────────────────────────────────────
    private void AddCustomBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddCustomCharDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // 이미 등록된 문자인 경우 덮어쓰기 확인
            if (_storage.IsCustomChar(dlg.ResultChar))
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"'{dlg.ResultChar}' 는 이미 등록되어 있습니다.\n이름을 '{dlg.ResultName}'(으)로 덮어쓰시겠습니까?",
                    "중복 문자 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }
            _storage.AddCustomChar(dlg.ResultChar, dlg.ResultName);
            SwitchTab("custom");
            StatusText.Text = $"추가됨: {dlg.ResultChar}  {dlg.ResultName}";
        }
    }

    // ── 최근 사용 초기화 ──────────────────────────────────────────────────
    private void ClearRecentsBtn_Click(object sender, RoutedEventArgs e)
    {
        _storage.ClearRecents();
        StatusText.Text = "최근 사용 목록이 초기화되었습니다";
        RefreshGrid();
    }

    // ── 즐겨찾기 순서 이동 ───────────────────────────────────────────────
    private void FavMoveUpBtn_Click(object sender, RoutedEventArgs e)   => MoveFocusedFavorite(-1);
    private void FavMoveDownBtn_Click(object sender, RoutedEventArgs e) => MoveFocusedFavorite(+1);

    private void MoveFocusedFavorite(int delta)
    {
        // 현재 포커스된 문자 버튼의 CharEntry를 찾아 이동
        var focused = FocusManager.GetFocusedElement(this) as Border;
        if (focused?.Tag is not CharEntry entry) return;
        _storage.MoveFavorite(entry.Char, delta);
        StatusText.Text = $"순서 변경: {entry.Char}  {entry.Name}";
        RefreshGrid();
        // 이동 후 동일 문자에 포커스 복원
        for (int i = 0; i < CharGrid.Children.Count; i++)
        {
            if (CharGrid.Children[i] is Grid g && g.Children[0] is Border b && b.Tag is CharEntry ce && ce.Char == entry.Char)
            { b.Focus(); break; }
        }
    }

    // ── 커스텀 탭 정렬 전환 ──────────────────────────────────────────────
    private void SortCustomBtn_Click(object sender, RoutedEventArgs e)
    {
        _customSortByName = !_customSortByName;
        SortCustomBtn.Content  = _customSortByName ? "↑A" : "↕";
        SortCustomBtn.ToolTip  = _customSortByName ? "정렬: 이름순 (클릭하면 추가순으로 전환)" : "정렬 전환 (추가순 / 이름순)";
        RefreshGrid();
    }

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
        // AddCustomCharDialog 등 OwnedWindow가 열려 있으면 숨기지 않음
        if (IsVisible && !_pinned && OwnedWindows.Count == 0) Hide();
    }
}
