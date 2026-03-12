using System.Windows;
using System.Windows.Controls;

namespace SysClean;

public partial class MainWindow : Window
{
    private Button _activeNavBtn;

    public MainWindow()
    {
        InitializeComponent();
        _activeNavBtn = BtnNavCleaner;
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString();

        // 스타일 전환
        _activeNavBtn.Style = (Style)Resources["NavButton"];
        btn.Style = (Style)Resources["NavButtonActive"];
        _activeNavBtn = btn;

        // 뷰 전환
        ViewCleaner.Visibility  = Visibility.Collapsed;
        ViewRegistry.Visibility = Visibility.Collapsed;
        ViewStartup.Visibility  = Visibility.Collapsed;
        ViewPrograms.Visibility = Visibility.Collapsed;

        switch (tag)
        {
            case "Cleaner":  ViewCleaner.Visibility  = Visibility.Visible; break;
            case "Registry": ViewRegistry.Visibility = Visibility.Visible; break;
            case "Startup":  ViewStartup.Visibility  = Visibility.Visible; break;
            case "Programs": ViewPrograms.Visibility = Visibility.Visible; break;
        }
    }
}
