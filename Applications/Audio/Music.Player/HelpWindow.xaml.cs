using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Music.Player
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
                VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Escape or Key.F1)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
