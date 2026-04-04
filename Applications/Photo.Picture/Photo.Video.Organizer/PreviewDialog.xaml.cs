using System.Windows;
using System.Windows.Input;
using Photo.Video.Organizer.Services;

namespace Photo.Video.Organizer
{
    public partial class PreviewDialog : Window
    {
        public PreviewDialog(List<FileOrganizer.PreviewEntry> entries, string destinationRoot, bool moveFiles)
        {
            InitializeComponent();
            Populate(entries, destinationRoot, moveFiles);
        }

        private void Populate(List<FileOrganizer.PreviewEntry> entries, string destinationRoot, bool moveFiles)
        {
            var action = moveFiles ? "이동" : "복사";
            SubtitleText.Text = $"{entries.Count}개 파일 {action} 예정  ·  대상: {destinationRoot}";

            var grouped = entries
                .GroupBy(e => e.DestinationFolder)
                .OrderBy(g => g.Key)
                .Select(g => new FolderGroup
                {
                    FolderDisplay = g.Key.Replace(destinationRoot, "").TrimStart('\\', '/'),
                    CountDisplay = $"{g.Count()}개",
                    FileListDisplay = string.Join("  ·  ", g.Take(3).Select(e => e.SourceFileName))
                                     + (g.Count() > 3 ? $"  ..." : "")
                })
                .ToList();

            GroupList.ItemsSource = grouped;

            var folderCount = grouped.Count;
            SummaryText.Text = $"총 {entries.Count}개 파일을 {folderCount}개 폴더로 {action}합니다.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private class FolderGroup
        {
            public string FolderDisplay { get; set; } = "";
            public string CountDisplay { get; set; } = "";
            public string FileListDisplay { get; set; } = "";
        }
    }
}
