namespace CaseForge;

public partial class PopupWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private bool _initialized;
    private AppSettings _settings = new();
    private bool _pinnedOnly;

    // 키보드 네비게이션
    private readonly List<Border> _rows = [];
    private readonly Dictionary<Border, (string value, string label)> _rowData = [];
    private int _selectedIndex = -1;

    public PopupWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
        PositionNearCursor();
        _initialized = true;
        _settings = SettingsService.Load();
        UpdateHotkeyHint();
        if (_settings.AutoLoadClipboard)
            TryLoadClipboard();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void PositionNearCursor()
    {
        var pt = System.Windows.Forms.Cursor.Position;
        var wa = System.Windows.Forms.Screen.FromPoint(pt).WorkingArea;
        double wx = pt.X + 12, wy = pt.Y + 12;
        if (wx + Width  > wa.Right)  wx = wa.Right  - Width  - 12;
        if (wy + Height > wa.Bottom) wy = wa.Bottom - Height - 12;
        Left = wx; Top = wy;
    }

    private void UpdateHotkeyHint()
    {
        HotkeyHint.Text = SettingsService.FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyVK);
    }

    internal void ShowAndActivate()
    {
        _settings = SettingsService.Load();
        UpdateHotkeyHint();
        if (!IsVisible) Show();
        base.Activate();
        PositionNearCursor();
        // AutoLoadClipboard 설정 + 기존 입력 없을 때만 클립보드 로드 (재오픈 시 입력 유지)
        if (_settings.AutoLoadClipboard && string.IsNullOrWhiteSpace(InputBox.Text))
            TryLoadClipboard();
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void TryLoadClipboard()
    {
        try
        {
            var text = System.Windows.Clipboard.GetText().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                InputBox.Text = text;
                InputBox.SelectAll();
            }
        }
        catch { }
    }

    // ── 결과 렌더링 ──────────────────────────────────────────────────────
    private void RenderResults(string input)
    {
        ResultPanel.Children.Clear();
        _rows.Clear();
        _rowData.Clear();
        _selectedIndex = -1;

        bool hasInput = !string.IsNullOrWhiteSpace(input);

        if (!hasInput)
        {
            CopyAllBtn.Visibility = Visibility.Collapsed;
            RenderHistory();
            StatusText.Text = "텍스트를 입력하세요";
            return;
        }

        // 멀티라인: 줄별로 분리
        var lines = input.Split('\n')
                         .Select(l => l.Trim())
                         .Where(l => !string.IsNullOrWhiteSpace(l))
                         .ToArray();

        if (lines.Length > 1)
        {
            RenderMultiline(lines);
        }
        else
        {
            RenderSingleLine(input.Trim());
        }

        CopyAllBtn.Visibility = _rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderSingleLine(string input)
    {
        var results = CaseConverter.ConvertAll(input);
        var resultMap = results.ToDictionary(r => r.Key);

        // 즐겨찾기는 PinnedCases 저장 순서 기준으로 표시
        var pinnedSet = _settings.PinnedCases.Count > 0
            ? _settings.PinnedCases
                .Where(k => resultMap.ContainsKey(k))
                .Select(k => resultMap[k])
                .ToArray()
            : [];

        if (_pinnedOnly)
        {
            if (pinnedSet.Length == 0)
            {
                AddSectionLabel("즐겨찾기 없음 — ☆ 버튼으로 추가하세요");
                StatusText.Text = "";
                return;
            }
            foreach (var (label, key, value) in pinnedSet)
                AddResultRow(label, key, value);
            StatusText.Text = $"즐겨찾기 {pinnedSet.Length}개 — 클릭 또는 ↑↓ Enter";
            return;
        }

        if (pinnedSet.Length > 0)
        {
            AddSectionLabel("★  즐겨찾기");
            foreach (var (label, key, value) in pinnedSet)
                AddResultRow(label, key, value);
            AddSectionLabel("전체");
        }

        foreach (var (label, key, value) in results)
        {
            if (!_settings.PinnedCases.Contains(key))
                AddResultRow(label, key, value);
        }

        int wordCount = CaseConverter.ParseWords(input).Count;
        StatusText.Text = $"단어 {wordCount}개 — 클릭 또는 ↑↓ Enter";
    }

    private void RenderMultiline(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            AddSectionLabel($"[{i + 1}] {line}");

            var results = _pinnedOnly
                ? CaseConverter.ConvertAll(line).Where(r => _settings.PinnedCases.Contains(r.Key)).ToArray()
                : CaseConverter.ConvertAll(line);

            foreach (var (label, key, value) in results)
            {
                if (!_pinnedOnly || _settings.PinnedCases.Contains(key))
                    AddResultRow(label, key, value);
            }
        }
        StatusText.Text = $"{lines.Length}개 항목 일괄 변환 — 클릭 또는 ↑↓ Enter";
    }

    private void AddSectionLabel(string text)
        => ResultPanel.Children.Add(new TextBlock
        {
            Text       = text,
            Foreground = (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily = new WpfFontFamily("Segoe UI"),
            FontSize   = 10,
            Margin     = new Thickness(2, 6, 0, 2),
        });

    private void AddResultRow(string label, string key, string value)
    {
        var border = MakeRow(label, key, value);
        _rows.Add(border);
        _rowData[border] = (value, label);
        ResultPanel.Children.Add(border);
    }

    private void RenderHistory()
    {
        var history = _settings.RecentHistory;
        if (history.Count == 0) return;

        AddSectionLabel("최근 입력 (클릭: 입력창 채움 / ⎘ 클릭: 원문 즉시 복사)");
        foreach (var h in history.Take(8))
        {
            var border = new Border
            {
                Margin       = new Thickness(0, 2, 0, 2),
                Padding      = new Thickness(12, 7, 8, 7),
                CornerRadius = new CornerRadius(6),
                Background   = (SolidColorBrush)FindResource("SurfaceBrush"),
                Cursor       = System.Windows.Input.Cursors.Hand,
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBlock
            {
                Text              = h,
                Foreground        = (SolidColorBrush)FindResource("TextPrimary"),
                FontFamily        = new WpfFontFamily("Consolas, Segoe UI"),
                FontSize          = 12,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // 원문 즉시 복사 버튼
            string t = h;
            var copyTb = new TextBlock
            {
                Text                = "⎘",
                FontSize            = 13,
                Foreground          = (SolidColorBrush)FindResource("TextSecondary"),
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Cursor              = System.Windows.Input.Cursors.Hand,
                Width               = 22,
                TextAlignment       = TextAlignment.Center,
                ToolTip             = "원문 즉시 복사",
            };
            copyTb.MouseEnter += (_, _) => copyTb.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            copyTb.MouseLeave += (_, _) => copyTb.Foreground = (SolidColorBrush)FindResource("TextSecondary");
            copyTb.MouseLeftButtonUp += (_, args) =>
            {
                args.Handled = true;
                try { System.Windows.Clipboard.SetText(t); } catch { }
                StatusText.Foreground = (SolidColorBrush)FindResource("CopyFeedback");
                StatusText.Text = $"✓ 이력 복사됨!";
                _ = Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(Hide));
            };

            Grid.SetColumn(tb, 0);
            Grid.SetColumn(copyTb, 1);
            rowGrid.Children.Add(tb);
            rowGrid.Children.Add(copyTb);
            border.Child = rowGrid;

            border.MouseEnter        += (_, _) => { if (_rows.IndexOf(border) != _selectedIndex) border.Background = (SolidColorBrush)FindResource("RowHover"); };
            border.MouseLeave        += (_, _) => { if (_rows.IndexOf(border) != _selectedIndex) border.Background = (SolidColorBrush)FindResource("SurfaceBrush"); };
            border.MouseLeftButtonUp += (_, _) =>
            {
                InputBox.Text = t;
                InputBox.CaretIndex = t.Length;
                InputBox.Focus();
            };
            _rows.Add(border);
            _rowData[border] = (t, "history");
            ResultPanel.Children.Add(border);
        }
    }

    private Border MakeRow(string label, string key, string value)
    {
        bool hasValue = !string.IsNullOrEmpty(value);
        bool isPinned = _settings.PinnedCases.Contains(key);

        var border = new Border
        {
            Margin       = new Thickness(0, 2, 0, 2),
            Padding      = new Thickness(12, 8, 6, 8),
            CornerRadius = new CornerRadius(6),
            Background   = (SolidColorBrush)FindResource("SurfaceBrush"),
            Cursor       = hasValue ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var lblText = new TextBlock
        {
            Text              = label,
            Foreground        = (SolidColorBrush)FindResource("LabelColor"),
            FontFamily        = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valText = new TextBlock
        {
            Text              = hasValue ? value : "—",
            Foreground        = hasValue
                ? (SolidColorBrush)FindResource("TextPrimary")
                : (SolidColorBrush)FindResource("TextSecondary"),
            FontFamily        = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize          = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping      = TextWrapping.Wrap,
        };

        // 핀 토글 (TextBlock 기반 — template 불필요)
        var pinTb = new TextBlock
        {
            Text              = isPinned ? "★" : "☆",
            FontSize          = 13,
            Foreground        = isPinned
                ? (SolidColorBrush)FindResource("CopyFeedback")
                : (SolidColorBrush)FindResource("TextSecondary"),
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Cursor              = System.Windows.Input.Cursors.Hand,
            Width               = 22,
            TextAlignment       = TextAlignment.Center,
        };

        string captureKey = key;
        pinTb.MouseEnter += (_, _) => pinTb.Foreground = (SolidColorBrush)FindResource("AccentBrush");
        pinTb.MouseLeave += (_, _) => pinTb.Foreground = _settings.PinnedCases.Contains(captureKey)
            ? (SolidColorBrush)FindResource("CopyFeedback")
            : (SolidColorBrush)FindResource("TextSecondary");
        pinTb.MouseLeftButtonUp += (_, args) =>
        {
            args.Handled = true;
            _settings = SettingsService.Load();
            if (_settings.PinnedCases.Contains(captureKey))
                _settings.PinnedCases.Remove(captureKey);
            else
                _settings.PinnedCases.Add(captureKey);
            SettingsService.Save(_settings);
            RenderResults(InputBox.Text);
        };

        Grid.SetColumn(lblText, 0);
        Grid.SetColumn(valText, 1);
        Grid.SetColumn(pinTb,   2);
        grid.Children.Add(lblText);
        grid.Children.Add(valText);
        grid.Children.Add(pinTb);
        border.Child = grid;

        if (hasValue)
        {
            string copyValue = value;
            border.MouseEnter += (_, _) =>
            {
                if (_rows.IndexOf(border) != _selectedIndex)
                    border.Background = (SolidColorBrush)FindResource("RowHover");
            };
            border.MouseLeave += (_, _) =>
            {
                if (_rows.IndexOf(border) != _selectedIndex)
                    border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            };
            // 핀 TextBlock이 args.Handled=true로 버블링 차단하므로 여기까지 오면 행 클릭
            border.MouseLeftButtonUp += (_, _) => CopyAndClose(copyValue, label);
        }

        return border;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            bool sel = i == _selectedIndex;
            _rows[i].Background      = sel
                ? (SolidColorBrush)FindResource("RowHover")
                : (SolidColorBrush)FindResource("SurfaceBrush");
            _rows[i].BorderThickness = sel ? new Thickness(2, 0, 0, 0) : new Thickness(0);
            _rows[i].BorderBrush     = sel ? (SolidColorBrush)FindResource("AccentBrush") : null;
        }
        if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
            _rows[_selectedIndex].BringIntoView();
    }

    private async void CopyAndClose(string text, string label)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            SettingsService.AddHistory(_settings, InputBox.Text.Trim()); // 이중 Load() 없이 1회 Save
            StatusText.Foreground = (SolidColorBrush)FindResource("CopyFeedback");
            StatusText.Text = $"✓ {label} 복사됨!";
        }
        catch
        {
            StatusText.Text = "⚠ 클립보드 복사 실패";
        }
        await Task.Delay(500);
        Hide();
    }

    // ── 버튼 이벤트 ──────────────────────────────────────────────────────
    private void PinFilterBtn_Click(object sender, RoutedEventArgs e)
    {
        _pinnedOnly = !_pinnedOnly;
        PinFilterBtn.Content    = _pinnedOnly ? "★" : "☆";
        PinFilterBtn.Foreground = _pinnedOnly
            ? (SolidColorBrush)FindResource("CopyFeedback")
            : (SolidColorBrush)FindResource("TextSecondary");
        RenderResults(InputBox.Text);
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => ((App)System.Windows.Application.Current).ShowSettings();

    private void CopyAllBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) return;
        var lines = _rowData.Values
            .Where(d => d.label != "history" && !string.IsNullOrEmpty(d.value))
            .Select(d => $"{d.label}: {d.value}");
        var all = string.Join("\n", lines);
        try
        {
            System.Windows.Clipboard.SetText(all);
            StatusText.Foreground = (SolidColorBrush)FindResource("CopyFeedback");
            StatusText.Text = $"✓ 전체 {_rows.Count}개 복사됨!";
        }
        catch
        {
            StatusText.Text = "⚠ 클립보드 복사 실패";
        }
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────
    private void ClearInputBtn_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Clear();
        InputBox.Focus();
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        bool empty = string.IsNullOrEmpty(InputBox.Text);
        Placeholder.Visibility  = empty ? Visibility.Visible  : Visibility.Collapsed;
        ClearInputBtn.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        RenderResults(InputBox.Text);
    }

    private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when _rows.Count > 0:
                _selectedIndex = Math.Min(_selectedIndex + 1, _rows.Count - 1);
                UpdateSelection();
                e.Handled = true;
                break;

            case Key.Up:
                if (_selectedIndex > 0)       _selectedIndex--;
                else if (_selectedIndex == 0) _selectedIndex = -1;
                UpdateSelection();
                e.Handled = true;
                break;

            case Key.Enter when _selectedIndex >= 0 && _selectedIndex < _rows.Count:
                var (v, l) = _rowData[_rows[_selectedIndex]];
                if (l == "history")
                {
                    InputBox.Text = v;
                    InputBox.CaretIndex = v.Length;
                    InputBox.Focus();
                }
                else
                {
                    CopyAndClose(v, l);
                }
                e.Handled = true;
                break;

            case Key.Enter when e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.None
                             && _selectedIndex < 0:
                // 멀티라인 입력 허용 — 기본 동작 유지 (줄바꿈 삽입)
                break;

            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (IsVisible) Hide();
        StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondary");
    }
}
