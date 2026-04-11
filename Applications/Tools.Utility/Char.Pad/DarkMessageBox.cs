using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfBrushes = System.Windows.Media.Brushes;

namespace CharPad;

/// <summary>
/// 앱 다크 테마에 맞춘 MessageBox 대체.
/// System.Windows.MessageBox는 시스템 라이트 테마를 사용하여 다크 앱에서 이질적임.
/// </summary>
internal static class DarkMessageBox
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public static MessageBoxResult Show(
        string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
    {
        // 기본 결과: 닫기/Esc 시 반환값
        var result = buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None
        };

        var win = new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WpfBrushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        win.Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(win).Handle;
            int v = 1;
            DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
        };

        // 아이콘 이모지 + 색상
        var (iconEmoji, iconColor) = image switch
        {
            MessageBoxImage.Question    => ("?", "#0288D1"),
            MessageBoxImage.Warning     => ("\u26A0", "#FF9800"),
            MessageBoxImage.Error       => ("\u2715", "#E53935"),
            _                           => ("\u2139", "#1DE9B6"),
        };

        // ── 루트 Border ────────────────────────────────────
        var root = new Border
        {
            Background = Res("BgBrush"),
            BorderBrush = Res("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = System.Windows.Input.Cursors.Arrow,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16, ShadowDepth = 3, Opacity = 0.6, Color = WpfColors.Black
            }
        };

        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        // ── 타이틀 ─────────────────────────────────────────
        var titlePanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        titlePanel.Children.Add(new TextBlock
        {
            Text = iconEmoji, FontSize = 14,
            Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(iconColor)!),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = Res("AccentBrush"),
        });
        stack.Children.Add(titlePanel);

        // ── 메시지 ─────────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 13,
            Foreground = Res("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        // ── 버튼 ───────────────────────────────────────────
        var btnPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        void AddBtn(string text, MessageBoxResult r, bool accent = false)
        {
            var btn = new WpfButton
            {
                Content = text, Width = 72, Height = 30,
                Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0),
            };
            if (accent) btn.Background = Res("AccentBrush");
            btn.Click += (_, _) => { result = r; win.DialogResult = true; };
            btnPanel.Children.Add(btn);
        }

        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddBtn("확인", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.OKCancel:
                AddBtn("취소", MessageBoxResult.Cancel);
                AddBtn("확인", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.YesNo:
                AddBtn("아니오", MessageBoxResult.No);
                AddBtn("예", MessageBoxResult.Yes, true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddBtn("취소", MessageBoxResult.Cancel);
                AddBtn("아니오", MessageBoxResult.No);
                AddBtn("예", MessageBoxResult.Yes, true);
                break;
        }
        stack.Children.Add(btnPanel);

        root.Child = stack;
        win.Content = root;

        // ── 키보드 ─────────────────────────────────────────
        win.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                // result 기본값 그대로 (Cancel 또는 No)
                win.DialogResult = false;
            }
            else if (e.Key == Key.Enter)
            {
                result = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel
                    ? MessageBoxResult.Yes : MessageBoxResult.OK;
                win.DialogResult = true;
            }
        };

        // 드래그 이동
        root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) win.DragMove();
        };

        win.ShowDialog();
        return result;
    }

    private static SolidColorBrush Res(string key)
        => (SolidColorBrush)System.Windows.Application.Current.Resources[key];
}
