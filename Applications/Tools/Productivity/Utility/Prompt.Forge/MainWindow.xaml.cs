using System.Runtime.InteropServices;
using System.Windows.Controls;
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

    readonly MainViewModel _vm;
    readonly Database      _db;

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

    void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.FilterTag     = CbTag.SelectedIndex <= 0     ? null : CbTag.SelectedItem as string;
        _vm.FilterService = CbService.SelectedIndex <= 0 ? null : CbService.SelectedItem as string;
        _vm.FavOnly       = ChkFav.IsChecked == true;
        RefreshFilterCombos();
    }

    // ── 목록 선택 ─────────────────────────────────────────────────────────────

    void LstItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        LoadSelected();
    }

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
            _vm.StatusText = "클립보드에 복사됨";
        }
        catch { }
    }

    void FillVars_Click(object sender, RoutedEventArgs e)
    {
        var content = TxtContent.Text;
        var vars    = Regex.Matches(content, @"\{\{(\w+)\}\}")
                          .Select(m => m.Groups[1].Value)
                          .Distinct()
                          .ToList();

        if (vars.Count == 0)
        {
            try { Clipboard.SetText(content); } catch { }
            _vm.StatusText = "변수 없음 — 그대로 복사됨";
            return;
        }

        var dlg = new FillVarsDialog(content, vars) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.FilledContent != null)
            _vm.StatusText = "변수 채우기 완료 — 클립보드에 복사됨";
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
}
