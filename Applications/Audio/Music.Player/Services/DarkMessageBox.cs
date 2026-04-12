using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Music.Player.Services;

internal static class DarkMessageBox
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private static readonly SolidColorBrush BgBrush = new(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly SolidColorBrush BorderColor = new(Color.FromRgb(0x40, 0x40, 0x40));
    private static readonly SolidColorBrush FgBrush = new(Color.FromRgb(0xEE, 0xEE, 0xEE));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0x9C, 0x27, 0xB0));
    private static readonly SolidColorBrush AccentDimBrush = new(Color.FromRgb(0x3A, 0x1A, 0x42));
    private static readonly SolidColorBrush BtnBgBrush = new(Color.FromRgb(0x38, 0x38, 0x38));
    private static readonly SolidColorBrush SecondaryBrush = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

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
            MessageBoxImage.Question => ("?", "#AB47BC"),
            MessageBoxImage.Warning  => ("\u26A0", "#FFA726"),
            MessageBoxImage.Error    => ("\u2715", "#EF5350"),
            _                        => ("\u2139", "#AB47BC"),
        };

        var root = new Border
        {
            Background = BgBrush,
            BorderBrush = BorderColor,
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
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconColor)!),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = AccentBrush,
        });
        stack.Children.Add(titlePanel);

        // Message
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = FgBrush,
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
                FontSize = 13, FontFamily = new FontFamily("Segoe UI"),
                Background = accent ? AccentDimBrush : BtnBgBrush,
                Foreground = accent ? AccentBrush : FgBrush,
                BorderThickness = new Thickness(0),
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
}
