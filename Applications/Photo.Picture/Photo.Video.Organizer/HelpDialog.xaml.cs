using System.Windows;
using System.Windows.Input;

namespace Photo.Video.Organizer
{
    public partial class HelpDialog : Window
    {
        public HelpDialog()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
