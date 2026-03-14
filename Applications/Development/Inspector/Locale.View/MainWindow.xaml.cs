using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LocaleView;

public class LocaleItem
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Region { get; set; } = "";
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
}

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private List<LocaleItem> _allLocales = [];
    private List<LocaleItem> _filtered = [];
    private LocaleItem? _selected;
    private readonly DateTime _sampleDate = new(2026, 3, 14, 15, 30, 45);
    private readonly List<LocaleItem> _compareList = [];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        LoadLocales();
        StatusBar.Text = $"{_allLocales.Count}개 로케일 로드됨. 로케일을 선택하면 상세 정보가 표시됩니다.";
    }

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
                    Culture = c
                };
            })
            .OrderBy(l => l.Name)
            .ToList();

        _filtered = [.. _allLocales];
        LocaleGrid.ItemsSource = _filtered;
        CountText.Text = $"{_filtered.Count} / {_allLocales.Count}";
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = SearchBox.Text.Trim().ToLower();
        _filtered = string.IsNullOrEmpty(q)
            ? [.. _allLocales]
            : [.. _allLocales.Where(l =>
                l.Name.ToLower().Contains(q) ||
                l.Code.ToLower().Contains(q) ||
                l.NativeName.ToLower().Contains(q) ||
                l.Region.ToLower().Contains(q))];
        LocaleGrid.ItemsSource = _filtered;
        CountText.Text = $"{_filtered.Count} / {_allLocales.Count}";
    }

    void LocaleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocaleGrid.SelectedItem is not LocaleItem item) return;
        _selected = item;
        ShowLocaleDetail(item);
    }

    void ShowLocaleDetail(LocaleItem item)
    {
        var c = item.Culture;
        LocaleName.Text = item.Name;
        LocaleCode.Text = $"[{item.Code}]";
        LocaleNative.Text = item.NativeName;

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
            .Select(i => (c.DateTimeFormat.MonthNames[i], c.DateTimeFormat.MonthNames[i])).ToArray());

        // 숫자/통화 탭
        NumberPanel.Children.Clear();
        var nf = c.NumberFormat;
        AddSection(NumberPanel, "숫자 형식", [
            ("소수점 구분자",   nf.NumberDecimalSeparator),
            ("천 단위 구분자",  nf.NumberGroupSeparator),
            ("음수 기호",       nf.NegativeSign),
            ("숫자 예시",       (1234567.89).ToString("N", c)),
            ("퍼센트 예시",     (0.1234).ToString("P", c)),
        ]);
        AddSection(NumberPanel, "통화 형식", [
            ("통화 기호",       nf.CurrencySymbol),
            ("소수점 구분자",   nf.CurrencyDecimalSeparator),
            ("천 단위 구분자",  nf.CurrencyGroupSeparator),
            ("양수 패턴",       $"패턴 {nf.CurrencyPositivePattern}"),
            ("음수 패턴",       $"패턴 {nf.CurrencyNegativePattern}"),
            ("통화 예시",       (1234567.89).ToString("C", c)),
            ("음수 예시",       (-1234.56).ToString("C", c)),
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
        sb.AppendLine($"(1234.56).ToString(\"N\", culture);   // {(1234.56).ToString("N", c)}");
        sb.AppendLine($"(1234.56).ToString(\"C\", culture);   // {(1234.56).ToString("C", c)}");
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

        StatusBar.Text = $"[{item.Code}] {item.Name} — {item.NativeName}";
    }

    void AddSection(StackPanel parent, string title, (string Key, string Value)[] rows)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title, FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0)),
            Margin = new Thickness(0, parent.Children.Count == 0 ? 0 : 20, 0, 8)
        });

        foreach (var (key, val) in rows)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyTb = new TextBlock
            {
                Text = key, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 12
            };
            var valTb = new TextBlock
            {
                Text = val, Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
                TextWrapping = TextWrapping.Wrap
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

        // 비교할 4개 로케일 선택 다이얼로그
        var dlg = new CompareDialog(_allLocales) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Selected.Count > 0)
        {
            var win = new CompareWindow(dlg.Selected, _sampleDate) { Owner = this };
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
        Width = 400; Height = 480;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
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

        var btn = new Button
        {
            Content = "비교", Margin = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0)),
            Foreground = Brushes.White,
            Template = CreateButtonTemplate()
        };
        Grid.SetRow(btn, 1);
        btn.Click += (_, _) =>
        {
            Selected = lb.SelectedItems.Cast<string>()
                .Take(4)
                .Select(s => all.First(l => s.StartsWith(l.Code)))
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

    public CompareWindow(List<LocaleItem> items, DateTime sample)
    {
        Title = "로케일 비교"; Width = 1000; Height = 600;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var grid = new Grid();
        for (int i = 0; i < items.Count; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var c = item.Culture;
            var panel = new StackPanel { Margin = new Thickness(12) };

            panel.Children.Add(new TextBlock
            {
                Text = $"{item.Code}", FontWeight = FontWeights.Bold, FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0)),
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
            }

            AddRow("ShortDate", sample.ToString(c.DateTimeFormat.ShortDatePattern, c));
            AddRow("LongDate", sample.ToString(c.DateTimeFormat.LongDatePattern, c));
            AddRow("ShortTime", sample.ToString(c.DateTimeFormat.ShortTimePattern, c));
            AddRow("통화", (1234567.89).ToString("C", c));
            AddRow("숫자", (1234567.89).ToString("N", c));
            AddRow("퍼센트", (0.1234).ToString("P", c));
            try { AddRow("통화 기호", new RegionInfo(c.Name).ISOCurrencySymbol); } catch { }
            AddRow("달력 유형", c.Calendar.GetType().Name.Replace("Calendar", ""));
            AddRow("RTL", c.TextInfo.IsRightToLeft ? "예" : "아니오");

            Grid.SetColumn(panel, i);
            grid.Children.Add(panel);

            if (i < items.Count - 1)
            {
                var sep = new Border { Width = 1, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) };
                // separator handled by margin
            }
        }

        outer.Content = grid;
        Content = outer;

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
    }
}
