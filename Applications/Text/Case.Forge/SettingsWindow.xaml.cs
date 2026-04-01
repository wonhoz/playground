namespace CaseForge;

public partial class SettingsWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private AppSettings _settings = new();
    private bool  _capturingHotkey;
    private uint  _newMods;
    private uint  _newVK;

    public SettingsWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        int v = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref v, sizeof(int));

        _settings = SettingsService.Load();
        _newMods  = _settings.HotkeyModifiers;
        _newVK    = _settings.HotkeyVK;
        HotkeyDisplay.Text = SettingsService.FormatHotkey(_newMods, _newVK);

        // 케이스별 핀 체크박스 동적 생성
        foreach (var (label, key, _) in CaseConverter.Definitions)
        {
            PinnedPanel.Children.Add(new System.Windows.Controls.CheckBox
            {
                Content   = label,
                Tag       = key,
                IsChecked = _settings.PinnedCases.Contains(key),
                Margin    = new Thickness(0, 0, 14, 8),
                Foreground = (SolidColorBrush)FindResource("TextPrimary"),
                FontFamily = new WpfFontFamily("Consolas, Segoe UI"),
                FontSize   = 12,
            });
        }

        RefreshHistory();
    }

    private void RefreshHistory()
    {
        HistoryPanel.Children.Clear();
        if (_settings.RecentHistory.Count == 0)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text       = "이력 없음",
                Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                FontFamily = new WpfFontFamily("Segoe UI"),
                FontSize   = 12,
                Margin     = new Thickness(2, 0, 0, 4),
            });
            return;
        }
        foreach (var h in _settings.RecentHistory)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text         = $"  {h}",
                Foreground   = (SolidColorBrush)FindResource("TextPrimary"),
                FontFamily   = new WpfFontFamily("Consolas, Segoe UI"),
                FontSize     = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(0, 1, 0, 1),
            });
        }
    }

    // ── 단축키 캡처 ─────────────────────────────────────────────────────
    private void CaptureBtn_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyDisplay.Text = "키 조합을 누르세요...";
        HotkeyBorder.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
        Focus(); // Window에 포커스 이동 → Window_PreviewKeyDown 수신
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingHotkey) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;

        var k = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape → 캡처 취소
        if (k == Key.Escape)
        {
            CancelCapture();
            return;
        }

        // 수정자 키만 단독 입력 → 무시
        if (k is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
               or Key.LeftAlt  or Key.RightAlt  or Key.LWin     or Key.RWin)
            return;

        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LWin)      || Keyboard.IsKeyDown(Key.RWin))      mods |= 0x0008;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= 0x0004;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  mods |= 0x0002;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   mods |= 0x0001;

        if (mods == 0) return; // 수정자 없으면 무시

        var vk = (uint)KeyInterop.VirtualKeyFromKey(k);
        if (vk == 0) return;

        _newMods = mods;
        _newVK   = vk;
        _capturingHotkey = false;
        HotkeyDisplay.Text = SettingsService.FormatHotkey(mods, vk);
        HotkeyBorder.BorderBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2A, 0x4A, 0x32));
    }

    private void CancelCapture()
    {
        _capturingHotkey = false;
        HotkeyDisplay.Text = SettingsService.FormatHotkey(_newMods, _newVK);
        HotkeyBorder.BorderBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2A, 0x4A, 0x32));
    }

    // ── 이력 삭제 ────────────────────────────────────────────────────────
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _settings.RecentHistory.Clear();
        RefreshHistory();
    }

    // ── 저장/취소 ────────────────────────────────────────────────────────
    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.HotkeyModifiers = _newMods;
        _settings.HotkeyVK        = _newVK;
        _settings.PinnedCases = PinnedPanel.Children
            .OfType<System.Windows.Controls.CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (string)cb.Tag)
            .ToList();
        SettingsService.Save(_settings);
        ((App)System.Windows.Application.Current).ReapplyHotkey(_settings);
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();
    private void CloseBtn_Click(object sender,  RoutedEventArgs e)
    {
        if (_capturingHotkey) { CancelCapture(); return; }
        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !_capturingHotkey) Close();
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
}
