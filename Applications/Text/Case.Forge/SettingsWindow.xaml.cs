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

        AutoLoadClipboardCb.IsChecked = _settings.AutoLoadClipboard;

        BuildPinnedPanel();
        RefreshHistory();
    }

    // 케이스 목록 StackPanel 구성 — 핀된 항목 우선, 순서 변경 ↑↓ 지원
    private void BuildPinnedPanel()
    {
        PinnedPanel.Children.Clear();

        // 핀된 순서 → 나머지 순서
        var allKeys = CaseConverter.Definitions.Select(d => d.Key).ToList();
        var orderedKeys = _settings.PinnedCases
            .Concat(allKeys.Except(_settings.PinnedCases))
            .ToList();

        foreach (var key in orderedKeys)
        {
            var def = CaseConverter.Definitions.FirstOrDefault(d => d.Key == key);
            if (def == default) continue;
            PinnedPanel.Children.Add(BuildPinnedRow(def.Label, key, _settings.PinnedCases.Contains(key)));
        }
    }

    private UIElement BuildPinnedRow(string label, string key, bool isPinned)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new System.Windows.Controls.CheckBox
        {
            Content    = label,
            Tag        = key,
            IsChecked  = isPinned,
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            FontFamily = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var upBtn   = MakePinnedArrowBtn("↑");
        var downBtn = MakePinnedArrowBtn("↓");
        upBtn.Click   += (_, _) => MovePinnedRow(row, -1);
        downBtn.Click += (_, _) => MovePinnedRow(row, +1);

        Grid.SetColumn(cb,      0);
        Grid.SetColumn(upBtn,   1);
        Grid.SetColumn(downBtn, 2);
        row.Children.Add(cb);
        row.Children.Add(upBtn);
        row.Children.Add(downBtn);
        return row;
    }

    private System.Windows.Controls.Button MakePinnedArrowBtn(string content)
    {
        var btn = new System.Windows.Controls.Button
        {
            Content           = content,
            Width             = 22,
            Height            = 22,
            FontSize          = 11,
            Foreground        = (SolidColorBrush)FindResource("TextSecondary"),
            Background        = System.Windows.Media.Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Cursor            = System.Windows.Input.Cursors.Hand,
            Margin            = new Thickness(2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        // 호버 효과를 위한 간단한 ControlTemplate (TemplateBinding 불필요)
        var tpl = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var bd  = new FrameworkElementFactory(typeof(Border));
        bd.Name = "bd";
        bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
        bd.AppendChild(cp);
        tpl.VisualTree = bd;
        var hover = new Trigger { Property = System.Windows.Controls.Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x40))) { TargetName = "bd" });
        tpl.Triggers.Add(hover);
        btn.Template = tpl;
        return btn;
    }

    private void MovePinnedRow(Grid row, int direction)
    {
        int idx    = PinnedPanel.Children.IndexOf(row);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= PinnedPanel.Children.Count) return;
        PinnedPanel.Children.Remove(row);
        PinnedPanel.Children.Insert(newIdx, row);
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
        foreach (var h in _settings.RecentHistory.ToList())
        {
            string item = h;
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBlock
            {
                Text         = $"  {item}",
                Foreground   = (SolidColorBrush)FindResource("TextPrimary"),
                FontFamily   = new WpfFontFamily("Consolas, Segoe UI"),
                FontSize     = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var delTb = new TextBlock
            {
                Text                = "✕",
                FontSize            = 11,
                Foreground          = (SolidColorBrush)FindResource("TextSecondary"),
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Cursor              = System.Windows.Input.Cursors.Hand,
                Width               = 20,
                TextAlignment       = TextAlignment.Center,
                ToolTip             = "이 항목 삭제",
            };
            delTb.MouseEnter += (_, _) => delTb.Foreground = (SolidColorBrush)FindResource("TextPrimary");
            delTb.MouseLeave += (_, _) => delTb.Foreground = (SolidColorBrush)FindResource("TextSecondary");
            delTb.MouseLeftButtonUp += (_, _) =>
            {
                _settings.RecentHistory.Remove(item);
                SettingsService.Save(_settings);
                RefreshHistory();
            };

            Grid.SetColumn(tb,    0);
            Grid.SetColumn(delTb, 1);
            row.Children.Add(tb);
            row.Children.Add(delTb);
            HistoryPanel.Children.Add(row);
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

        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LWin)      || Keyboard.IsKeyDown(Key.RWin))      mods |= 0x0008;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= 0x0004;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  mods |= 0x0002;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   mods |= 0x0001;

        // 수정자 키만 단독 입력 → 실시간 프리뷰만 업데이트
        if (k is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
               or Key.LeftAlt  or Key.RightAlt  or Key.LWin     or Key.RWin)
        {
            HotkeyDisplay.Text = mods > 0
                ? SettingsService.FormatHotkey(mods, 0).TrimEnd('+', ' ') + " + ..."
                : "키 조합을 누르세요...";
            return;
        }

        if (mods == 0) return; // 수정자 없으면 무시

        var vk = (uint)KeyInterop.VirtualKeyFromKey(k);
        if (vk == 0) return;

        _newMods = mods;
        _newVK   = vk;
        _capturingHotkey = false;
        HotkeyDisplay.Text = SettingsService.FormatHotkey(mods, vk);
        HotkeyBorder.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
    }

    private void CancelCapture()
    {
        _capturingHotkey = false;
        HotkeyDisplay.Text = SettingsService.FormatHotkey(_newMods, _newVK);
        HotkeyBorder.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
    }

    // ── 이력 삭제 ────────────────────────────────────────────────────────
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _settings.RecentHistory.Clear();
        SettingsService.Save(_settings);
        RefreshHistory();
    }

    // ── 저장/취소 ────────────────────────────────────────────────────────
    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.HotkeyModifiers   = _newMods;
        _settings.HotkeyVK          = _newVK;
        _settings.AutoLoadClipboard = AutoLoadClipboardCb.IsChecked == true;

        // StackPanel 순서 그대로 핀 목록 저장 (↑↓ 순서 반영)
        _settings.PinnedCases = PinnedPanel.Children
            .OfType<Grid>()
            .Select(row => row.Children.OfType<System.Windows.Controls.CheckBox>().FirstOrDefault())
            .Where(cb => cb?.IsChecked == true)
            .Select(cb => (string)cb!.Tag)
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
