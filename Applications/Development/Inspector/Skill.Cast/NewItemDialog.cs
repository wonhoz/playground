using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SkillCast;

/// <summary>이름 + 전역/프로젝트 선택 다이얼로그 (순수 코드, XAML 없음)</summary>
public class NewItemDialog : Window
{
    public string ItemName { get; private set; } = "";
    public string Location { get; private set; } = "global";

    private readonly TextBox _nameBox;
    private readonly RadioButton _rbGlobal;
    private readonly RadioButton _rbProject;

    public NewItemDialog(string title, string placeholder)
    {
        Title = title;
        Width = 420;
        Height = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        Helpers.DwmHelper.EnableDarkTitleBar(this);

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 이름 입력
        var label = new TextBlock
        {
            Text = placeholder,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(label, 0);
        root.Children.Add(label);

        _nameBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13
        };
        Grid.SetRow(_nameBox, 0);
        root.Children.Add(_nameBox);

        // 레이아웃 수정: label 위, textbox 아래
        root.RowDefinitions[0] = new RowDefinition { Height = GridLength.Auto };
        var namePanel = new StackPanel();
        namePanel.Children.Add(label);
        namePanel.Children.Add(_nameBox);
        Grid.SetRow(namePanel, 0);
        root.Children.Clear();
        root.Children.Add(namePanel);

        // 위치 선택
        var locPanel = new StackPanel { Orientation = Orientation.Horizontal };
        locPanel.Children.Add(new TextBlock
        {
            Text = "저장 위치:  ",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
        });
        _rbGlobal = new RadioButton
        {
            Content = "🌍 전역 (~/.claude)",
            IsChecked = true,
            Margin = new Thickness(0, 0, 16, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
        };
        _rbProject = new RadioButton
        {
            Content = "📂 프로젝트",
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
        };
        locPanel.Children.Add(_rbGlobal);
        locPanel.Children.Add(_rbProject);
        Grid.SetRow(locPanel, 2);
        root.Children.Add(locPanel);

        // 버튼
        var btnRow = new Grid();
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnCancel = CreateButton("취소", false, Color.FromRgb(0x38, 0x38, 0x38));
        var btnOk = CreateButton("만들기", true, Color.FromRgb(0x5B, 0x8A, 0xF0));
        Grid.SetColumn(btnCancel, 0);
        Grid.SetColumn(btnOk, 2);
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnOk);
        Grid.SetRow(btnRow, 4);
        root.Children.Add(btnRow);

        Content = root;
        _nameBox.Focus();
    }

    private Button CreateButton(string text, bool isDefault, Color bg)
    {
        var btn = new Button
        {
            Content = text,
            IsDefault = isDefault,
            Background = new SolidColorBrush(bg),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 8, 0, 8),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        factory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(cp);
        template.VisualTree = factory;
        btn.Template = template;
        btn.Click += isDefault ? OkButton_Click : CancelButton_Click;
        return btn;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("이름을 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            _nameBox.Focus();
            return;
        }
        ItemName = name;
        Location = _rbGlobal.IsChecked == true ? "global" : "project";
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
