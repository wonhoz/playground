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
    private bool      _preserveClipboard = false;

    // 검색 히스토리 (세션 내 메모리, 최대 10개)
    private readonly List<string> _searchHistory = new();
    private int                   _searchHistoryIdx = -1;

    // 다중 문자 선택 (Ctrl+클릭)
    private readonly List<CharEntry> _multiSelected = new();

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

        _customSortByName  = _storage.GetSetting("custom_sort_by_name")  == "1";
        _preserveClipboard = _storage.GetSetting("preserve_clipboard")  == "1";
        BuildTabs();
        UpdatePinBtn();
        UpdateClipboardPreserveBtn();
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
        _multiSelected.Clear();  // 탭 전환 시 다중 선택 해제
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

        var query = SearchBox.Text.Trim();
        bool isSearchResult = !string.IsNullOrEmpty(query);

        // favorites: favSet은 모든 탭에서 별표 표시에 필요 — 항상 조회
        var favoritesList = _storage.GetFavorites();  // sort_order 순서 보장
        var favSet        = favoritesList.ToHashSet();
        // recents: 커스텀 탭 비검색 시 불필요 — 건너뜀
        bool needRecents = isSearchResult || _activeTab != "custom";
        var recentsList   = needRecents ? _storage.GetRecents() : new List<string>();
        // 커스텀 문자 조회 — 최근 탭·검색·즐겨찾기 탭 등 공통 재사용
        var customChars   = _storage.GetCustomChars();
        var customLookup  = customChars.ToDictionary(t => t.Char, t => t.Name);
        // 최근 탭 use_count 딕셔너리 (툴팁 "N회 사용" 표시용 — 최근 탭에서만 조회)
        Dictionary<string, int>? recentUseCounts = null;
        if (!isSearchResult && _activeTab == "recent")
            recentUseCounts = _storage.GetRecentUseCounts();

        IEnumerable<CharEntry> entries;

        if (isSearchResult)
        {
            // U+XXXX 코드포인트 직접 검색
            if (query.Length >= 3 && query.StartsWith("U+", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(query[2..], System.Globalization.NumberStyles.HexNumber, null, out int codepoint) &&
                codepoint is >= 0x20 and <= 0x10FFFF)
            {
                try
                {
                    var ch = char.ConvertFromUtf32(codepoint);
                    var found = CharDatabase.AllByChar.GetValueOrDefault(ch)
                             ?? (customLookup.TryGetValue(ch, out var cn2) ? new CharEntry(ch, cn2, "custom") : null)
                             ?? new CharEntry(ch, $"U+{codepoint:X4}", "symbol");
                    entries = [found];
                    StatusText.Text = $"U+{codepoint:X4} 검색";
                }
                catch { entries = []; StatusText.Text = "잘못된 코드포인트"; }
            }
            // 커스텀 탭: CharDatabase가 아닌 StorageService에서 검색
            else if (_activeTab == "custom")
            {
                var lowerQuery = query.ToLowerInvariant();
                entries = customChars
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
                {
                    results = results.Where(e => e.Category == _activeTab);
                    entries = results;
                    var statusName = Tabs.FirstOrDefault(t => t.Id == _activeTab).StatusName ?? "";
                    StatusText.Text = $"{statusName} 검색";
                }
                else
                {
                    // recent/favorite 탭 전체 검색: CharDatabase + 커스텀 문자 병합
                    var lq = query.ToLowerInvariant();
                    var customResults = customChars
                        .Where(t => t.Name.ToLowerInvariant().Contains(lq)
                                 || t.Char.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Select(t => new CharEntry(t.Char, t.Name, "custom"));
                    var merged = results.Concat(customResults);
                    entries = merged;

                    // 카테고리별 통계 표시
                    var grouped = merged.GroupBy(e => e.Category)
                        .OrderByDescending(g => g.Count())
                        .Select(g =>
                        {
                            var tabLabel = Tabs.FirstOrDefault(t => t.Id == g.Key).StatusName ?? g.Key;
                            return $"{tabLabel} {g.Count()}";
                        });
                    StatusText.Text = string.Join(" · ", grouped.Take(4));
                    if (!StatusText.Text.Any()) StatusText.Text = "검색 결과";
                }
            }
        }
        else
        {
            entries = _activeTab switch
            {
                "recent"   => recentsList
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c)
                                         ?? (customLookup.TryGetValue(c, out var cn)
                                             ? new CharEntry(c, cn, "custom") : null))
                                .Where(e => e is not null).Select(e => e!),
                "favorite" => favoritesList  // sort_order 순서 유지 (커스텀 문자 포함)
                                .Select(c => CharDatabase.AllByChar.GetValueOrDefault(c)
                                         ?? (customLookup.TryGetValue(c, out var fn)
                                             ? new CharEntry(c, fn, "custom") : null))
                                .Where(e => e is not null).Select(e => e!),
                "custom"   => (_customSortByName
                                ? (IEnumerable<(string Char, string Name)>)customChars.OrderBy(t => t.Name)
                                : customChars)
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

        // 탭 배지 업데이트 (즐겨찾기·커스텀·최근 수)
        UpdateTabBadge("favorite", favoritesList.Count);
        UpdateTabBadge("custom",   customChars.Count);
        UpdateTabBadge("recent",   recentsList.Count);

        // 검색어 히스토리 추가 (2자 이상의 의미 있는 쿼리만)
        if (isSearchResult && query.Length >= 2)
        {
            _searchHistory.Remove(query);
            _searchHistory.Insert(0, query);
            if (_searchHistory.Count > 10) _searchHistory.RemoveAt(_searchHistory.Count - 1);
            _searchHistoryIdx = -1;
        }

        // 결과 없음 안내
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        CharGrid.Children.Clear();
        foreach (var entry in list)
        {
            int? uc = (recentUseCounts != null && recentUseCounts.TryGetValue(entry.Char, out int cnt)) ? cnt : null;
            CharGrid.Children.Add(MakeCharButton(entry, favSet, isSearchResult, uc));
        }
    }

    // ── 탭 배지 카운트 업데이트 ─────────────────────────────────────────
    private void UpdateTabBadge(string tabId, int count)
    {
        var tabDef = Tabs.FirstOrDefault(t => t.Id == tabId);
        if (string.IsNullOrEmpty(tabDef.Id)) return;
        string label = count > 0 ? $"{tabDef.Label} ({count})" : tabDef.Label;
        foreach (WpfButton b in TabPanel.Children)
        {
            if ((string)b.Tag == tabId) { b.Content = label; break; }
        }
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
    private static string GetUnicodeTooltip(CharEntry entry, int? useCount = null)
    {
        var codepoints = new System.Text.StringBuilder();
        for (int i = 0; i < entry.Char.Length; )
        {
            int cp = char.ConvertToUtf32(entry.Char, i);
            if (codepoints.Length > 0) codepoints.Append(' ');
            codepoints.Append($"U+{cp:X4}");
            i += char.IsSurrogatePair(entry.Char, i) ? 2 : 1;
        }
        var tip = $"{entry.Char}  {entry.Name}  {codepoints}";
        if (useCount.HasValue && useCount.Value > 1)
            tip += $"  ({useCount.Value}회 사용)";
        return tip;
    }

    // ── 문자 버튼 생성 ──────────────────────────────────────────────────
    private UIElement MakeCharButton(CharEntry entry, HashSet<string> favSet, bool isSearchResult = false, int? useCount = null)
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
            ToolTip         = GetUnicodeTooltip(entry, useCount),
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
            StatusText.Text = GetUnicodeTooltip(entry, useCount);
        };
        border.MouseLeave += (_, _) =>
        {
            if (!border.IsKeyboardFocused)
                border.Background = (SolidColorBrush)FindResource("TabInactive");
            StatusText.Text = _lastStatusText;
        };
        border.GotKeyboardFocus  += (_, _) => border.Background = (SolidColorBrush)FindResource("CharHover");
        border.LostKeyboardFocus += (_, _) => border.Background = (SolidColorBrush)FindResource("TabInactive");

        // 다중 선택 상태이면 테두리 강조 복원
        if (_multiSelected.Any(e => e.Char == entry.Char))
        {
            border.BorderBrush     = (SolidColorBrush)FindResource("AccentHover");
            border.BorderThickness = new Thickness(2);
        }

        // 좌클릭: Ctrl → 다중 선택 / Shift → 복사만 / 기본 → 삽입
        border.MouseLeftButtonUp += (_, _) =>
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                ToggleMultiSelect(entry, border);
            }
            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                CopyCharOnly(entry);
            }
            else if (_multiSelected.Count > 0)
            {
                // 다중 선택 중 일반 클릭 → 해당 문자 추가 후 즉시 연속 삽입
                if (!_multiSelected.Any(e => e.Char == entry.Char))
                    _multiSelected.Add(entry);
                _ = InsertMultipleCharsAsync();
            }
            else
            {
                InsertChar(entry);
            }
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
            // 클립보드 유지 모드: 삽입 전 원본 클립보드 내용 백업
            string? prevClip = _preserveClipboard ? TryGetClipboardText() : null;

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
                    if (!string.IsNullOrEmpty(prevClip)) TrySetClipboardText(prevClip);
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
                if (!string.IsNullOrEmpty(prevClip))
                {
                    await Task.Delay(100); // 붙여넣기 완료 후 복원
                    TrySetClipboardText(prevClip);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삽입 실패: {ex.Message}";
        }
    }

    private static string? TryGetClipboardText()
    {
        try { return System.Windows.Clipboard.GetText(); } catch { return null; }
    }

    private static void TrySetClipboardText(string text)
    {
        try { System.Windows.Clipboard.SetText(text); } catch { }
    }

    // ── 클립보드 유지 토글 ────────────────────────────────────────────────
    private void ClipboardPreserveBtn_Click(object sender, RoutedEventArgs e)
    {
        _preserveClipboard = !_preserveClipboard;
        _storage.SetSetting("preserve_clipboard", _preserveClipboard ? "1" : "0");
        UpdateClipboardPreserveBtn();
    }

    private void UpdateClipboardPreserveBtn()
    {
        ClipboardPreserveBtn.Foreground = _preserveClipboard
            ? (SolidColorBrush)FindResource("AccentBrush")
            : (SolidColorBrush)FindResource("TextSecondary");
        ClipboardPreserveBtn.ToolTip = _preserveClipboard
            ? "클립보드 유지 ON — 삽입 후 원본 클립보드 복원 중"
            : "클립보드 유지 — 삽입 후 원본 클립보드 복원";
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
        _searchHistoryIdx = -1;  // 직접 편집 시 히스토리 탐색 인덱스 초기화
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
        else if (e.Key == Key.Up)
        {
            // 이전 검색어 히스토리 순환 (↑ 키)
            if (_searchHistory.Count > 0)
            {
                _searchHistoryIdx = Math.Min(_searchHistoryIdx + 1, _searchHistory.Count - 1);
                SearchBox.Text = _searchHistory[_searchHistoryIdx];
                SearchBox.CaretIndex = SearchBox.Text.Length;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            // 다중 선택 삽입 우선, 없으면 첫 번째 문자 삽입
            if (_multiSelected.Count > 0)
            {
                _ = InsertMultipleCharsAsync();
            }
            else if (CharGrid.Children.Count > 0 &&
                CharGrid.Children[0] is Grid g && g.Children.Count > 0 &&
                g.Children[0] is Border b && b.Tag is CharEntry entry)
            {
                InsertChar(entry);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _multiSelected.Count > 0)
        {
            // 다중 선택 취소
            ClearMultiSelection();
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

    // ── 커스텀 문자 우클릭 컨텍스트 메뉴 (즐겨찾기 · 편집 · 삭제) ──────
    private void ShowCustomCharContextMenu(CharEntry entry, UIElement anchor)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Background    = (SolidColorBrush)FindResource("SurfaceBrush");
        menu.BorderBrush   = (SolidColorBrush)FindResource("BorderBrush");
        menu.BorderThickness = new Thickness(1);

        bool isFav = _storage.IsFavorite(entry.Char);
        var favItem = new System.Windows.Controls.MenuItem
        {
            Header     = isFav ? $"★  즐겨찾기 제거  ({entry.Char})" : $"☆  즐겨찾기 추가  ({entry.Char})",
            Foreground = isFav
                ? (SolidColorBrush)FindResource("FavColor")
                : (SolidColorBrush)FindResource("TextPrimary"),
            Background = System.Windows.Media.Brushes.Transparent,
        };
        favItem.Click += (_, _) =>
        {
            // 즐겨찾기 토글 — 별표 TextBlock은 anchor(border)의 부모 Grid 두 번째 Child
            TextBlock? starTb = null;
            if (anchor is Border b && b.Parent is Grid g && g.Children.Count > 1 && g.Children[1] is TextBlock st)
                starTb = st;
            if (starTb != null)
                ToggleFavorite(entry, starTb);
            else
            {
                if (_storage.IsFavorite(entry.Char)) _storage.RemoveFavorite(entry.Char);
                else _storage.AddFavorite(entry.Char);
                RefreshGrid();
            }
        };

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

        menu.Items.Add(favItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
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

    // ── 다중 선택 ────────────────────────────────────────────────────────
    private void ToggleMultiSelect(CharEntry entry, Border border)
    {
        var existing = _multiSelected.FirstOrDefault(e => e.Char == entry.Char);
        if (existing != null)
        {
            _multiSelected.Remove(existing);
            border.BorderBrush     = System.Windows.Media.Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
        }
        else
        {
            _multiSelected.Add(entry);
            border.BorderBrush     = (SolidColorBrush)FindResource("AccentHover");
            border.BorderThickness = new Thickness(2);
        }
        UpdateMultiSelectStatus();
    }

    private void ClearMultiSelection()
    {
        _multiSelected.Clear();
        // 그리드 재렌더링으로 테두리 초기화
        RefreshGrid();
        StatusText.Text = _lastStatusText;
    }

    private void UpdateMultiSelectStatus()
    {
        if (_multiSelected.Count == 0)
            StatusText.Text = _lastStatusText;
        else
        {
            var preview = string.Concat(_multiSelected.Select(e => e.Char));
            StatusText.Text = $"{_multiSelected.Count}개 선택됨: {preview}  — Enter 또는 클릭으로 삽입, Esc로 취소";
        }
    }

    private async Task InsertMultipleCharsAsync()
    {
        if (_multiSelected.Count == 0) return;
        var combined = string.Concat(_multiSelected.Select(e => e.Char));

        try
        {
            string? prevClip = _preserveClipboard ? TryGetClipboardText() : null;
            System.Windows.Clipboard.SetText(combined);
            foreach (var entry in _multiSelected) _storage.AddRecent(entry.Char);

            if (_pinned)
            {
                StatusText.Text = $"삽입됨: {combined}";
                if (_targetHwnd != IntPtr.Zero)
                {
                    await Task.Delay(50);
                    await InputHelper.PasteToWindowAsync(_targetHwnd);
                    await Task.Delay(100);
                    if (!string.IsNullOrEmpty(prevClip)) TrySetClipboardText(prevClip);
                    SetForegroundWindow(new WindowInteropHelper(this).Handle);
                }
            }
            else
            {
                Hide();
                if (_targetHwnd != IntPtr.Zero)
                {
                    await Task.Delay(80);
                    await InputHelper.PasteToWindowAsync(_targetHwnd);
                    if (!string.IsNullOrEmpty(prevClip))
                    {
                        await Task.Delay(100);
                        TrySetClipboardText(prevClip);
                    }
                }
            }
            _multiSelected.Clear();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삽입 실패: {ex.Message}";
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
        _storage.SetSetting("custom_sort_by_name", _customSortByName ? "1" : "0");
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
