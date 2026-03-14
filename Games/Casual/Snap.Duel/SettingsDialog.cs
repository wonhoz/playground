using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnapDuel;

public class SettingsDialog : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public int Rounds { get; private set; }
    public bool TrapEnabled { get; private set; }

    private readonly Slider _roundSlider;
    private readonly CheckBox _trapCheck;
    private readonly TextBlock _roundLabel;

    public SettingsDialog(int currentRounds, bool trapEnabled)
    {
        Title = "설정";
        Width = 360; Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        Loaded += (_, _) =>
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
        };

        var border = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        var stack = new StackPanel { Margin = new Thickness(24) };

        // 라운드 설정
        stack.Children.Add(new TextBlock
        {
            Text = "라운드 수", FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        _roundLabel = new TextBlock
        {
            Text = $"{currentRounds} 라운드",
            Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0)),
            FontSize = 13, Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(_roundLabel);

        _roundSlider = new Slider
        {
            Minimum = 3, Maximum = 10, Value = currentRounds,
            TickFrequency = 1, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 0, 0, 20)
        };
        _roundSlider.ValueChanged += (_, e) => _roundLabel.Text = $"{(int)e.NewValue} 라운드";
        stack.Children.Add(_roundSlider);

        // 함정 설정
        _trapCheck = new CheckBox
        {
            Content = "함정 신호 활성화",
            IsChecked = trapEnabled,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 24)
        };
        stack.Children.Add(_trapCheck);

        // 확인/취소
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var btnOk = MakeButton("확인", true);
        var btnCancel = MakeButton("취소", false);
        btnOk.Margin = new Thickness(0, 0, 8, 0);
        btnOk.Click += (_, _) =>
        {
            Rounds = (int)_roundSlider.Value;
            TrapEnabled = _trapCheck.IsChecked == true;
            DialogResult = true;
        };
        btnCancel.Click += (_, _) => DialogResult = false;
        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);
        stack.Children.Add(btnPanel);

        Content = stack;
    }

    Button MakeButton(string text, bool accent)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(20, 8, 20, 8),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = accent
                ? new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0)
        };

        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(cp);
        template.VisualTree = factory;
        btn.Template = template;
        return btn;
    }
}
