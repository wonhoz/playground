using System.Windows;
using System.Windows.Input;

namespace SysClean.Views;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape || e.Key == Key.F1)
        {
            Close();
            e.Handled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
