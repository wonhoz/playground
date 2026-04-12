using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfCursors = System.Windows.Input.Cursors;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColors = System.Windows.Media.Colors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace Photo.Video.Organizer;

internal static class DarkMessageBox
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private static SolidColorBrush Brush(string hex) =>
        new((WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

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

        var (iconText, iconColor) = image switch
        {
            MessageBoxImage.Question => ("?", "#4FC3F7"),
            MessageBoxImage.Warning => ("\u26A0", "#FFC107"),
            MessageBoxImage.Error => ("\u2715", "#EF5350"),
            _ => ("\u2139", "#66BB6A"),
        };

        var root = new Border
        {
            Background = Brush("#2D2D2D"),
            BorderBrush = Brush("#404040"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16, ShadowDepth = 3, Opacity = 0.6, Color = WpfColors.Black
            }
        };

        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        // Title
        var titlePanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        titlePanel.Children.Add(new TextBlock
        {
            Text = iconText, FontSize = 14,
            Foreground = Brush(iconColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#4FC3F7"),
        });
        stack.Children.Add(titlePanel);

        // Message
        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = new WpfFontFamily("Segoe UI"), FontSize = 13,
            Foreground = Brush("#EEEEEE"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        // Buttons
        var btnPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        void AddBtn(string text, MessageBoxResult r, bool accent = false)
        {
            var btn = new WpfButton
            {
                Content = text,
                Cursor = WpfCursors.Hand,
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12,
                Foreground = accent ? Brush("#1E1E1E") : Brush("#9E9E9E"),
                FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal,
            };

            var bgBrush = accent ? Brush("#4FC3F7") : Brush("Transparent");
            var hoverBg = accent ? Brush("#29B6F6") : Brush("#383838");
            var borderBr = accent ? Brush("#4FC3F7") : Brush("#505050");

            var template = new ControlTemplate(typeof(WpfButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "bd");
            borderFactory.SetValue(Border.BackgroundProperty, bgBrush);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
            if (!accent)
            {
                borderFactory.SetValue(Border.BorderBrushProperty, borderBr);
                borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            }
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cpFactory);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "bd"));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
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
