using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace ImgCast.Services;

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
            Width = 400,
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
            MessageBoxImage.Question => ("?", "#29B6F6"),
            MessageBoxImage.Warning  => ("\u26A0", "#FF9800"),
            MessageBoxImage.Error    => ("\u2715", "#FA6E6E"),
            _                        => ("\u2139", "#29B6F6"),
        };

        var root = new Border
        {
            Background = Res("BgBrush"),
            BorderBrush = Res("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Arrow,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16, ShadowDepth = 3, Opacity = 0.6, Color = Colors.Black
            }
        };

        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        // Title
        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        titlePanel.Children.Add(new TextBlock
        {
            Text = iconEmoji, FontSize = 14,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconColor)!),
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

        // Message
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 13,
            Foreground = Res("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        // Buttons
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        void AddBtn(string text, MessageBoxResult r, bool accent = false)
        {
            var btn = new WpfButton
            {
                Content = text, Width = 72, Height = 30,
                Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0),
                FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 12,
                Foreground = accent ? Res("TextBrush") : Res("SubTextBrush"),
                Background = accent ? Res("AccentDimBrush") : Res("SurfaceBrush"),
                BorderBrush = accent ? Res("AccentBrush") : Res("BorderBrush"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
            };
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
