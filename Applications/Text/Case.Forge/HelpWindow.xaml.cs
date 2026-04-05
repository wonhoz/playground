namespace CaseForge;

public partial class HelpWindow : System.Windows.Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public HelpWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        int v = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref v, sizeof(int));

        var settings = SettingsService.Load();
        HotkeyText.Text = SettingsService.FormatHotkey(settings.HotkeyModifiers, settings.HotkeyVK);

        var steps = new[]
        {
            "1.  단축키로 팝업 호출 — 클립보드 텍스트 자동 입력",
            "2.  변환할 텍스트를 입력창에 입력",
            "3.  결과 행 클릭 또는 ↑↓ Enter 키로 선택 복사",
            "4.  클립보드에 복사 완료 후 팝업 자동 닫힘",
            "5.  입력란 비우면 최근 변환 이력 표시 (↑↓ Enter로 재입력)",
        };
        foreach (var s in steps)
            StepsPanel.Children.Add(MakeHelpLine(s));

        var keys = new[]
        {
            ("↑ ↓",    "결과 탐색"),
            ("Enter",   "선택 항목 복사 / 이력 항목은 입력창에 채움"),
            ("Escape",  "팝업 닫기"),
        };
        foreach (var (key, desc) in keys)
            KeysPanel.Children.Add(MakeKeyRow(key, desc));

        var features = new[]
        {
            ("☆ / ★",       "각 결과 행 우측 버튼 — 즐겨찾기 추가/제거"),
            ("☆ 헤더 토글",  "헤더 ☆ 클릭 — 즐겨찾기 케이스만 보기 전환"),
            ("전체 복사",    "상태바 버튼 — 전체 결과를 label: value 형태로 복사"),
            ("멀티라인",     "입력창에 줄바꿈(Enter) — 여러 단어 일괄 변환"),
            ("⚙ 설정",      "헤더 ⚙ 버튼 또는 트레이 메뉴 — 단축키·즐겨찾기 설정"),
        };
        foreach (var (key, desc) in features)
            FeaturesPanel.Children.Add(MakeKeyRow(key, desc));
    }

    private UIElement MakeHelpLine(string text)
        => new TextBlock
        {
            Text         = text,
            Foreground   = (SolidColorBrush)FindResource("TextPrimary"),
            FontFamily   = new WpfFontFamily("Segoe UI"),
            FontSize     = 12,
            Margin       = new Thickness(0, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap,
        };

    private UIElement MakeKeyRow(string key, string desc)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var kTb = new TextBlock
        {
            Text       = key,
            Foreground = (SolidColorBrush)FindResource("AccentBrush"),
            FontFamily = new WpfFontFamily("Consolas, Segoe UI"),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var dTb = new TextBlock
        {
            Text       = desc,
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            FontFamily = new WpfFontFamily("Segoe UI"),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(kTb, 0);
        Grid.SetColumn(dTb, 1);
        grid.Children.Add(kTb);
        grid.Children.Add(dTb);
        return grid;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
}
