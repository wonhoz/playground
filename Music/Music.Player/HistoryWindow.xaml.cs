using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Music.Player.Models;
using Music.Player.Services;

namespace Music.Player
{
    public partial class HistoryWindow : Window
    {
        private readonly Action<IEnumerable<string>> _addToPlaylist;

        public HistoryWindow(Action<IEnumerable<string>> addToPlaylist)
        {
            _addToPlaylist = addToPlaylist;
            InitializeComponent();
            LoadHistory();
        }

        private void LoadHistory()
        {
            RecentListView.ItemsSource = HistoryService.Instance.GetRecentTracks();
            MostPlayedListView.ItemsSource = HistoryService.Instance.GetMostPlayed();
            FavoritesListView.ItemsSource = HistoryService.Instance.GetFavorites();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
            => LoadHistory();

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView lv && lv.SelectedItem is PlayHistoryEntry entry)
            {
                if (File.Exists(entry.FilePath))
                {
                    _addToPlaylist(new[] { entry.FilePath });
                    StatusText.Text = $"추가됨: {entry.Title}";
                }
                else
                {
                    MessageBox.Show($"파일을 찾을 수 없습니다:\n{entry.FilePath}", "파일 없음",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var paths = GetSelectedPaths().ToList();
            if (paths.Count == 0)
            {
                StatusText.Text = "선택된 항목이 없습니다.";
                return;
            }
            _addToPlaylist(paths);
            StatusText.Text = $"{paths.Count}곡 추가됨";
        }

        private IEnumerable<string> GetSelectedPaths()
        {
            var lv = HistoryTabControl.SelectedIndex switch
            {
                0 => RecentListView,
                1 => MostPlayedListView,
                2 => FavoritesListView,
                _ => null
            };

            if (lv == null) return [];

            return lv.SelectedItems
                .Cast<PlayHistoryEntry>()
                .Select(e => e.FilePath)
                .Where(File.Exists);
        }
    }
}
