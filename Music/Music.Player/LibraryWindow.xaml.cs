using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Music.Player.Models;
using Music.Player.Services;

namespace Music.Player
{
    public partial class LibraryWindow : Window
    {
        private readonly Action<IEnumerable<string>> _addToPlaylist;
        private List<TrackInfo> _allTracks = new();
        private string? _selectedFolder;
        private CancellationTokenSource? _scanCts;

        // 아티스트 목록 (전체 포함)
        private readonly ObservableCollection<string> _artists = new();

        public LibraryWindow(Action<IEnumerable<string>> addToPlaylist)
        {
            _addToPlaylist = addToPlaylist;
            InitializeComponent();
            ArtistListBox.ItemsSource = _artists;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
            Close();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "음악 파일이 있는 폴더를 선택하세요",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFolder = dialog.FolderName;
                FolderPathText.Text = _selectedFolder;
                FolderPathText.Foreground = System.Windows.Media.Brushes.LightGray;
                ScanButton.IsEnabled = true;
                StatusText.Text = "스캔 시작 버튼을 눌러주세요";
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolder)) return;

            // 이미 스캔 중이면 취소
            if (_scanCts != null)
            {
                _scanCts.Cancel();
                return;
            }

            _scanCts = new CancellationTokenSource();
            ScanButton.Content = "취소";
            ScanProgressPanel.Visibility = Visibility.Visible;
            ScanProgressBar.Value = 0;
            AddSelectedButton.IsEnabled = false;
            AddAllButton.IsEnabled = false;

            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                var pct = (double)p.current / p.total * 100;
                ScanProgressBar.Value = pct;
                ScanProgressText.Text = $"스캔 중... ({p.current}/{p.total})";
                ScanFileText.Text = p.fileName;
            });

            try
            {
                _allTracks = await LibraryScanner.ScanFolderAsync(
                    _selectedFolder, progress, _scanCts.Token);

                UpdateArtistList();
                ArtistListBox.SelectedIndex = 0; // "전체" 선택
                StatusText.Text = $"총 {_allTracks.Count}곡 스캔 완료";
                AddAllButton.IsEnabled = _allTracks.Count > 0;
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "스캔이 취소되었습니다";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"오류: {ex.Message}";
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;
                ScanButton.Content = "스캔 시작";
                ScanProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateArtistList()
        {
            _artists.Clear();
            _artists.Add($"전체 ({_allTracks.Count}곡)");

            var artistGroups = _allTracks
                .GroupBy(t => t.DisplayArtist)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key} ({g.Count()})");

            foreach (var artist in artistGroups)
                _artists.Add(artist);
        }

        private void ArtistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArtistListBox.SelectedIndex < 0) return;

            List<TrackInfo> filtered;

            if (ArtistListBox.SelectedIndex == 0)
            {
                // 전체
                filtered = _allTracks;
            }
            else
            {
                // 선택된 아티스트 이름 추출 (이름 (N곡) 형식에서)
                var selectedText = ArtistListBox.SelectedItem as string ?? "";
                var artistName = selectedText.Contains(" (")
                    ? selectedText[..selectedText.LastIndexOf(" (")]
                    : selectedText;

                filtered = _allTracks
                    .Where(t => t.DisplayArtist == artistName)
                    .ToList();
            }

            TrackListView.ItemsSource = filtered;
            AddSelectedButton.IsEnabled = false;
            StatusText.Text = $"{filtered.Count}곡";
        }

        private void TrackListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TrackListView.SelectedItem is TrackInfo track && File.Exists(track.FilePath))
            {
                _addToPlaylist(new[] { track.FilePath });
                StatusText.Text = $"추가됨: {track.DisplayTitle}";
            }
        }

        private void TrackListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddSelectedButton.IsEnabled = TrackListView.SelectedItems.Count > 0;
            if (TrackListView.SelectedItems.Count > 0)
                StatusText.Text = $"{TrackListView.SelectedItems.Count}곡 선택됨";
        }

        private void AddSelected_Click(object sender, RoutedEventArgs e)
        {
            var paths = TrackListView.SelectedItems
                .Cast<TrackInfo>()
                .Select(t => t.FilePath)
                .Where(File.Exists)
                .ToList();

            if (paths.Count == 0) return;
            _addToPlaylist(paths);
            StatusText.Text = $"{paths.Count}곡 추가됨";
        }

        private void AddAll_Click(object sender, RoutedEventArgs e)
        {
            var source = TrackListView.ItemsSource as List<TrackInfo> ?? _allTracks;
            var paths = source.Select(t => t.FilePath).Where(File.Exists).ToList();
            if (paths.Count == 0) return;
            _addToPlaylist(paths);
            StatusText.Text = $"{paths.Count}곡 모두 추가됨";
        }
    }
}
