using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfColor = System.Windows.Media.Color;

namespace SysClean.Views;

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
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
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

        var (iconEmoji, iconColor) = image switch
        {
            MessageBoxImage.Question => ("?", "#4FC3F7"),
            MessageBoxImage.Warning  => ("\u26A0", "#FFA726"),
            MessageBoxImage.Error    => ("\u2715", "#EF5350"),
            _                        => ("\u2139", "#66BB6A"),
        };

        var root = new Border
        {
            Background = Res("BrBg1"),
            BorderBrush = Res("BrBg4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Arrow,
            Effect = new DropShadowEffect
            {
                BlurRadius = 16, ShadowDepth = 3, Opacity = 0.6, Color = Colors.Black
            }
        };

        var stack = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        // Title
        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 14)
        };
        titlePanel.Children.Add(new TextBlock
        {
            Text = iconEmoji, FontSize = 15,
            Foreground = new SolidColorBrush((WpfColor)ColorConverter.ConvertFromString(iconColor)!),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = Res("BrAccent"),
        });
        stack.Children.Add(titlePanel);

        // Message
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = Res("BrFg"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 18),
        });

        // Buttons
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        void AddBtn(string text, MessageBoxResult r, bool accent = false)
        {
            var btn = new Button
            {
                Content = text, MinWidth = 72, Height = 30,
                Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(12, 0, 12, 0),
                FontSize = 13,
            };
            if (accent)
            {
                btn.Background = Res("BrAccentDim");
                btn.Foreground = Res("BrAccent");
                btn.BorderBrush = Res("BrAccent");
            }
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

        win.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                win.DialogResult = false;
            else if (e.Key == Key.Enter)
            {
                result = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel
                    ? MessageBoxResult.Yes : MessageBoxResult.OK;
                win.DialogResult = true;
            }
        };

        root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) win.DragMove();
        };

        win.ShowDialog();
        return result;
    }

    private static SolidColorBrush Res(string key)
        => (SolidColorBrush)Application.Current.Resources[key];
}
