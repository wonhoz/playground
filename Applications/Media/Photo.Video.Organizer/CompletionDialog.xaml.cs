using System.Windows;

namespace Photo.Video.Organizer
{
    public partial class CompletionDialog : Window
    {
        public bool OpenFolder { get; private set; }

        public CompletionDialog(int successCount)
        {
            InitializeComponent();
            MessageText.Text = $"{successCount}개 파일이 정리되었습니다.";
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder = false;
            DialogResult = false;
            Close();
        }
    }
}
