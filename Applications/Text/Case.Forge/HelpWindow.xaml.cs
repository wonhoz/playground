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
            "1.  단축키로 팝업 호출 (클립보드 텍스트 자동 입력, 설정에서 끄기 가능)",
            "2.  변환할 텍스트를 입력창에 입력 — 입력 즉시 실시간 변환",
            "3.  결과 행 클릭 또는 ↑↓ Enter 키로 선택 복사",
            "4.  클립보드에 복사 완료 후 팝업 자동 닫힘",
            "5.  입력란 비우면 최근 변환 이력 표시 — ↑↓ Enter로 재입력, ⎘ 클릭으로 원문 즉시 복사",
        };
        foreach (var s in steps)
            StepsPanel.Children.Add(MakeHelpLine(s));

        var keys = new[]
        {
            ("↑ ↓",    "결과 / 이력 탐색"),
            ("Enter",   "선택 항목 복사 (이력 항목은 입력창에 채움)"),
            ("Escape",  "팝업 닫기"),
            ("✕ 버튼",  "입력창 우측 — 입력 내용 한 번에 지우기"),
        };
        foreach (var (key, desc) in keys)
            KeysPanel.Children.Add(MakeKeyRow(key, desc));

        var features = new[]
        {
            ("☆ / ★",       "결과 행 우측 버튼 — 즐겨찾기 추가/제거"),
            ("☆ 헤더 토글",  "헤더 ☆ 클릭 — 즐겨찾기 케이스만 보기 전환"),
            ("⎘ 이력 복사",  "이력 행 우측 버튼 — 원문 텍스트를 즉시 클립보드 복사"),
            ("전체 복사",    "상태바 버튼 — 전체 결과를 label: value 형태로 복사"),
            ("멀티라인",     "입력창 Enter — 여러 단어 일괄 변환 (↑↓ 선택 없을 때)"),
            ("⚙ 설정",      "단축키·즐겨찾기 순서·클립보드 자동 로드 설정"),
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
