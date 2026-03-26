using System.Windows;
using System.Windows.Controls;

namespace MarkView;

public partial class ShortcutRow : UserControl
{
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.Register(nameof(Key), typeof(string), typeof(ShortcutRow), new PropertyMetadata(""));

    public static readonly DependencyProperty DescProperty =
        DependencyProperty.Register(nameof(Desc), typeof(string), typeof(ShortcutRow), new PropertyMetadata(""));

    public string Key
    {
        get => (string)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public string Desc
    {
        get => (string)GetValue(DescProperty);
        set => SetValue(DescProperty, value);
    }

    public ShortcutRow()
    {
        InitializeComponent();
    }
}
