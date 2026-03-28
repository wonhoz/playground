using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LocaleView;

public class LocaleItem
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Region { get; set; } = "";
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
    public bool IsPinned { get; set; }
    public string DisplayName => IsPinned ? $"★ {Name}" : Name;
}

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private List<LocaleItem> _allLocales = [];
    private List<LocaleItem> _filtered = [];
    private LocaleItem? _selected;
    private DateTime _sampleDate = new(2026, 3, 14, 15, 30, 45);
    private double _sampleNumber = 1234567.89;
    private HashSet<string> _pinned = [];

    private string PinnedFilePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocaleView");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "pinned.json");
        }
    }

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        SampleDateBox.Text = _sampleDate.ToString("yyyy-MM-dd");
        SampleNumberBox.Text = _sampleNumber.ToString("0.##");

        LoadPinned();
        LoadLocales();
        StatusBar.Text = $"{_allLocales.Count}개 로케일 로드됨. 로케일을 선택하면 상세 정보가 표시됩니다.";
    }

    // ─── 즐겨찾기 ───────────────────────────────────────────────────────────

    void LoadPinned()
    {
        try
        {
            if (File.Exists(PinnedFilePath))
            {
                var codes = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PinnedFilePath));
                _pinned = [.. (codes ?? [])];
            }
        }
        catch { }
    }

    void SavePinned()
    {
        try { File.WriteAllText(PinnedFilePath, JsonSerializer.Serialize(_pinned.ToList())); }
        catch { }
    }

    void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_pinned.Contains(_selected.Code))
        {
            _pinned.Remove(_selected.Code);
            _selected.IsPinned = false;
            BtnPin.Content = "☆";
            BtnPin.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            StatusBar.Text = $"[{_selected.Code}] 즐겨찾기 해제됨";
        }
        else
        {
            _pinned.Add(_selected.Code);
            _selected.IsPinned = true;
            BtnPin.Content = "★";
            BtnPin.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));
            StatusBar.Text = $"[{_selected.Code}] 즐겨찾기 등록됨";
        }
        SavePinned();
        var reselect = _selected;
        UpdateLocaleList();
        LocaleGrid.SelectedItem = reselect;
    }

    // ─── 로케일 로드 & 검색 ─────────────────────────────────────────────────

    void LoadLocales()
    {
        _allLocales = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c =>
            {
                string region = "";
                try { region = new RegionInfo(c.Name).EnglishName; } catch { }
                return new LocaleItem
                {
                    Code = c.Name,
                    Name = c.EnglishName,
                    NativeName = c.NativeName,
                    Region = region.Length > 0 ? region[..Math.Min(region.Length, 12)] : "",
                    Culture = c,
                    IsPinned = _pinned.Contains(c.Name)
                };
            })
            .OrderBy(l => l.Name)
            .ToList();

        UpdateLocaleList();
    }

    void UpdateLocaleList()
    {
        string q = IsLoaded ? SearchBox.Text.Trim().ToLower() : "";
        var base_ = string.IsNullOrEmpty(q)
            ? _allLocales
            : _allLocales.Where(l =>
                l.Name.ToLower().Contains(q) ||
                l.Code.ToLower().Contains(q) ||
                l.NativeName.ToLower().Contains(q) ||
                l.Region.ToLower().Contains(q));

        _filtered = [.. base_.OrderBy(l => l.IsPinned ? 0 : 1).ThenBy(l => l.Name)];
        LocaleGrid.ItemsSource = _filtered;
        CountText.Text = $"{_filtered.Count} / {_allLocales.Count}";

        if (IsLoaded)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            NoResultText.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateLocaleList();
    }

    void LocaleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocaleGrid.SelectedItem is not LocaleItem item) return;
        _selected = item;
        ShowLocaleDetail(item);
    }

    // ─── 샘플 값 입력 ────────────────────────────────────────────────────────

    void SampleDateBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DateTime.TryParseExact(SampleDateBox.Text, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            _sampleDate = d;
            SampleDateBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            if (_selected != null) ShowLocaleDetail(_selected);
        }
        else
        {
            SampleDateBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0x44, 0x44));
            StatusBar.Text = "날짜 형식 오류 — yyyy-MM-dd 형식으로 입력하세요 (예: 2026-03-14)";
        }
    }

    void SampleNumberBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(SampleNumberBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
        {
            _sampleNumber = n;
            SampleNumberBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            if (_selected != null) ShowLocaleDetail(_selected);
        }
        else
        {
            SampleNumberBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0x44, 0x44));
            StatusBar.Text = "숫자 형식 오류 — 숫자를 입력하세요 (예: 1234567.89)";
        }
    }

    void SampleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender == SampleDateBox) SampleDateBox_LostFocus(sender, e);
            else SampleNumberBox_LostFocus(sender, e);
            Keyboard.ClearFocus();
        }
    }

    // ─── 키보드 단축키 ───────────────────────────────────────────────────────

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(SearchBox.Text))
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }

    void BtnHelp_Click(object sender, RoutedEventArgs e) => ShowHelp();

    void ShowHelp()
    {
        var win = new HelpWindow { Owner = this };
        win.ShowDialog();
    }

    // ─── 상세 뷰 ─────────────────────────────────────────────────────────────

    void ShowLocaleDetail(LocaleItem item)
    {
        var c = item.Culture;
        LocaleName.Text = item.Name;
        LocaleCode.Text = $"[{item.Code}]";
        LocaleNative.Text = item.NativeName;

        BtnPin.Visibility = Visibility.Visible;
        BtnPin.Content = item.IsPinned ? "★" : "☆";
        BtnPin.Foreground = item.IsPinned
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00))
            : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

        // 날짜/시간 탭
        DateTimePanel.Children.Clear();
        AddSection(DateTimePanel, "날짜 형식", [
            ("ShortDate",    _sampleDate.ToString(c.DateTimeFormat.ShortDatePattern, c)),
            ("LongDate",     _sampleDate.ToString(c.DateTimeFormat.LongDatePattern, c)),
            ("ShortTime",    _sampleDate.ToString(c.DateTimeFormat.ShortTimePattern, c)),
            ("LongTime",     _sampleDate.ToString(c.DateTimeFormat.LongTimePattern, c)),
            ("FullDateTime", _sampleDate.ToString(c.DateTimeFormat.FullDateTimePattern, c)),
            ("RFC1123",      _sampleDate.ToString("R", c)),
            ("Sortable",     _sampleDate.ToString("s", c)),
        ]);
        AddSection(DateTimePanel, "요일 이름", [
            ("일요일", c.DateTimeFormat.DayNames[0]),
            ("월요일", c.DateTimeFormat.DayNames[1]),
            ("화요일", c.DateTimeFormat.DayNames[2]),
            ("수요일", c.DateTimeFormat.DayNames[3]),
            ("목요일", c.DateTimeFormat.DayNames[4]),
            ("금요일", c.DateTimeFormat.DayNames[5]),
            ("토요일", c.DateTimeFormat.DayNames[6]),
        ]);
        AddSection(DateTimePanel, "월 이름", Enumerable.Range(0, 12)
            .Select(i => ($"{i + 1}월", c.DateTimeFormat.MonthNames[i])).ToArray());

        // 숫자/통화 탭
        NumberPanel.Children.Clear();
        var nf = c.NumberFormat;
        AddSection(NumberPanel, "숫자 형식", [
            ("소수점 구분자",   nf.NumberDecimalSeparator),
            ("천 단위 구분자",  nf.NumberGroupSeparator),
            ("음수 기호",       nf.NegativeSign),
            ("숫자 예시",       _sampleNumber.ToString("N", c)),
            ("퍼센트 예시",     (_sampleNumber / 1000000).ToString("P", c)),
        ]);
        AddSection(NumberPanel, "통화 형식", [
            ("통화 기호",       nf.CurrencySymbol),
            ("소수점 구분자",   nf.CurrencyDecimalSeparator),
            ("천 단위 구분자",  nf.CurrencyGroupSeparator),
            ("양수 패턴",       $"패턴 {nf.CurrencyPositivePattern}"),
            ("음수 패턴",       $"패턴 {nf.CurrencyNegativePattern}"),
            ("통화 예시",       _sampleNumber.ToString("C", c)),
            ("음수 예시",       (-_sampleNumber / 1000).ToString("C", c)),
        ]);

        // 달력/시간대 탭
        CalendarPanel.Children.Clear();
        string calType = c.Calendar.GetType().Name.Replace("Calendar", "");
        AddSection(CalendarPanel, "달력 시스템", [
            ("달력 유형",    calType),
            ("최소 연도",    c.Calendar.MinSupportedDateTime.Year.ToString()),
            ("최대 연도",    c.Calendar.MaxSupportedDateTime.Year == 9999 ? "9999" : c.Calendar.MaxSupportedDateTime.Year.ToString()),
            ("현재 연도",    c.Calendar.GetYear(_sampleDate).ToString()),
            ("현재 월",      c.Calendar.GetMonth(_sampleDate).ToString()),
            ("현재 일",      c.Calendar.GetDayOfMonth(_sampleDate).ToString()),
        ]);
        AddSection(CalendarPanel, "텍스트 방향", [
            ("RTL 여부",   c.TextInfo.IsRightToLeft ? "오른쪽 → 왼쪽" : "왼쪽 → 오른쪽"),
            ("대소문자",   c.TextInfo.CultureName),
        ]);

        try
        {
            var ri = new RegionInfo(c.Name);
            AddSection(CalendarPanel, "지역 정보", [
                ("영문명",           ri.EnglishName),
                ("현지명",           ri.NativeName),
                ("ISO 2코드",        ri.TwoLetterISORegionName),
                ("ISO 3코드",        ri.ThreeLetterISORegionName),
                ("통화코드",         ri.ISOCurrencySymbol),
                ("통화 현지명",      ri.CurrencyNativeName),
                ("미터법",           ri.IsMetric ? "예" : "아니오"),
            ]);
        }
        catch { }

        // C# 코드 탭
        CodePanel.Children.Clear();
        var sb = new StringBuilder();
        sb.AppendLine($"// Locale.View — {item.Code} ({item.Name})");
        sb.AppendLine($"var culture = CultureInfo.GetCultureInfo(\"{item.Code}\");");
        sb.AppendLine();
        sb.AppendLine($"// 날짜 형식");
        sb.AppendLine($"DateTime.Now.ToString(culture.DateTimeFormat.ShortDatePattern, culture);");
        sb.AppendLine($"DateTime.Now.ToString(\"D\", culture);  // 긴 날짜");
        sb.AppendLine($"DateTime.Now.ToString(\"F\", culture);  // 전체 날짜+시간");
        sb.AppendLine();
        sb.AppendLine($"// 숫자/통화 형식");
        sb.AppendLine($"({_sampleNumber}).ToString(\"N\", culture);   // {_sampleNumber.ToString("N", c)}");
        sb.AppendLine($"({_sampleNumber}).ToString(\"C\", culture);   // {_sampleNumber.ToString("C", c)}");
        sb.AppendLine($"(0.123).ToString(\"P\", culture);     // {(0.123).ToString("P", c)}");
        sb.AppendLine();
        sb.AppendLine($"// 달력");
        sb.AppendLine($"culture.Calendar.GetType().Name;     // {calType}Calendar");

        var codeTb = new TextBox
        {
            Text = sb.ToString(),
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x20)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xDD, 0xFF)),
            BorderThickness = new Thickness(0),
            IsReadOnly = true,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            Padding = new Thickness(12),
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        CodePanel.Children.Add(codeTb);

        StatusBar.Text = $"[{item.Code}] {item.Name} — {item.NativeName}  |  값 클릭 시 클립보드 복사";
    }

    void AddSection(StackPanel parent, string title, (string Key, string Value)[] rows)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title, FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
            Margin = new Thickness(0, parent.Children.Count == 0 ? 0 : 20, 0, 8)
        });

        foreach (var (key, val) in rows)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyTb = new TextBlock
            {
                Text = key,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 12
            };
            var valTb = new TextBlock
            {
                Text = val,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Cursor = Cursors.Hand,
                ToolTip = "클릭하여 복사"
            };
            valTb.MouseLeftButtonUp += (_, _) =>
            {
                Clipboard.SetText(val);
                StatusBar.Text = $"복사됨: {val}";
            };
            Grid.SetColumn(keyTb, 0);
            Grid.SetColumn(valTb, 1);
            row.Children.Add(keyTb);
            row.Children.Add(valTb);
            parent.Children.Add(row);
        }
    }

    void BtnCopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        string code = $"CultureInfo.GetCultureInfo(\"{_selected.Code}\")";
        Clipboard.SetText(code);
        StatusBar.Text = $"클립보드에 복사: {code}";
    }

    void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        if (_allLocales.Count == 0) return;
        var dlg = new CompareDialog(_allLocales) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Selected.Count > 0)
        {
            var win = new CompareWindow(dlg.Selected, _sampleDate, _sampleNumber) { Owner = this };
            win.Show();
        }
    }
}

// ─── 비교 선택 다이얼로그 ─────────────────────────────────────────────
public class CompareDialog : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
    public List<LocaleItem> Selected { get; private set; } = [];

    public CompareDialog(List<LocaleItem> all)
    {
        Title = "비교 로케일 선택 (최대 4개)";
        Width = 400; Height = 520;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

        var lb = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderThickness = new Thickness(0),
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = all.Select(l => $"{l.Code} — {l.Name}").ToList()
        };
        grid.Children.Add(lb);

        var countLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 11,
            Margin = new Thickness(12, 4, 12, 4),
            Text = "0개 선택됨"
        };
        Grid.SetRow(countLabel, 1);
        lb.SelectionChanged += (_, _) =>
        {
            int n = lb.SelectedItems.Count;
            countLabel.Text = n > 4 ? $"{n}개 선택됨 (4개 초과분은 제외됩니다)" : $"{n}개 선택됨";
            countLabel.Foreground = n > 4
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x44))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        };
        grid.Children.Add(countLabel);

        var btn = new Button
        {
            Content = "비교", Margin = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
            Foreground = Brushes.White,
            Template = CreateButtonTemplate()
        };
        Grid.SetRow(btn, 2);
        btn.Click += (_, _) =>
        {
            Selected = lb.SelectedItems.Cast<string>()
                .Take(4)
                .Select(s => all.First(l => s.StartsWith(l.Code + " —")))
                .ToList();
            DialogResult = true;
        };
        grid.Children.Add(btn);
        Content = grid;

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
    }

    public static ControlTemplate CreateButtonTemplatePublic() => CreateButtonTemplate();
    static ControlTemplate CreateButtonTemplate()
    {
        var tmpl = new ControlTemplate(typeof(Button));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        bd.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        tmpl.VisualTree = bd;
        return tmpl;
    }
}

// ─── 비교 창 ─────────────────────────────────────────────────────────
public class CompareWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public CompareWindow(List<LocaleItem> items, DateTime sample, double sampleNumber)
    {
        Title = "로케일 비교"; Width = 1000; Height = 640;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Grid();
        outer.RowDefinitions.Add(new RowDefinition());
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

        var sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var grid = new Grid();
        for (int i = 0; i < items.Count; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

        // 비교 데이터 수집 (복사용)
        var copyHeaders = new List<string>();
        var copyRows = new Dictionary<string, List<string>>();
        string[] rowKeys = ["ShortDate", "LongDate", "ShortTime", "통화", "숫자", "퍼센트", "통화 기호", "달력 유형", "RTL"];
        foreach (var rk in rowKeys) copyRows[rk] = [];

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var c = item.Culture;
            copyHeaders.Add($"{item.Code} ({item.Name})");

            var panel = new StackPanel { Margin = new Thickness(12) };

            panel.Children.Add(new TextBlock
            {
                Text = $"{item.Code}", FontWeight = FontWeights.Bold, FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.NativeName, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            void AddRow(string key, string val)
            {
                panel.Children.Add(new TextBlock { Text = key, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) });
                panel.Children.Add(new TextBlock { Text = val, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) });
                copyRows[key].Add(val);
            }

            AddRow("ShortDate", sample.ToString(c.DateTimeFormat.ShortDatePattern, c));
            AddRow("LongDate", sample.ToString(c.DateTimeFormat.LongDatePattern, c));
            AddRow("ShortTime", sample.ToString(c.DateTimeFormat.ShortTimePattern, c));
            AddRow("통화", sampleNumber.ToString("C", c));
            AddRow("숫자", sampleNumber.ToString("N", c));
            AddRow("퍼센트", (sampleNumber / 1000000).ToString("P", c));
            try { AddRow("통화 기호", new RegionInfo(c.Name).ISOCurrencySymbol); } catch { copyRows["통화 기호"].Add("—"); }
            AddRow("달력 유형", c.Calendar.GetType().Name.Replace("Calendar", ""));
            AddRow("RTL", c.TextInfo.IsRightToLeft ? "예" : "아니오");

            Grid.SetColumn(panel, i);
            grid.Children.Add(panel);
        }

        sv.Content = grid;
        outer.Children.Add(sv);

        // 결과 복사 버튼
        var copyBtn = new Button
        {
            Content = "📋 결과 복사 (탭 구분)",
            Margin = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            Template = CompareDialog.CreateButtonTemplatePublic()
        };
        Grid.SetRow(copyBtn, 1);
        copyBtn.Click += (_, _) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("\t" + string.Join("\t", copyHeaders));
            foreach (var rk in rowKeys)
                sb.AppendLine(rk + "\t" + string.Join("\t", copyRows[rk]));
            Clipboard.SetText(sb.ToString());
        };
        outer.Children.Add(copyBtn);
        Content = outer;

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
    }
}

// ─── 도움말 창 ───────────────────────────────────────────────────────
public class HelpWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public HelpWindow()
    {
        Title = "Locale.View — 사용법";
        Width = 440; Height = 500;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var sv = new ScrollViewer { Padding = new Thickness(20) };
        var sp = new StackPanel();

        void AddTitle(string t) => sp.Children.Add(new TextBlock
        {
            Text = t, FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
            Margin = new Thickness(0, sp.Children.Count == 0 ? 0 : 16, 0, 6)
        });

        void AddItem(string key, string desc)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var k = new TextBlock { Text = key, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xCC, 0xFF)), FontSize = 12 };
            var d = new TextBlock { Text = desc, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(d, 1);
            g.Children.Add(k);
            g.Children.Add(d);
            sp.Children.Add(g);
        }

        AddTitle("🔍 검색");
        AddItem("이름·코드·지역", "영문명, BCP-47 코드, 국가명으로 검색");

        AddTitle("📌 즐겨찾기");
        AddItem("★ 버튼 (헤더)", "로케일을 즐겨찾기 등록 — 목록 상단 고정, 재시작 후 복원");

        AddTitle("📋 복사");
        AddItem("값 클릭", "속성 값을 클릭하면 클립보드에 복사됨");
        AddItem("코드 복사 버튼", "CultureInfo.GetCultureInfo(\"...\") 코드 복사");

        AddTitle("⚖️ 비교 모드");
        AddItem("비교 모드 버튼", "로케일 최대 4개를 나란히 비교");
        AddItem("결과 복사 버튼", "비교 결과를 탭 구분 텍스트로 복사 (Excel 붙여넣기 가능)");

        AddTitle("🗓️ 샘플 값");
        AddItem("날짜 입력 (툴바)", "yyyy-MM-dd 형식으로 날짜 변경 → Enter 또는 포커스 이동");
        AddItem("숫자 입력 (툴바)", "포맷 미리보기에 사용할 기준 숫자 변경");

        AddTitle("⌨️ 단축키");
        AddItem("F1", "이 도움말 창 열기");
        AddItem("Ctrl+F", "검색창 포커스");
        AddItem("Escape", "검색 초기화");

        sv.Content = sp;
        Content = sv;

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
    }
}
