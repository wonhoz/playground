using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Prompt.Forge.Views;

namespace Prompt.Forge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int  HotkeyId = 0x50;   // Win+Shift+P
    const uint MOD_WIN  = 0x0008;
    const uint MOD_SHIFT = 0x0004;
    const uint VK_P     = 0x50;

    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Prompt.Forge", "settings.txt");

    readonly MainViewModel _vm;
    readonly Database      _db;
    bool _refreshing;

    public MainWindow()
    {
        _db = new Database(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prompt.Forge", "prompts.db"));
        _vm = new MainViewModel(_db);
        DataContext = _vm;
        InitializeComponent();

        Loaded   += OnLoaded;
        Closing  += OnClosing;
        TxtContent.TextChanged += TxtContent_TextChanged;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));

        // 전역 단축키 등록 (Win+Shift+P)
        RegisterHotKey(handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_P);

        HwndSource.FromHwnd(handle)?.AddHook(WndProc);

        // 필터 콤보 초기화
        RefreshFilterCombos();

        // 마지막 선택 항목 복원 (B4)
        RestoreLastSelection();

        // 검색 X버튼 표시/숨김
        TxtSearch.TextChanged += (_, _) =>
            BtnClearSearch.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Collapsed : Visibility.Visible;

        // 태그 자동완성
        TxtTags.TextChanged += TxtTags_TextChanged;
    }

    // ── 마지막 선택 항목 저장/복원 ─────────────────────────────────────────────

    void SaveLastSelection()
    {
        if (_vm.Selected == null) return;
        try { File.WriteAllText(SettingsPath, _vm.Selected.Id.ToString()); }
        catch { }
    }

    void RestoreLastSelection()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            if (!int.TryParse(File.ReadAllText(SettingsPath).Trim(), out var lastId)) return;
            var item = _vm.Items.FirstOrDefault(x => x.Id == lastId);
            if (item != null)
            {
                _vm.Selected = item;
                LoadSelected();
            }
        }
        catch { }
    }

    // ── 키보드 단축키 (B1) ────────────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsLoaded) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.N)
            {
                NewPrompt_Click(this, new RoutedEventArgs());
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
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyId);
        _db.Dispose();
    }

    // ── 필터 갱신 ─────────────────────────────────────────────────────────────

    void RefreshFilterCombos()
    {
        if (!IsLoaded) return;
        bool prev = _refreshing;
        _refreshing = true;
        try
        {
            var selTag = CbTag.SelectedIndex > 0 ? CbTag.SelectedItem as string : null;
            var selSvc = CbService.SelectedIndex > 0 ? CbService.SelectedItem as string : null;

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
            return;
        }
        TxtTitle.Text   = p.Title;
        TxtContent.Text = p.Content;
        TxtTags.Text    = p.Tags;
        TxtService.Text = p.Service;
        TxtNotes.Text   = p.Notes;
        BtnFav.Content  = p.IsFavorite ? "★" : "☆";
    }

    // ── 에디터 이벤트 ─────────────────────────────────────────────────────────

    void TagLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not TextBlock tb || string.IsNullOrWhiteSpace(tb.Text)) return;
        // Ctrl+클릭일 때만 태그 필터 적용 (일반 클릭은 항목 선택만)
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        var firstTag = tb.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .FirstOrDefault();
        if (firstTag == null) return;
        e.Handled = true;
        var idx = _vm.Tags.IndexOf(firstTag);
        if (idx >= 0) CbTag.SelectedIndex = idx;
    }

    void TxtContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _vm.Selected == null) return;
        var content = TxtContent.Text;
        var temp = new PromptItem { Content = content };
        var vars = temp.ExtractVariables();
        var charCount = content.Length;
        _vm.StatusText = vars.Count == 0
            ? $"{charCount:N0}자"
            : $"{charCount:N0}자 · 변수 {vars.Count}개";
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

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtContent.Text)) return;
        try
        {
            Clipboard.SetText(TxtContent.Text);
            if (_vm.Selected != null) _vm.IncrementUseCount(_vm.Selected.Id);
            bool dirty = _vm.Selected != null && IsDirty(_vm.Selected);
            _vm.StatusText = dirty ? "클립보드에 복사됨 ⚠ 미저장 내용 포함" : "클립보드에 복사됨";

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

        var dlg = new FillVarsDialog(content, vars) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.FilledContent != null)
        {
            if (_vm.Selected != null) _vm.IncrementUseCount(_vm.Selected.Id);
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
        _vm.SortOrder = CbSort.SelectedIndex == 1 ? "use_count" : "updated";
    }

    void Help_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "프롬프트 내보내기",
            Filter     = "JSON 파일 (*.json)|*.json",
            DefaultExt = ".json",
            FileName   = $"prompts_{DateTime.Now:yyyyMMdd}"
        };
        if (dlg.ShowDialog() != true) return;

        var all  = _db.GetAll();
        var data = all.Select(p => new
        {
            title      = p.Title,
            content    = p.Content,
            tags       = p.Tags,
            service    = p.Service,
            isFavorite = p.IsFavorite,
            version    = p.Version,
            notes      = p.Notes
        });
        var json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
        _vm.StatusText = $"내보내기 완료 — {all.Count}개";
    }

    void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "프롬프트 가져오기",
            Filter = "JSON 파일 (*.json)|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            var opts = new System.Text.Json.JsonSerializerOptions
                { PropertyNameCaseInsensitive = true };
            var items = System.Text.Json.JsonSerializer
                .Deserialize<List<ImportDto>>(json, opts);

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
                    Notes      = item.Notes ?? ""
                });
                imported++;
            }
            _vm.Refresh();
            _vm.StatusText = skipped > 0
                ? $"가져오기 완료 — {imported}개 추가, {skipped}개 중복 스킵"
                : $"가져오기 완료 — {imported}개";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"가져오기 실패: {ex.Message}";
        }
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

    private record ImportDto(
        string? Title, string? Content, string? Tags,
        string? Service, bool IsFavorite, int Version, string? Notes);
}
