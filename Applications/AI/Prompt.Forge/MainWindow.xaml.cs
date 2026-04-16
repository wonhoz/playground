using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Prompt.Forge.Views;

namespace Prompt.Forge;

public partial class MainWindow : Window
{

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int  HotkeyId = 0x50;   // Win+Shift+P
    const uint MOD_WIN  = 0x0008;
    const uint MOD_SHIFT = 0x0004;
    const uint VK_P     = 0x50;

    readonly MainViewModel _vm;
    readonly Database      _db;
    readonly AppSettings   _appSettings;
    bool _refreshing;
    readonly List<string>  _recentSearches = [];
    System.Windows.Threading.DispatcherTimer? _contentDebounce;

    // FillVarsDialog에서 프롬프트별 마지막 입력값 기억: [promptId → [varName → value]]
    readonly Dictionary<int, Dictionary<string, string>> _varHistory = [];

    // 클립보드 히스토리 (최근 복사 5개: (제목, 내용))
    readonly List<(string Title, string Content)> _clipboardHistory = [];

    public MainWindow()
    {
        _appSettings = AppSettings.Load();
        _db = new Database(_appSettings.ResolvedDbPath);
        _vm = new MainViewModel(_db, _appSettings.LastSortOrder);
        DataContext = _vm;

        // 저장된 창 위치가 있으면 CenterScreen 비활성
        if (!double.IsNaN(_appSettings.WindowLeft) && _appSettings.WindowLeft >= 0)
            WindowStartupLocation = WindowStartupLocation.Manual;

        InitializeComponent();

        Loaded   += OnLoaded;
        Closing  += OnClosing;
        TxtContent.TextChanged += TxtContent_TextChanged;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        App.ApplyDarkTitleBar(this);

        // 창 위치/크기 복원
        if (!double.IsNaN(_appSettings.WindowLeft) && _appSettings.WindowLeft >= 0)
        {
            Left   = _appSettings.WindowLeft;
            Top    = _appSettings.WindowTop;
            Width  = _appSettings.WindowWidth;
            Height = _appSettings.WindowHeight;
            if (_appSettings.WindowState == "Maximized")
                WindowState = WindowState.Maximized;
        }

        // 전역 단축키 등록 (Win+Shift+P)
        RegisterHotKey(handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_P);

        HwndSource.FromHwnd(handle)?.AddHook(WndProc);

        // 프리셋 콤보 초기화
        RefreshPresetCombo();

        // 필터 콤보 초기화
        RefreshFilterCombos();

        // 마지막 정렬 방식 복원 — CbSort UI만 동기화 (SortOrder는 ViewModel 생성자에서 초기화됨)
        _refreshing = true;
        try
        {
            CbSort.SelectedIndex = _appSettings.LastSortOrder switch
            {
                "use_count" => 1,
                "custom"    => 2,
                _           => 0
            };
        }
        finally { _refreshing = false; }

        // 마지막 선택 항목 복원
        RestoreLastSelection();

        // 최근 검색어 복원
        _recentSearches.AddRange(_appSettings.RecentSearches);

        // 검색 X버튼 표시/숨김
        TxtSearch.TextChanged += (_, _) =>
            BtnClearSearch.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Collapsed : Visibility.Visible;

        // 태그/서비스 자동완성
        TxtTags.TextChanged    += TxtTags_TextChanged;
        TxtService.TextChanged += TxtService_TextChanged;

        // 최근 검색어
        TxtSearch.GotFocus += TxtSearch_GotFocus;
        TxtSearch.LostFocus += (_, _) =>
        {
            // 포커스를 잃을 때 검색어 저장 + 팝업 닫기 (클릭 이벤트 우선을 위해 딜레이)
            var query = TxtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                _recentSearches.Remove(query);
                _recentSearches.Insert(0, query);
                if (_recentSearches.Count > 10) _recentSearches.RemoveAt(10);
                _appSettings.RecentSearches = [.. _recentSearches];
                _appSettings.Save();
            }
            var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(200) };
            t.Tick += (_, _) => { t.Stop(); RecentSearchPopup.IsOpen = false; };
            t.Start();
        };
    }

    // ── 마지막 선택 항목 저장/복원 ─────────────────────────────────────────────

    void SaveLastSelection()
    {
        if (_vm.Selected == null) return;
        _appSettings.LastSelectedId = _vm.Selected.Id;
    }

    void RestoreLastSelection()
    {
        if (_appSettings.LastSelectedId < 0) return;
        var item = _vm.Items.FirstOrDefault(x => x.Id == _appSettings.LastSelectedId);
        if (item != null)
        {
            _vm.Selected = item;
            LoadSelected();
        }
    }

    // ── 키보드 단축키 (B1) ────────────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsLoaded) return;

        // ↑↓: 목록 탐색
        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            if (_vm.Items.Count == 0) return;
            var cur = _vm.Selected == null ? -1 : _vm.Items.IndexOf(_vm.Selected);
            if (e.Key == Key.Up   && cur > 0)                       _vm.Selected = _vm.Items[cur - 1];
            else if (e.Key == Key.Down && cur < _vm.Items.Count - 1) _vm.Selected = _vm.Items[cur + 1];
            else if (cur < 0)                                        _vm.Selected = _vm.Items[0];
            if (_vm.Selected != null) LstItems.ScrollIntoView(_vm.Selected);
            LoadSelected();
            e.Handled = true;
            return;
        }

        // Esc: 검색창 포커스 상태일 때 검색·필터 전체 초기화
        if (e.Key == Key.Escape && TxtSearch.IsFocused)
        {
            ClearSearch_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+F: 즐겨찾기 토글
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && e.Key == Key.F && _vm.HasSelection)
        {
            Fav_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.N)
            {
                NewPrompt_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Ctrl+F: 검색창 포커스 + 전체 선택
            if (e.Key == Key.F)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+G: 본문 내 텍스�� 찾기
            if (e.Key == Key.G)
            {
                OpenFindBar();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P && _vm.HasSelection)
            {
                Pin_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (!_vm.HasSelection) return;

            switch (e.Key)
            {
                case Key.S:
                    Save_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D:
                    Duplicate_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Enter:
                    FillVars_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.H:
                    ShowHistory_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.C when !TxtContent.IsFocused && !TxtTitle.IsFocused
                             && !TxtTags.IsFocused && !TxtService.IsFocused && !TxtNotes.IsFocused:
                    Copy_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            if (Visibility == Visibility.Visible && IsActive)
                Hide();
            else
            {
                Show();
                Activate();
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    void OnClosing(object? s, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HotkeyId);
            _appSettings.LastSortOrder  = _vm.SortOrder;
            _appSettings.RecentSearches = [.. _recentSearches];

            // 창 위치/크기 저장 (최대화 상태가 아닐 때만 좌표 저장)
            _appSettings.WindowState = WindowState == WindowState.Maximized ? "Maximized" : "Normal";
            if (WindowState == WindowState.Normal)
            {
                _appSettings.WindowLeft   = Left;
                _appSettings.WindowTop    = Top;
                _appSettings.WindowWidth  = Width;
                _appSettings.WindowHeight = Height;
            }

            _appSettings.Save();
        }
        finally
        {
            _db.Dispose();
        }
    }

    // ── 필터 갱신 ─────────────────────────────────────────────────────────────

    void RefreshFilterCombos()
    {
        if (!IsLoaded) return;
        bool prev = _refreshing;
        _refreshing = true;
        try
        {
            // Tags.Clear() 후 ComboBox SelectedItem이 -1로 초기화되므로,
            // UI 상태가 아닌 ViewModel 필터 상태를 기준으로 복원
            var selTag = _vm.FilterTag;
            var selSvc = _vm.FilterService;

            CbTag.ItemsSource = _vm.Tags;
            CbTag.SelectedIndex = 0;
            CbService.ItemsSource = _vm.Services;
            CbService.SelectedIndex = 0;

            if (selTag != null)
            {
                int i = _vm.Tags.IndexOf(selTag);
                if (i >= 0) CbTag.SelectedIndex = i;
            }
            if (selSvc != null)
            {
                int i = _vm.Services.IndexOf(selSvc);
                if (i >= 0) CbService.SelectedIndex = i;
            }
        }
        finally
        {
            _refreshing = prev;  // 호출 전 상태로 복원 (중첩 호출 시 올바른 동작 보장)
        }
    }

    // ── 필터 프리셋 ───────────────────────────────────────────────────────────

    void RefreshPresetCombo()
    {
        bool prev = _refreshing;
        _refreshing = true;
        try
        {
            CbPreset.ItemsSource = null;
            var items = new List<string> { "— 프리셋 선택 —" };
            items.AddRange(_appSettings.FilterPresets.Select(p => p.Name));
            CbPreset.ItemsSource = items;
            CbPreset.SelectedIndex = 0;
        }
        finally { _refreshing = prev; }
    }

    void Preset_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _refreshing) return;
        if (CbPreset.SelectedIndex <= 0) return;
        var preset = _appSettings.FilterPresets[CbPreset.SelectedIndex - 1];

        _refreshing = true;
        try
        {
            if (!string.IsNullOrEmpty(preset.Search)) _vm.Search = preset.Search;
            _vm.FilterTag     = preset.Tag;
            _vm.FilterService = preset.Service;
            _vm.FavOnly       = preset.FavOnly;
            RefreshFilterCombos();
        }
        finally { _refreshing = false; }
    }

    void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var hasFilter = _vm.FilterTag != null || _vm.FilterService != null
                     || _vm.FavOnly || !string.IsNullOrWhiteSpace(_vm.Search);
        if (!hasFilter)
        {
            MessageBox.Show("저장할 필터가 없습니다.\n태그·서비스·즐겨찾기·검색어 중 하나 이상 설정 후 저장하세요.",
                "프리셋 저장", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = ShowInputDialog("프리셋 이름을 입력하세요:", "프리셋 저장");
        if (string.IsNullOrWhiteSpace(name)) return;

        _appSettings.FilterPresets.RemoveAll(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        _appSettings.FilterPresets.Add(new FilterPreset(
            name.Trim(), _vm.FilterTag, _vm.FilterService, _vm.FavOnly,
            string.IsNullOrWhiteSpace(_vm.Search) ? null : _vm.Search));
        _appSettings.Save();
        RefreshPresetCombo();
        _vm.StatusText = $"프리셋 '{name}' 저장됨";
    }

    void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (CbPreset.SelectedIndex <= 0) return;
        var preset = _appSettings.FilterPresets[CbPreset.SelectedIndex - 1];
        var r = MessageBox.Show($"프리셋 '{preset.Name}'을 삭제하시겠습니까?",
            "프리셋 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _appSettings.FilterPresets.RemoveAt(CbPreset.SelectedIndex - 1);
        _appSettings.Save();
        RefreshPresetCombo();
        _vm.StatusText = $"프리셋 '{preset.Name}' 삭제됨";
    }

    void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _refreshing) return;
        _refreshing = true;  // VM setter 호출 전에 설정 — Tags.Clear() → SelectionChanged 재진입 차단
        try
        {
            _vm.FilterTag     = CbTag.SelectedIndex <= 0     ? null : CbTag.SelectedItem as string;
            _vm.FilterService = CbService.SelectedIndex <= 0 ? null : CbService.SelectedItem as string;
            _vm.FavOnly       = ChkFav.IsChecked == true;
            RefreshFilterCombos();
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ── 목록 선택 ─────────────────────────────────────────────────────────────

    void LstItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _refreshing) return;

        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is PromptItem prev && IsDirty(prev))
        {
            var result = MessageBox.Show(
                $"'{prev.Title}' 변경 내용을 저장하시겠습니까?",
                "저장 확인", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                // 선택을 이전 항목으로 되돌림
                _refreshing = true;
                try { LstItems.SelectedItem = prev; }
                finally { _refreshing = false; }
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                var nextId = e.AddedItems.Count > 0 && e.AddedItems[0] is PromptItem next
                    ? next.Id : (int?)null;
                ApplyEditToModel(prev);
                _refreshing = true;
                try
                {
                    _vm.Save(prev);
                    if (nextId.HasValue)
                        _vm.Selected = _vm.Items.FirstOrDefault(x => x.Id == nextId);
                }
                finally { _refreshing = false; }
            }
            // No: 변경 내용 버리고 진행
        }

        LoadSelected();
        SaveLastSelection();
    }

    bool IsDirty(PromptItem p) =>
        TxtTitle.Text.Trim()   != p.Title   ||
        TxtContent.Text        != p.Content  ||
        TxtTags.Text.Trim()    != p.Tags     ||
        TxtService.Text.Trim() != p.Service  ||
        TxtNotes.Text.Trim()   != p.Notes;

    void LoadSelected()
    {
        var p = _vm.Selected;
        if (p == null)
        {
            TxtTitle.Text = "";
            TxtContent.Text = "";
            TxtTags.Text = "";
            TxtService.Text = "";
            TxtNotes.Text = "";
            BtnFav.Content = "☆";
            UpdatePinButton();
            return;
        }
        TxtTitle.Text   = p.Title;
        TxtContent.Text = p.Content;
        TxtTags.Text    = p.Tags;
        TxtService.Text = p.Service;
        TxtNotes.Text   = p.Notes;
        BtnFav.Content  = p.IsFavorite ? "★" : "☆";
        UpdatePinButton();
    }

    void UpdatePinButton()
    {
        var pinned = _vm.Selected?.IsPinned == true;
        BtnPin.Content = pinned ? "📌" : "📍";
        BtnPin.ToolTip = pinned ? "상단 고정 해제 (Ctrl+P)" : "상단 고정 (Ctrl+P)";
    }

    // ── 에디터 이벤트 ─────────────────────────────────────────────────────────

    void TagLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not TextBlock tb || string.IsNullOrWhiteSpace(tb.Text)) return;
        var allTags = tb.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .Where(t => _vm.Tags.Contains(t))
                             .ToList();
        if (allTags.Count == 0) return;
        e.Handled = true;

        if (allTags.Count == 1)
        {
            var idx = _vm.Tags.IndexOf(allTags[0]);
            if (idx >= 0) CbTag.SelectedIndex = idx;
            return;
        }

        // 여러 태그 — ContextMenu로 선택
        var menu = new ContextMenu { Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F1E2E")) };
        menu.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E3555"));

        foreach (var tag in allTags)
        {
            var mi = new MenuItem
            {
                Header = tag,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B0C8E0"))
            };
            mi.Click += (_, _) =>
            {
                var i = _vm.Tags.IndexOf(tag);
                if (i >= 0) CbTag.SelectedIndex = i;
            };
            menu.Items.Add(mi);
        }
        tb.ContextMenu = menu;
        menu.PlacementTarget = tb;
        menu.IsOpen = true;
    }

    void TxtContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _vm.Selected == null) return;
        var charCount = TxtContent.Text.Length;
        var estTokens = (int)Math.Ceiling(charCount / 4.0);
        _vm.StatusText = $"{charCount:N0}자 · ~{estTokens:N0} tokens";  // 즉시 글자수+토큰 업데이트

        // Find bar 열린 상태에서 내용이 변경되면 검색 위치 재계산 (stale 인덱스 방어)
        if (FindBar.Visibility == Visibility.Visible && !string.IsNullOrEmpty(TxtFind.Text))
            UpdateFindPositions();

        // 변수 수는 200ms 디바운싱으로 Regex 실행 부하 감소
        if (_contentDebounce == null)
        {
            _contentDebounce = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(200) };
            _contentDebounce.Tick += (_, _) =>
            {
                _contentDebounce.Stop();
                if (_vm.Selected == null) return;
                var vars  = new PromptItem { Content = TxtContent.Text }.ExtractVariables();
                var count = TxtContent.Text.Length;
                var tokens = (int)Math.Ceiling(count / 4.0);
                var base_ = $"{count:N0}자 · ~{tokens:N0} tokens";
                _vm.StatusText = vars.Count == 0 ? base_ : $"{base_} · 변수 {vars.Count}개";
            };
        }
        _contentDebounce.Stop();
        _contentDebounce.Start();
    }

    // ── 버튼 핸들러 ───────────────────────────────────────────────────────────

    void NewPrompt_Click(object sender, RoutedEventArgs e)
    {
        _vm.CreateNew();
        LoadSelected();
        TxtTitle.Focus();
        TxtTitle.SelectAll();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        ApplyEditToModel(_vm.Selected);

        // 내용 중복 감지: 동일 content를 가진 다른 프롬프트 존재 시 경고
        var currentContent = _vm.Selected.Content.Trim();
        if (!string.IsNullOrEmpty(currentContent))
        {
            var duplicate = _db.FindDuplicateContent(currentContent, _vm.Selected.Id);
            if (duplicate != null)
            {
                var r = MessageBox.Show(
                    $"내용이 동일한 프롬프트가 이미 있습니다:\n  \"{duplicate.Title}\"\n\n그래도 저장하시겠습니까?",
                    "중복 내용 감지", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }
        }

        _vm.Save(_vm.Selected);
        RefreshFilterCombos();
    }

    void SaveVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        ApplyEditToModel(_vm.Selected);
        _vm.SaveAsNewVersion(_vm.Selected);
        RefreshFilterCombos();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        var r = MessageBox.Show($"'{_vm.Selected.Title}'을(를) 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        _vm.Delete(_vm.Selected);
        LoadSelected();
        RefreshFilterCombos();
    }

    void Fav_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        _vm.ToggleFavorite(_vm.Selected);
        BtnFav.Content = _vm.Selected?.IsFavorite == true ? "★" : "☆";
    }

    void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        _vm.TogglePin(_vm.Selected);
        UpdatePinButton();
        RefreshFilterCombos();
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtContent.Text)) return;
        try
        {
            Clipboard.SetText(TxtContent.Text);
            if (_vm.Selected != null) _vm.IncrementUseCount(_vm.Selected.Id);
            bool dirty = _vm.Selected != null && IsDirty(_vm.Selected);
            _vm.StatusText = dirty ? "클립보드에 복사됨 ⚠ 미저장 내용 포함" : "클립보드에 복사됨";

            // 클립보드 히스토리 갱신 (최근 5개)
            var title   = _vm.Selected?.Title ?? "(무제)";
            var content = TxtContent.Text;
            _clipboardHistory.RemoveAll(h => h.Title == title && h.Content == content);
            _clipboardHistory.Insert(0, (title, content));
            if (_clipboardHistory.Count > 5) _clipboardHistory.RemoveAt(5);
            RefreshClipboardHistoryMenu();

            // 복사 버튼 일시 피드백
            if (sender is Button btn)
            {
                var original = btn.Content;
                btn.Content = "✓ 복사됨";
                btn.IsEnabled = false;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    btn.Content = original;
                    btn.IsEnabled = _vm.HasSelection;
                };
                timer.Start();
            }
        }
        catch { _vm.StatusText = "클립보드 복사 실패"; }
    }

    void RefreshClipboardHistoryMenu()
    {
        ClipHistoryPanel.Children.Clear();
        if (_clipboardHistory.Count == 0)
        {
            ClipHistoryPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "최근 복사 내역 없음",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 4)
            });
            return;
        }
        foreach (var (title, histContent) in _clipboardHistory)
        {
            var btn = new Button
            {
                Content = title.Length > 30 ? title[..30] + "…" : title,
                ToolTip = histContent.Length > 100 ? histContent[..100] + "…" : histContent,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 2),
                Tag = histContent
            };
            btn.Click += (_, _) =>
            {
                try
                {
                    Clipboard.SetText((string)btn.Tag);
                    _vm.StatusText = $"히스토리 복사: {title}";
                    ClipHistoryPopup.IsOpen = false;
                }
                catch { }
            };
            ClipHistoryPanel.Children.Add(btn);
        }
    }

    void FillVars_Click(object sender, RoutedEventArgs e)
    {
        var content = TxtContent.Text;
        var temp    = new PromptItem { Content = content };
        var vars    = temp.ExtractVariables();

        if (vars.Count == 0)
        {
            try { Clipboard.SetText(content); } catch { }
            _vm.StatusText = "변수 없음 — 그대로 복사됨";
            return;
        }

        var promptId = _vm.Selected?.Id ?? 0;
        _varHistory.TryGetValue(promptId, out var prevValues);

        var dlg = new FillVarsDialog(content, vars, prevValues) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.FilledContent != null)
        {
            if (_vm.Selected != null) _vm.IncrementUseCount(_vm.Selected.Id);
            _varHistory[promptId] = dlg.LastInputValues;

            // 변수 채운 결과도 클립보드 히스토리에 추가
            var title = _vm.Selected?.Title ?? "(무제)";
            _clipboardHistory.RemoveAll(h => h.Title == title);
            _clipboardHistory.Insert(0, (title, dlg.FilledContent));
            if (_clipboardHistory.Count > 5) _clipboardHistory.RemoveAt(5);

            _vm.StatusText = "변수 채우기 완료 — 클립보드에 복사됨";
        }
    }

    void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        _vm.Duplicate(_vm.Selected);
        LoadSelected();
    }

    void ShowHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null) return;
        var history = _db.GetVersionHistory(_vm.Selected.Id);
        var dlg = new VersionHistoryDialog(_vm.Selected, history, _db) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.RestoredContent != null)
        {
            TxtContent.Text = dlg.RestoredContent;
            _vm.StatusText = "이전 버전 복원됨 — 저장하여 확정하세요";
        }
    }

    void Sort_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _refreshing) return;
        _vm.SortOrder = CbSort.SelectedIndex switch
        {
            1 => "use_count",
            2 => "custom",
            _ => "updated"
        };
        _appSettings.LastSortOrder = _vm.SortOrder;
        _appSettings.Save();
    }

    // ── 드래그 앤 드롭 순서 변경 ──────────────────────────────────────────────

    Point        _dragStart;
    PromptItem?  _dragItem;

    void LstItems_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem  = (e.OriginalSource as FrameworkElement)?.DataContext as PromptItem;
    }

    void LstItems_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;

        // 필터·검색 활성 중 드래그 차단 — 일부 항목만 보일 때 sort_order 재배정하면 충돌 발생
        if (!string.IsNullOrWhiteSpace(_vm.Search) || _vm.FilterTag != null ||
            _vm.FilterService != null || _vm.FavOnly)
        {
            _dragItem = null;
            return;
        }

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(LstItems, _dragItem, DragDropEffects.Move);
    }

    void LstItems_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    void LstItems_Drop(object sender, DragEventArgs e)
    {
        var target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (target?.DataContext is not PromptItem targetItem || _dragItem == null || targetItem == _dragItem)
        {
            _dragItem = null;
            return;
        }

        int fromIdx = _vm.Items.IndexOf(_dragItem);
        int toIdx   = _vm.Items.IndexOf(targetItem);
        if (fromIdx < 0 || toIdx < 0) { _dragItem = null; return; }

        _vm.MoveItem(fromIdx, toIdx);

        if (CbSort.SelectedIndex != 2)
        {
            _refreshing = true;
            try { CbSort.SelectedIndex = 2; }
            finally { _refreshing = false; }
        }
        _dragItem = null;
    }

    static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    // ── 더블클릭 즉시 복사 ────────────────────────────────────────────────────

    void LstItems_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_vm.Selected == null || string.IsNullOrEmpty(TxtContent.Text)) return;
        // 드래그 시작 직후 더블클릭 오발동 방지: ListBoxItem 위에서 발생한 경우만 처리
        if (FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) == null) return;
        Copy_Click(sender, e);
    }

    void Help_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    void ClipHistory_Click(object sender, RoutedEventArgs e)
    {
        RefreshClipboardHistoryMenu();
        ClipHistoryPopup.IsOpen = !ClipHistoryPopup.IsOpen;
    }

    // ── 동기화 설정 ───────────────────────────────────────────────────────────

    void SyncSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.SettingsDialog(_appSettings) { Owner = this };
        dlg.ShowDialog();
        // PAT/GistId는 SettingsDialog 내부에서 저장됨
    }

    // ── GitHub Gist 동기화 ────────────────────────────────────────────────────

    async void GistUpload_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_appSettings.GithubPat))
        {
            MessageBox.Show("GitHub PAT가 설정되지 않았습니다.\n⚙ 설정에서 PAT를 입력하세요.",
                "설정 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (sender is Button btn) btn.IsEnabled = false;
        _vm.StatusText = "Gist에 업로드 중...";
        try
        {
            var all  = _db.GetAll();
            var data = all.Select(p => new
            {
                title = p.Title, content = p.Content, tags = p.Tags,
                service = p.Service, isFavorite = p.IsFavorite,
                version = p.Version, notes = p.Notes,
                useCount = p.UseCount, sortOrder = p.SortOrder,
                isPinned = p.IsPinned
            });
            var json = System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            using var gist = new Services.GistSync(_appSettings.GithubPat);
            var newId = await gist.UploadAsync(_appSettings.GistId, json);

            if (newId != _appSettings.GistId)
            {
                _appSettings.GistId = newId;
                _appSettings.Save();
            }
            _vm.StatusText = $"Gist 업로드 완료 — {all.Count}개 (ID: {newId[..8]}...)";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Gist 업로드 실패: {ex.Message}";
            MessageBox.Show($"업로드 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (sender is Button b) b.IsEnabled = true;
        }
    }

    async void GistDownload_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_appSettings.GithubPat))
        {
            MessageBox.Show("GitHub PAT가 설정되지 않았습니다.\n⚙ 설정에서 PAT를 입력하세요.",
                "설정 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrEmpty(_appSettings.GistId))
        {
            MessageBox.Show("Gist ID가 없습니다. 먼저 업로드를 실행하세요.",
                "설정 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (sender is Button btn) btn.IsEnabled = false;
        _vm.StatusText = "Gist에서 다운로드 중...";
        try
        {
            using var gist = new Services.GistSync(_appSettings.GithubPat);
            var json  = await gist.DownloadAsync(_appSettings.GistId);
            var opts  = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = System.Text.Json.JsonSerializer.Deserialize<List<ImportDto>>(json, opts);

            if (items == null || items.Count == 0)
            {
                _vm.StatusText = "다운로드된 데이터가 없습니다";
                return;
            }

            var existingAll = _db.GetAll();
            var existingMap = existingAll.ToDictionary(p => p.Title, p => p, StringComparer.OrdinalIgnoreCase);
            int imported = 0, updated = 0, skipped = 0;

            // 변경된 항목 먼저 파악 (내용·태그·서비스·메모 중 하나라도 다르면 변경으로 간주)
            var toUpdate = items
                .Where(item => existingMap.TryGetValue(item.Title ?? "", out var ex) &&
                               (ex.Content != (item.Content ?? "") ||
                                ex.Tags    != (item.Tags    ?? "") ||
                                ex.Service != (item.Service ?? "") ||
                                ex.Notes   != (item.Notes   ?? "")))
                .ToList();

            bool doMerge = false;
            if (toUpdate.Count > 0)
            {
                var mergeResult = MessageBox.Show(
                    $"기존 항목 중 {toUpdate.Count}개가 변경되었습니다.\n업데이트하시겠습니까?",
                    "머지 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                doMerge = mergeResult == MessageBoxResult.Yes;
            }

            // 신규 삽입 시 Gist의 SortOrder 순서대로 삽입 → DB auto-assign이 상대 순서를 보존
            foreach (var item in items.OrderBy(x => x.SortOrder))
            {
                if (existingMap.TryGetValue(item.Title ?? "", out var existing))
                {
                    if (doMerge && toUpdate.Any(u => string.Equals(u.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        existing.Content    = item.Content ?? "";
                        existing.Tags       = item.Tags    ?? "";
                        existing.Service    = item.Service ?? "";
                        existing.Notes      = item.Notes   ?? "";
                        existing.IsFavorite = item.IsFavorite;
                        existing.IsPinned   = item.IsPinned;
                        existing.Version    = item.Version > 0 ? item.Version : existing.Version;
                        _db.Update(existing);
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                    continue;
                }
                _db.Insert(new PromptItem
                {
                    Title      = item.Title ?? "",
                    Content    = item.Content ?? "",
                    Tags       = item.Tags ?? "",
                    Service    = item.Service ?? "",
                    IsFavorite = item.IsFavorite,
                    Version    = item.Version > 0 ? item.Version : 1,
                    Notes      = item.Notes ?? "",
                    UseCount   = item.UseCount,
                    IsPinned   = item.IsPinned
                });
                imported++;
            }
            _vm.Refresh();
            RefreshFilterCombos();
            var parts = new List<string>();
            if (imported > 0) parts.Add($"{imported}개 추가");
            if (updated  > 0) parts.Add($"{updated}개 업데이트");
            if (skipped  > 0) parts.Add($"{skipped}개 스킵");
            _vm.StatusText = $"Gist 다운로드 완료 — {string.Join(", ", parts)}";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Gist 다운로드 실패: {ex.Message}";
            MessageBox.Show($"다운로드 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (sender is Button b) b.IsEnabled = true;
        }
    }

    void Export_Click(object sender, RoutedEventArgs e)
    {
        // 필터된 항목이 있으면 전체 vs 현재 필터 선택
        var allItems = _db.GetAll();
        bool hasFilter = _vm.Items.Count < allItems.Count;
        List<PromptItem> source;
        if (hasFilter)
        {
            var choice = MessageBox.Show(
                $"현재 필터 결과 {_vm.Items.Count}개만 내보내시겠습니까?\n\n" +
                $"예 → 현재 필터 결과만\n아니요 → 전체 내보내기",
                "내보내기 범위", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;
            source = choice == MessageBoxResult.Yes
                ? [.. _vm.Items]
                : allItems;
        }
        else
        {
            source = allItems;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title          = "프롬프트 내보내기",
            Filter         = "JSON 파일 (*.json)|*.json|CSV 파일 (*.csv)|*.csv",
            DefaultExt     = ".json",
            FileName       = $"prompts_{DateTime.Now:yyyyMMdd}",
            InitialDirectory = string.IsNullOrEmpty(_appSettings.LastExportPath)
                ? "" : _appSettings.LastExportPath
        };
        if (dlg.ShowDialog() != true) return;

        bool useCsv = dlg.FilterIndex == 2;
        if (useCsv)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("제목,내용,태그,서비스,메모,즐겨찾기,사용횟수,버전");
            foreach (var p in source)
                sb.AppendLine(string.Join(",",
                    CsvEscape(p.Title), CsvEscape(p.Content), CsvEscape(p.Tags),
                    CsvEscape(p.Service), CsvEscape(p.Notes),
                    p.IsFavorite ? "1" : "0", p.UseCount, p.Version));
            File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        }
        else
        {
            var data = source.Select(p => new
            {
                title      = p.Title,
                content    = p.Content,
                tags       = p.Tags,
                service    = p.Service,
                isFavorite = p.IsFavorite,
                version    = p.Version,
                notes      = p.Notes,
                useCount   = p.UseCount,
                sortOrder  = p.SortOrder,
                isPinned   = p.IsPinned
            });
            var json = System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
        }
        _appSettings.LastExportPath = Path.GetDirectoryName(dlg.FileName) ?? "";
        _appSettings.Save();
        _vm.StatusText = $"내보내기 완료 — {source.Count}개 ({(useCsv ? "CSV" : "JSON")})";
    }

    void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title            = "프롬프트 가져오기",
            Filter           = "JSON 파일 (*.json)|*.json|CSV 파일 (*.csv)|*.csv",
            InitialDirectory = string.IsNullOrEmpty(_appSettings.LastImportPath)
                ? "" : _appSettings.LastImportPath
        };
        if (dlg.ShowDialog() != true) return;

        bool isCsv = dlg.FilterIndex == 2 ||
                     dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        try
        {
            List<ImportDto>? items;
            if (isCsv)
            {
                items = ImportFromCsv(dlg.FileName);
            }
            else
            {
                var json = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                var opts = new System.Text.Json.JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true };
                items = System.Text.Json.JsonSerializer.Deserialize<List<ImportDto>>(json, opts);
            }

            if (items == null || items.Count == 0)
            {
                _vm.StatusText = "가져올 데이터가 없습니다";
                return;
            }
            var existingTitles = new HashSet<string>(
                _db.GetAll().Select(p => p.Title), StringComparer.OrdinalIgnoreCase);
            int imported = 0, skipped = 0;
            foreach (var item in items)
            {
                if (existingTitles.Contains(item.Title ?? "")) { skipped++; continue; }
                _db.Insert(new PromptItem
                {
                    Title      = item.Title ?? "",
                    Content    = item.Content ?? "",
                    Tags       = item.Tags ?? "",
                    Service    = item.Service ?? "",
                    IsFavorite = item.IsFavorite,
                    Version    = item.Version > 0 ? item.Version : 1,
                    Notes      = item.Notes ?? "",
                    UseCount   = item.UseCount,
                    IsPinned   = item.IsPinned
                });
                imported++;
            }
            _vm.Refresh();
            RefreshFilterCombos();
            _appSettings.LastImportPath = Path.GetDirectoryName(dlg.FileName) ?? "";
            _appSettings.Save();
            var fmt = isCsv ? "CSV" : "JSON";
            _vm.StatusText = skipped > 0
                ? $"가져오기 완료 — {imported}개 추가, {skipped}개 중복 스킵 ({fmt})"
                : $"가져오기 완료 — {imported}개 ({fmt})";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"가져오기 실패: {ex.Message}";
            MessageBox.Show($"가져오기 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static List<ImportDto> ImportFromCsv(string filePath)
    {
        var text   = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var rows   = CsvParseAll(text);
        var result = new List<ImportDto>();
        if (rows.Count < 2) return result;

        // 헤더: 제목,내용,태그,서비스,메모,즐겨찾기,사용횟수,버전
        for (int i = 1; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Count < 2) continue;
            result.Add(new ImportDto(
                Title:      cols.ElementAtOrDefault(0) ?? "",
                Content:    cols.ElementAtOrDefault(1) ?? "",
                Tags:       cols.ElementAtOrDefault(2) ?? "",
                Service:    cols.ElementAtOrDefault(3) ?? "",
                Notes:      cols.ElementAtOrDefault(4) ?? "",
                IsFavorite: cols.ElementAtOrDefault(5) == "1",
                UseCount:   int.TryParse(cols.ElementAtOrDefault(6), out var uc)  ? uc  : 0,
                Version:    int.TryParse(cols.ElementAtOrDefault(7), out var ver) ? ver : 1
            ));
        }
        return result;
    }

    // RFC 4180 전체 문자열 CSV 파서 — 멀티라인 셀(개행 포함) 지원
    static List<List<string>> CsvParseAll(string text)
    {
        var rows = new List<List<string>>();
        var row  = new List<string>();
        var sb   = new System.Text.StringBuilder();
        int pos  = 0;

        while (pos < text.Length)
        {
            if (text[pos] == '"')
            {
                pos++;
                while (pos < text.Length)
                {
                    if (text[pos] == '"' && pos + 1 < text.Length && text[pos + 1] == '"')
                    { sb.Append('"'); pos += 2; }
                    else if (text[pos] == '"') { pos++; break; }
                    else { sb.Append(text[pos++]); }
                }
                row.Add(sb.ToString());
                sb.Clear();
                if (pos < text.Length && text[pos] == ',') pos++;
                else if (pos < text.Length)
                {
                    // 행 끝 (CRLF or LF)
                    if (text[pos] == '\r') pos++;
                    if (pos < text.Length && text[pos] == '\n') pos++;
                    rows.Add(row);
                    row = [];
                }
            }
            else if (text[pos] == ',')
            {
                row.Add(sb.ToString());
                sb.Clear();
                pos++;
            }
            else if (text[pos] == '\r' || text[pos] == '\n')
            {
                row.Add(sb.ToString());
                sb.Clear();
                if (text[pos] == '\r') pos++;
                if (pos < text.Length && text[pos] == '\n') pos++;
                if (row.Count > 0) { rows.Add(row); row = []; }
            }
            else
            {
                sb.Append(text[pos++]);
            }
        }
        if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
        return rows;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    void ApplyEditToModel(PromptItem p)
    {
        p.Title   = TxtTitle.Text.Trim();
        p.Content = TxtContent.Text;
        p.Tags    = TxtTags.Text.Trim();
        p.Service = TxtService.Text.Trim();
        p.Notes   = TxtNotes.Text.Trim();
    }

    // ── 본문 텍스트 찾기 (Ctrl+G) ─────────────────────────────────────────────

    int _findIndex = -1;
    List<int> _findPositions = [];

    void OpenFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        TxtFind.Focus();
        TxtFind.SelectAll();
    }

    void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFindPositions();
    }

    void UpdateFindPositions()
    {
        _findPositions.Clear();
        _findIndex = -1;
        var query = TxtFind.Text;
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(TxtContent.Text))
        {
            TxtFindCount.Text = "";
            return;
        }
        int pos = 0;
        while (pos < TxtContent.Text.Length)
        {
            var idx = TxtContent.Text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            _findPositions.Add(idx);
            pos = idx + query.Length;
        }
        TxtFindCount.Text = _findPositions.Count == 0
            ? "0개"
            : $"0/{_findPositions.Count}";
        if (_findPositions.Count > 0) FindNext();
    }

    void FindNext()
    {
        if (_findPositions.Count == 0) return;
        _findIndex = (_findIndex + 1) % _findPositions.Count;
        SelectFindMatch();
    }

    void FindPrev()
    {
        if (_findPositions.Count == 0) return;
        _findIndex = (_findIndex - 1 + _findPositions.Count) % _findPositions.Count;
        SelectFindMatch();
    }

    void SelectFindMatch()
    {
        var pos = _findPositions[_findIndex];
        TxtContent.Focus();
        TxtContent.Select(pos, TxtFind.Text.Length);
        var rect = TxtContent.GetRectFromCharacterIndex(pos);
        if (!rect.IsEmpty) TxtContent.ScrollToLine(TxtContent.GetLineIndexFromCharacterIndex(pos));
        TxtFindCount.Text = $"{_findIndex + 1}/{_findPositions.Count}";
        TxtFind.Focus();
    }

    void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    void FindPrev_Click(object sender, RoutedEventArgs e) => FindPrev();
    void FindClose_Click(object sender, RoutedEventArgs e) => FindBar.Visibility = Visibility.Collapsed;

    void TxtFind_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift) FindPrev();
            else FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FindBar.Visibility = Visibility.Collapsed;
            TxtContent.Focus();
            e.Handled = true;
        }
    }

    // ── 검색 초기화 ───────────────────────────────────────────────────────────

    void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        bool prev = _refreshing;
        _refreshing = true;
        try
        {
            CbTag.SelectedIndex = 0;
            CbService.SelectedIndex = 0;
            ChkFav.IsChecked = false;
        }
        finally { _refreshing = prev; }
        _vm.FilterTag = null;
        _vm.FilterService = null;
        _vm.FavOnly = false;
        _vm.Search = "";
        TxtSearch.Focus();
    }

    // ── 태그 자동완성 ─────────────────────────────────────────────────────────

    void TxtTags_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var text = TxtTags.Text;
        var lastComma = text.LastIndexOf(',');
        var partial = (lastComma >= 0 ? text[(lastComma + 1)..] : text).Trim();

        if (partial.Length == 0) { TagPopup.IsOpen = false; return; }

        var matches = _vm.Tags.Skip(1)  // "모든 태그" 제외
            .Where(t => t.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
                     && !t.Equals(partial, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) { TagPopup.IsOpen = false; return; }

        LstTagSuggestions.ItemsSource = matches;
        TagPopup.IsOpen = true;
    }

    void TagSuggestion_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (LstTagSuggestions.SelectedItem is not string tag) return;
        TagPopup.IsOpen = false;
        var text = TxtTags.Text;
        var lastComma = text.LastIndexOf(',');
        TxtTags.Text = lastComma >= 0 ? text[..(lastComma + 1)] + " " + tag : tag;
        TxtTags.CaretIndex = TxtTags.Text.Length;
        TxtTags.Focus();
        LstTagSuggestions.SelectedItem = null;
    }

    // ── 서비스 자동완성 ───────────────────────────────────────────────────────

    void TxtService_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var partial = TxtService.Text.Trim();
        if (partial.Length == 0) { ServicePopup.IsOpen = false; return; }

        var matches = _vm.Services.Skip(1)  // "모든 서비스" 제외
            .Where(s => s.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
                     && !s.Equals(partial, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) { ServicePopup.IsOpen = false; return; }

        LstServiceSuggestions.ItemsSource = matches;
        ServicePopup.IsOpen = true;
    }

    void ServiceSuggestion_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (LstServiceSuggestions.SelectedItem is not string svc) return;
        ServicePopup.IsOpen = false;
        TxtService.Text = svc;
        TxtService.CaretIndex = TxtService.Text.Length;
        TxtService.Focus();
        LstServiceSuggestions.SelectedItem = null;
    }

    // ── 최근 검색어 ──────────────────────────────────────────────────────────

    void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_recentSearches.Count == 0) return;
        LstRecentSearches.ItemsSource = _recentSearches.ToList();
        RecentSearchPopup.IsOpen = true;
    }

    void RecentSearch_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (LstRecentSearches.SelectedItem is not string query) return;
        RecentSearchPopup.IsOpen = false;
        _vm.Search = query;
        TxtSearch.CaretIndex = TxtSearch.Text.Length;
        LstRecentSearches.SelectedItem = null;
    }

    // ── 우클릭 내용 복사 ──────────────────────────────────────────────────────

    void CopyFromMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Selected == null || string.IsNullOrEmpty(_vm.Selected.Content)) return;
        try
        {
            Clipboard.SetText(_vm.Selected.Content);
            _vm.IncrementUseCount(_vm.Selected.Id);
            _vm.StatusText = "클립보드에 복사됨";
        }
        catch { _vm.StatusText = "클립보드 복사 실패"; }
    }

    void MergeCopy_Click(object sender, RoutedEventArgs e)
    {
        var selected = LstItems.SelectedItems.Cast<PromptItem>().ToList();
        if (selected.Count == 0) return;
        if (selected.Count == 1)
        {
            CopyFromMenu_Click(sender, e);
            return;
        }
        var merged = string.Join("\n\n---\n\n", selected.Select(p => $"[{p.Title}]\n{p.Content}"));
        try
        {
            Clipboard.SetText(merged);
            foreach (var p in selected) _vm.IncrementUseCount(p.Id);

            var mergedTitle = string.Join(" + ", selected.Select(p =>
                p.Title.Length > 12 ? p.Title[..12] + "…" : p.Title));
            _clipboardHistory.RemoveAll(h => h.Title == mergedTitle);
            _clipboardHistory.Insert(0, (mergedTitle, merged));
            if (_clipboardHistory.Count > 5) _clipboardHistory.RemoveAt(5);

            _vm.StatusText = $"{selected.Count}개 항목 합쳐서 복사됨";
        }
        catch { _vm.StatusText = "클립보드 복사 실패"; }
    }

    // ── 복수 선택 일괄 태그 편집 ─────────────────────────────────────────────

    void BulkTagEdit_Click(object sender, RoutedEventArgs e)
    {
        var selected = LstItems.SelectedItems.Cast<PromptItem>().ToList();
        if (selected.Count < 2)
        {
            MessageBox.Show("2개 이상 항목을 선택(Ctrl+클릭)한 뒤 사용하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Views.BulkTagEditDialog(selected.Count) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        foreach (var item in selected)
        {
            var tags = item.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            foreach (var t in dlg.TagsToAdd)
                if (!tags.Contains(t, StringComparer.OrdinalIgnoreCase)) tags.Add(t);
            foreach (var t in dlg.TagsToRemove)
                tags.RemoveAll(x => x.Equals(t, StringComparison.OrdinalIgnoreCase));
            item.Tags = string.Join(", ", tags);
            item.UpdatedAt = DateTime.UtcNow;
            _db.Update(item);
        }
        _vm.Refresh();
        RefreshFilterCombos();
        _vm.StatusText = $"{selected.Count}개 항목 태그 업데이트 완료";
    }

    void Stats_Click(object sender, RoutedEventArgs e)
    {
        var all = _db.GetAll();
        if (all.Count == 0)
        {
            MessageBox.Show("프롬프트가 없습니다.", "통계", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalUse = all.Sum(p => p.UseCount);
        var topUsed = all.Where(p => p.UseCount > 0)
                         .OrderByDescending(p => p.UseCount)
                         .Take(5)
                         .ToList();
        var favCount = all.Count(p => p.IsFavorite);
        var tagCounts = all.SelectMany(p => p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                           .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                           .OrderByDescending(g => g.Count())
                           .Take(5)
                           .ToList();
        var recentlyUpdated = all.OrderByDescending(p => p.UpdatedAt).Take(3).ToList();
        var neverUsed = all.Count(p => p.UseCount == 0);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"총 프롬프트: {all.Count}개  |  즐겨찾기: {favCount}개");
        sb.AppendLine($"총 사용 횟수: {totalUse}회  |  미사용: {neverUsed}개");
        sb.AppendLine();

        if (topUsed.Count > 0)
        {
            sb.AppendLine("🏆 가장 많이 사용한 프롬프트:");
            foreach (var p in topUsed)
                sb.AppendLine($"  {p.UseCount,4}회  {p.Title}");
            sb.AppendLine();
        }

        if (tagCounts.Count > 0)
        {
            sb.AppendLine("🏷️ 인기 태그:");
            foreach (var g in tagCounts)
                sb.AppendLine($"  {g.Count(),4}개  {g.Key}");
            sb.AppendLine();
        }

        sb.AppendLine("🕐 최근 수정:");
        foreach (var p in recentlyUpdated)
            sb.AppendLine($"  {p.UpdatedAt:yyyy-MM-dd HH:mm}  {p.Title}");

        ShowStatsWindow(sb.ToString());
    }

    string? ShowInputDialog(string prompt, string title)
    {
        string? result = null;
        var win = new Window
        {
            Title = title, Owner = this,
            Width = 360, Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E1A28")),
            ResizeMode = ResizeMode.NoResize
        };
        win.Loaded += (_, _) => App.ApplyDarkTitleBar(win);

        var tb = new TextBox
        {
            Margin = new Thickness(20, 16, 20, 8),
            VerticalAlignment = VerticalAlignment.Top
        };
        var okBtn = new Button
        {
            Content = "확인", HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 20, 14),
            Padding = new Thickness(16, 6, 16, 6),
            IsDefault = true
        };
        okBtn.Click += (_, _) => { result = tb.Text; win.Close(); };

        var lbl = new TextBlock
        {
            Text = prompt, Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8AAAC8")),
            Margin = new Thickness(20, 16, 20, 4),
            VerticalAlignment = VerticalAlignment.Top, FontSize = 12
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(lbl, 0);
        Grid.SetRow(tb, 1);
        Grid.SetRow(okBtn, 2);
        grid.Children.Add(lbl);
        grid.Children.Add(tb);
        grid.Children.Add(okBtn);
        win.Content = grid;
        win.Loaded += (_, _) => tb.Focus();
        win.ShowDialog();
        return result;
    }

    void ShowStatsWindow(string content)
    {
        var win = new Window
        {
            Title  = "프롬프트 사용 통계",
            Owner  = this,
            Width  = 420,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E1A28")),
            ResizeMode = ResizeMode.NoResize
        };
        win.Loaded += (_, _) => App.ApplyDarkTitleBar(win);

        var tb = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B0C8E0")),
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(20, 16, 20, 60),
            Padding = new Thickness(0)
        };
        var copyBtn = new Button
        {
            Content = "클립보드에 복사",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 20, 16),
            Padding = new Thickness(16, 8, 16, 8),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        copyBtn.Click += (_, _) =>
        {
            try { Clipboard.SetText(content); copyBtn.Content = "✓ 복사됨"; }
            catch { }
        };
        var grid = new Grid();
        grid.Children.Add(tb);
        grid.Children.Add(copyBtn);
        win.Content = grid;
        win.ShowDialog();
    }

    static string CsvEscape(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

    private record ImportDto(
        string? Title, string? Content, string? Tags,
        string? Service, bool IsFavorite, int Version, string? Notes,
        int UseCount = 0, int SortOrder = 0, bool IsPinned = false);
}
