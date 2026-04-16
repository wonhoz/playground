using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Music.Player.Models;
using Music.Player.Services;
using NAudio.Wave;
using Color = System.Windows.Media.Color;

namespace Music.Player
{
    public partial class MainWindow : Window
    {
        private readonly AudioPlayer _player = new();
        private readonly ObservableCollection<TrackInfo> _playlist = new();
        private readonly Random _random = new();
        private readonly WindowSnapService _snap;

        private int _currentIndex = -1;
        private bool _isDraggingSlider;
        private bool _isShuffleEnabled;
        private bool _isRepeatEnabled;
        private Point _dragStartPoint;
        private bool _isDraggingPlaylistItem;
        private float _lastVolume = 1f;
        private bool _isMuted;

        private static readonly string[] SupportedExtensions = Services.LibraryScanner.SupportedExtensions;

        // 가사 관련
        private readonly ObservableCollection<LrcLine> _lyricsLines = new();
        private int _currentLyricsIndex = -1;

        // 플레이리스트 필터 뷰
        private ICollectionView? _playlistView;

        public MainWindow()
        {
            InitializeComponent();
            _snap = new WindowSnapService(this);

            // 검색 필터를 적용한 CollectionView로 ListBox 연결
            _playlistView = CollectionViewSource.GetDefaultView(_playlist);
            _playlistView.Filter = PlaylistFilter;
            PlaylistBox.ItemsSource = _playlistView;

            LyricsBox.ItemsSource = _lyricsLines;
            _player.PositionChanged += Player_PositionChanged;
            _player.PlaybackStopped += Player_PlaybackStopped;

            // Enable drag & drop for main window
            AllowDrop = true;
            Drop += MainWindow_Drop;
            DragEnter += MainWindow_DragEnter;

            // Restore saved playlist state after window is loaded; 1× 기본 속도 버튼 활성화
            Loaded += async (s, e) =>
            {
                UpdateSpeedButtons(1.0f);
                await RestorePlaylistStateAsync();
            };
        }

        #region Title Bar

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Toggle playlist on double click
                PlaylistToggle.IsChecked = !PlaylistToggle.IsChecked;
                TogglePlaylistPanel();
            }
            else
            {
                DragMove();
            }
        }

        private void ContentArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 빈 공간 드래그로 창 이동
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _player.Dispose();
            Close();
        }

        #endregion

        #region Playback Controls

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0 && _playlist.Count > 0)
            {
                PlayTrack(0);
                return;
            }

            if (_player.PlaybackState == PlaybackState.Playing)
            {
                _player.Pause();
                UpdatePlayPauseIcon(false);
            }
            else
            {
                _player.Play();
                UpdatePlayPauseIcon(true);
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0) return;

            // If more than 3 seconds into the track, restart it
            if (_player.CurrentPosition.TotalSeconds > 3)
            {
                _player.Seek(TimeSpan.Zero);
                return;
            }

            int prevIndex = _currentIndex - 1;
            if (prevIndex < 0)
                prevIndex = _playlist.Count - 1;

            PlayTrack(prevIndex);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNext();
        }

        private void PlayNext()
        {
            if (_playlist.Count == 0) return;

            int nextIndex;
            if (_isShuffleEnabled)
            {
                if (_playlist.Count == 1)
                    nextIndex = 0;
                else
                {
                    do { nextIndex = _random.Next(_playlist.Count); }
                    while (nextIndex == _currentIndex);
                }
            }
            else
            {
                nextIndex = _currentIndex + 1;
                if (nextIndex >= _playlist.Count)
                {
                    if (_isRepeatEnabled)
                        nextIndex = 0;
                    else
                    {
                        _player.Stop();
                        UpdatePlayPauseIcon(false);
                        return;
                    }
                }
            }

            PlayTrack(nextIndex);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            _isShuffleEnabled = ShuffleButton.IsChecked == true;
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _isRepeatEnabled = RepeatButton.IsChecked == true;
        }

        private void PlayTrack(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            // 이전 재생 트랙 인디케이터 해제
            if (_currentIndex >= 0 && _currentIndex < _playlist.Count)
                _playlist[_currentIndex].IsPlaying = false;

            _currentIndex = index;
            var track = _playlist[index];
            track.IsPlaying = true;

            try
            {
                HistoryService.Instance.RecordPlay(track);
                LoadLyrics(track.FilePath);

                _player.Load(track.FilePath);
                _player.Play();

                UpdateTrackDisplay(track);
                UpdatePlayPauseIcon(true);
                PlaylistBox.SelectedIndex = index;
                PlaylistBox.ScrollIntoView(PlaylistBox.SelectedItem);

                ProgressSlider.Maximum = track.Duration.TotalSeconds;
                TotalTimeText.Text = track.DurationText;
            }
            catch (Exception ex)
            {
                TitleText.Text = $"재생 오류: {track.DisplayTitle}";
                ArtistText.Text = ex.Message;
                UpdatePlayPauseIcon(false);
            }
        }

        private void UpdateTrackDisplay(TrackInfo track)
        {
            TitleText.Text = track.DisplayTitle;
            ArtistText.Text = track.DisplayArtist;

            if (track.AlbumArt != null)
            {
                AlbumArtImage.Source = track.AlbumArt;
                AlbumArtImage.Visibility = Visibility.Visible;
                DefaultIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                AlbumArtImage.Visibility = Visibility.Collapsed;
                DefaultIcon.Visibility = Visibility.Visible;
            }
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            var canvas = (Canvas)PlayIcon.Child;
            canvas.Children.Clear();

            if (isPlaying)
            {
                // Pause icon (two bars)
                canvas.Children.Add(new Rectangle
                {
                    Width = 4,
                    Height = 14,
                    Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                    RadiusX = 1,
                    RadiusY = 1
                });
                Canvas.SetLeft(canvas.Children[0], 5);
                Canvas.SetTop(canvas.Children[0], 5);

                canvas.Children.Add(new Rectangle
                {
                    Width = 4,
                    Height = 14,
                    Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                    RadiusX = 1,
                    RadiusY = 1
                });
                Canvas.SetLeft(canvas.Children[1], 15);
                Canvas.SetTop(canvas.Children[1], 5);
            }
            else
            {
                // Play icon (triangle)
                var path = new System.Windows.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                    Data = Geometry.Parse("M8 5v14l11-7z")
                };
                canvas.Children.Add(path);
            }
        }

        #endregion

        #region Progress Slider

        private void Player_PositionChanged(object? sender, TimeSpan position)
        {
            Dispatcher.Invoke(() =>
            {
                // Thumb 마우스 캡처 이슈 자가복구: 실제로 마우스 버튼이 떼어진 상태면 플래그 해제
                if (_isDraggingSlider && Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    _isDraggingSlider = false;
                    _player.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
                }
                if (!_isDraggingSlider)
                {
                    ProgressSlider.Value = position.TotalSeconds;
                    CurrentTimeText.Text = position.ToString(@"m\:ss");
                }
                UpdateLyricsDisplay(position);
            });
        }

        private void Player_PlaybackStopped(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Check if we reached the end of the track
                if (_player.CurrentPosition >= _player.TotalDuration - TimeSpan.FromMilliseconds(500))
                {
                    PlayNext();
                }
            });
        }

        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;

            // 클릭한 위치로 바로 이동 (트랙 클릭 시)
            if (sender is Slider slider && ProgressSlider.Maximum > 0)
            {
                Point clickPos = e.GetPosition(slider);
                double ratio = clickPos.X / slider.ActualWidth;
                double newValue = ratio * slider.Maximum;
                newValue = Math.Max(0, Math.Min(slider.Maximum, newValue));
                slider.Value = newValue;
                CurrentTimeText.Text = TimeSpan.FromSeconds(newValue).ToString(@"m\:ss");
            }
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSlider)
            {
                _isDraggingSlider = false;
                _player.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
            {
                CurrentTimeText.Text = TimeSpan.FromSeconds(e.NewValue).ToString(@"m\:ss");
            }
        }

        private void ProgressSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 더블클릭이 다른 동작을 트리거하지 않도록 방지
            e.Handled = true;
        }

        #endregion

        #region Playlist

        private void PlaylistToggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePlaylistPanel();
        }

        private const double PlaylistPanelWidth = 220;

        private void TogglePlaylistPanel()
        {
            if (PlaylistToggle.IsChecked == true)
            {
                PlaylistPanel.Visibility = Visibility.Visible;
                PlaylistColumn.Width = new GridLength(PlaylistPanelWidth);
                Width += PlaylistPanelWidth;
            }
            else
            {
                PlaylistPanel.Visibility = Visibility.Collapsed;
                PlaylistColumn.Width = new GridLength(0);
                Width = Math.Max(MinWidth, Width - PlaylistPanelWidth);
            }
        }

        private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Add Music Files",
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a;*.wma;*.aac;*.ogg|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                await AddFilesAsync(dialog.FileNames);
            }
        }

        private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _playlist) t.IsPlaying = false;
            _player.Stop();
            _playlist.Clear();
            _currentIndex = -1;
            _lyricsLines.Clear();
            _currentLyricsIndex = -1;
            UpdatePlayPauseIcon(false);
            TitleText.Text = "No track selected";
            ArtistText.Text = "";
            AlbumArtImage.Visibility = Visibility.Collapsed;
            DefaultIcon.Visibility = Visibility.Visible;
            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = "0:00";
            ProgressSlider.Value = 0;
        }

        private void SavePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0)
            {
                Services.DarkMessageBox.Show("플레이리스트가 비어있습니다.", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Playlist",
                Filter = "JSON Files|*.json|All Files|*.*",
                DefaultExt = ".json",
                FileName = "playlist"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var playlistData = new { files = _playlist.Select(t => t.FilePath).ToList() };
                    var json = System.Text.Json.JsonSerializer.Serialize(playlistData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                }
                catch (Exception ex)
                {
                    Services.DarkMessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Playlist",
                Filter = "JSON Files|*.json|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var playlistData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

                    if (playlistData.TryGetProperty("files", out var filesElement))
                    {
                        var files = filesElement.EnumerateArray()
                            .Select(f => f.GetString())
                            .Where(f => !string.IsNullOrEmpty(f) && File.Exists(f))
                            .ToArray();

                        if (files.Length > 0)
                        {
                            LoadFilesFromArgs(files!);
                        }
                        else
                        {
                            Services.DarkMessageBox.Show("유효한 파일이 없습니다.", "불러오기", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Services.DarkMessageBox.Show($"불러오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PlaylistBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                PlayTrack(PlaylistBox.SelectedIndex);
            }
        }

        private async Task AddFilesAsync(IEnumerable<string> paths)
        {
            // ── UI 차단 방지: 파일 열거·메타데이터 읽기를 배경 스레드에서 수행 ──
            var prevTitle  = TitleText.Text;
            var prevArtist = ArtistText.Text;
            TitleText.Text  = "파일 추가 중...";
            ArtistText.Text = "";

            // 이미 추가된 경로를 미리 수집 (UI 스레드)
            var existingPaths = _playlist
                .Select(t => t.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newTracks = new List<TrackInfo>();

            await Task.Run(() =>
            {
                var allPaths = new List<string>();
                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(f => SupportedExtensions.Contains(
                                System.IO.Path.GetExtension(f).ToLower()));
                        allPaths.AddRange(files);
                    }
                    else if (File.Exists(path) &&
                             SupportedExtensions.Contains(
                                 System.IO.Path.GetExtension(path).ToLower()))
                    {
                        allPaths.Add(path);
                    }
                }

                foreach (var file in allPaths)
                {
                    var fullPath = System.IO.Path.GetFullPath(file);
                    if (existingPaths.Contains(fullPath)) continue;

                    // 진행 파일명 표시 (백그라운드 우선순위)
                    Dispatcher.InvokeAsync(
                        () => ArtistText.Text = System.IO.Path.GetFileName(fullPath),
                        System.Windows.Threading.DispatcherPriority.Background);

                    newTracks.Add(TrackInfo.FromFile(fullPath));
                    existingPaths.Add(fullPath); // 같은 경로 중복 방지
                }
            });

            // UI 스레드: ObservableCollection 갱신
            foreach (var track in newTracks)
            {
                HistoryService.Instance.LoadFavoriteStatus(track);
                _playlist.Add(track);
            }

            // 표시 복원
            if (_currentIndex >= 0 && _currentIndex < _playlist.Count)
                UpdateTrackDisplay(_playlist[_currentIndex]);
            else
            {
                TitleText.Text  = prevTitle;
                ArtistText.Text = prevArtist;
            }

            // 첫 트랙 자동 재생
            if (_currentIndex < 0 && _playlist.Count > 0)
                PlayTrack(0);
        }

        private void AddTrack(string filePath)
        {
            var fullPath = System.IO.Path.GetFullPath(filePath);
            if (_playlist.Any(t => string.Equals(t.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))) return;

            var track = TrackInfo.FromFile(fullPath);
            HistoryService.Instance.LoadFavoriteStatus(track);
            _playlist.Add(track);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.DataContext is TrackInfo track)
            {
                HistoryService.Instance.ToggleFavorite(track);
                button.IsChecked = track.IsFavorite;
            }
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            Func<IEnumerable<string>, Task> callback = async paths =>
            {
                await AddFilesAsync(paths);
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }
            };
            var historyWindow = new HistoryWindow(callback) { Owner = this };
            historyWindow.Show();
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            Func<IEnumerable<string>, Task> callback = async paths =>
            {
                await AddFilesAsync(paths);
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }
            };
            var libraryWindow = new LibraryWindow(callback) { Owner = this };
            libraryWindow.Show();
        }

        #endregion

        #region Lyrics

        private void LyricsToggle_Click(object sender, RoutedEventArgs e)
        {
            LyricsPanel.Visibility = LyricsToggle.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void LoadLyrics(string audioFilePath)
        {
            _lyricsLines.Clear();
            _currentLyricsIndex = -1;

            var lrcPath = LrcParser.FindLrcFile(audioFilePath);
            if (lrcPath == null)
            {
                LyricsNoFileText.Visibility = Visibility.Visible;
                LyricsBox.Visibility = Visibility.Collapsed;
                return;
            }

            var lines = LrcParser.ParseFile(lrcPath);
            foreach (var line in lines)
                _lyricsLines.Add(line);

            LyricsNoFileText.Visibility = Visibility.Collapsed;
            LyricsBox.Visibility = Visibility.Visible;
        }

        private void UpdateLyricsDisplay(TimeSpan position)
        {
            if (_lyricsLines.Count == 0 || LyricsPanel.Visibility != Visibility.Visible) return;

            // 현재 위치에 해당하는 마지막 가사 줄 찾기
            int newIndex = -1;
            for (int i = _lyricsLines.Count - 1; i >= 0; i--)
            {
                if (_lyricsLines[i].Time <= position)
                {
                    newIndex = i;
                    break;
                }
            }

            if (newIndex == _currentLyricsIndex) return;

            // 이전 활성 줄 비활성화
            if (_currentLyricsIndex >= 0 && _currentLyricsIndex < _lyricsLines.Count)
                _lyricsLines[_currentLyricsIndex].IsActive = false;

            _currentLyricsIndex = newIndex;

            // 새 활성 줄 설정 및 스크롤
            if (_currentLyricsIndex >= 0 && _currentLyricsIndex < _lyricsLines.Count)
            {
                _lyricsLines[_currentLyricsIndex].IsActive = true;
                LyricsBox.ScrollIntoView(_lyricsLines[_currentLyricsIndex]);
            }
        }

        #endregion

        #region Playlist (Remove)

        private void RemoveTrackButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is TrackInfo track)
            {
                int index = _playlist.IndexOf(track);
                if (index < 0) return;

                // 현재 재생 중인 트랙을 삭제하는 경우
                if (index == _currentIndex)
                {
                    _player.Stop();
                    _currentIndex = -1;
                    UpdatePlayPauseIcon(false);

                    // 다음 트랙이 있으면 다음 트랙 재생
                    if (_playlist.Count > 1)
                    {
                        _playlist.Remove(track);
                        if (index >= _playlist.Count)
                            index = _playlist.Count - 1;
                        PlayTrack(index);
                    }
                    else
                    {
                        _playlist.Remove(track);
                        TitleText.Text = "No track selected";
                        ArtistText.Text = "";
                        AlbumArtImage.Visibility = Visibility.Collapsed;
                        DefaultIcon.Visibility = Visibility.Visible;
                        CurrentTimeText.Text = "0:00";
                        TotalTimeText.Text = "0:00";
                        ProgressSlider.Value = 0;
                    }
                }
                else
                {
                    _playlist.Remove(track);
                    // 삭제된 항목이 현재 재생 중인 항목 앞에 있으면 인덱스 조정
                    if (index < _currentIndex)
                        _currentIndex--;
                }
            }
        }

        #endregion

        #region Drag & Drop

        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;
                await AddFilesAsync(files);

                // Show playlist if hidden
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }
            }
        }

        private void PlaylistBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void PlaylistBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;
                await AddFilesAsync(files);
            }
            e.Handled = true;
        }

        #endregion

        #region Playlist Item Drag Reorder

        private void PlaylistItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDraggingPlaylistItem = false;
        }

        private void PlaylistItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDraggingPlaylistItem)
                return;

            Point position = e.GetPosition(null);
            Vector diff = _dragStartPoint - position;

            // 충분히 드래그했는지 확인
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is ListBoxItem listBoxItem && listBoxItem.DataContext is TrackInfo track)
                {
                    _isDraggingPlaylistItem = true;
                    DragDrop.DoDragDrop(listBoxItem, track, DragDropEffects.Move);
                    _isDraggingPlaylistItem = false;
                }
            }
        }

        private void PlaylistItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TrackInfo)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void PlaylistItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TrackInfo)) && sender is ListBoxItem targetItem)
            {
                var draggedTrack = (TrackInfo)e.Data.GetData(typeof(TrackInfo));
                var targetTrack = targetItem.DataContext as TrackInfo;

                if (draggedTrack == null || targetTrack == null || draggedTrack == targetTrack)
                    return;

                int oldIndex = _playlist.IndexOf(draggedTrack);
                int newIndex = _playlist.IndexOf(targetTrack);

                if (oldIndex < 0 || newIndex < 0)
                    return;

                // 현재 재생 중인 트랙 인덱스 조정
                if (_currentIndex == oldIndex)
                {
                    _currentIndex = newIndex;
                }
                else if (oldIndex < _currentIndex && newIndex >= _currentIndex)
                {
                    _currentIndex--;
                }
                else if (oldIndex > _currentIndex && newIndex <= _currentIndex)
                {
                    _currentIndex++;
                }

                // 항목 이동
                _playlist.Move(oldIndex, newIndex);
                PlaylistBox.SelectedIndex = newIndex;

                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    await AddFilesAsync(files);
                }
                e.Handled = true;
            }
        }

        #endregion

        #region Keyboard Shortcuts

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    PlayPauseButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case Key.Left:
                    // 5초 뒤로
                    if (_player.PlaybackState != PlaybackState.Stopped)
                    {
                        var newPos = _player.CurrentPosition - TimeSpan.FromSeconds(5);
                        if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
                        _player.Seek(newPos);
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                    // 5초 앞으로
                    if (_player.PlaybackState != PlaybackState.Stopped)
                    {
                        var newPos = _player.CurrentPosition + TimeSpan.FromSeconds(5);
                        if (newPos > _player.TotalDuration) newPos = _player.TotalDuration;
                        _player.Seek(newPos);
                    }
                    e.Handled = true;
                    break;

                case Key.Home:
                    // 이전 곡
                    PreviousButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case Key.End:
                    // 다음 곡
                    NextButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;

                case Key.Delete:
                    // 선택된 항목 삭제
                    if (PlaylistBox.SelectedItem is TrackInfo selectedTrack)
                    {
                        int index = _playlist.IndexOf(selectedTrack);
                        if (index >= 0)
                        {
                            var fakeButton = new Button { DataContext = selectedTrack };
                            RemoveTrackButton_Click(fakeButton, new RoutedEventArgs());
                        }
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+Up: 선택된 항목을 위로 이동
                        if (PlaylistBox.SelectedItem is TrackInfo trackUp)
                        {
                            int index = _playlist.IndexOf(trackUp);
                            if (index > 0)
                                MovePlaylistItem(index, index - 1);
                        }
                    }
                    else
                    {
                        // Up: 볼륨 올리기
                        VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 5);
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // Ctrl+Down: 선택된 항목을 아래로 이동
                        if (PlaylistBox.SelectedItem is TrackInfo trackDown)
                        {
                            int index = _playlist.IndexOf(trackDown);
                            if (index >= 0 && index < _playlist.Count - 1)
                                MovePlaylistItem(index, index + 1);
                        }
                    }
                    else
                    {
                        // Down: 볼륨 내리기
                        VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
                    }
                    e.Handled = true;
                    break;

                case Key.M:
                    // 음소거 토글
                    ToggleMute();
                    e.Handled = true;
                    break;

                case Key.F1:
                    // 도움말
                    ShowHelpWindow();
                    e.Handled = true;
                    break;
            }
        }

        private void MovePlaylistItem(int oldIndex, int newIndex)
        {
            // 현재 재생 중인 트랙 인덱스 조정
            if (_currentIndex == oldIndex)
            {
                _currentIndex = newIndex;
            }
            else if (oldIndex < _currentIndex && newIndex >= _currentIndex)
            {
                _currentIndex--;
            }
            else if (oldIndex > _currentIndex && newIndex <= _currentIndex)
            {
                _currentIndex++;
            }

            _playlist.Move(oldIndex, newIndex);
            PlaylistBox.SelectedIndex = newIndex;
        }

        #endregion

        #region Volume

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            float vol = (float)(e.NewValue / 100.0);
            _player.Volume = vol;
            _isMuted = vol == 0;
            if (vol > 0) _lastVolume = vol;
            UpdateVolumeIcon(vol);
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMute();
        }

        private void ToggleMute()
        {
            if (_isMuted)
            {
                _isMuted = false;
                VolumeSlider.Value = _lastVolume * 100;
            }
            else
            {
                _lastVolume = (float)(VolumeSlider.Value / 100.0);
                if (_lastVolume < 0.05f) _lastVolume = 0.5f;
                _isMuted = true;
                VolumeSlider.Value = 0;
            }
        }

        private void UpdateVolumeIcon(float volume)
        {
            var canvas = (Canvas)VolumeIcon.Child;
            canvas.Children.Clear();

            string data;
            if (volume <= 0)
                data = "M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z";
            else if (volume < 0.5f)
                data = "M7 9v6h4l5 5V4l-5 5H7z";
            else
                data = "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z";

            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Fill = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                Data = Geometry.Parse(data)
            });
        }

        #endregion

        #region Help

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHelpWindow();
        }

        private void ShowHelpWindow()
        {
            var helpWindow = new HelpWindow { Owner = this };
            helpWindow.ShowDialog();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SavePlaylistState();
            _player.Dispose();
            _snap.Dispose();
            base.OnClosed(e);
        }

        #region Command Line Arguments

        public async void LoadFilesFromArgs(string[] args)
        {
            if (args.Length == 0) return;

            // 기존 플레이리스트 클리어
            foreach (var t in _playlist) t.IsPlaying = false;
            _player.Stop();
            _playlist.Clear();
            _currentIndex = -1;
            UpdatePlayPauseIcon(false);

            // AddFilesAsync 로 비동기 처리 (UI 스레드 블로킹 방지)
            await AddFilesAsync(args);

            // 플레이리스트 패널 표시 (AddFilesAsync는 패널을 열지 않음)
            if (_playlist.Count > 0 && PlaylistToggle.IsChecked != true)
            {
                PlaylistToggle.IsChecked = true;
                TogglePlaylistPanel();
            }
        }

        #endregion

        #region Playlist State Persistence

        private void SavePlaylistState()
        {
            var state = new PlaylistState
            {
                FilePaths = _playlist.Select(t => t.FilePath).ToList(),
                CurrentTrackIndex = _currentIndex,
                CurrentPositionSeconds = _player.CurrentPosition.TotalSeconds,
                IsShuffleEnabled = _isShuffleEnabled,
                IsRepeatEnabled = _isRepeatEnabled,
                VolumePercent = (int)VolumeSlider.Value
            };

            PlaylistService.SaveState(state);
        }

        private async Task RestorePlaylistStateAsync()
        {
            var state = PlaylistService.LoadState();
            if (state == null || state.FilePaths.Count == 0)
                return;

            // Restore shuffle, repeat, volume settings (UI thread)
            _isShuffleEnabled = state.IsShuffleEnabled;
            _isRepeatEnabled  = state.IsRepeatEnabled;
            ShuffleButton.IsChecked = _isShuffleEnabled;
            RepeatButton.IsChecked  = _isRepeatEnabled;
            VolumeSlider.Value = state.VolumePercent;

            // ── UI 차단 방지: TrackInfo.FromFile()을 배경 스레드에서 수행 ──
            TitleText.Text  = "재생목록 복원 중...";
            ArtistText.Text = "";

            var tracks = new List<TrackInfo>();
            await Task.Run(() =>
            {
                foreach (var filePath in state.FilePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    Dispatcher.InvokeAsync(
                        () => ArtistText.Text = System.IO.Path.GetFileName(filePath),
                        System.Windows.Threading.DispatcherPriority.Background);

                    tracks.Add(TrackInfo.FromFile(filePath));
                }
            });

            // UI 스레드: ObservableCollection 갱신 + 즐겨찾기 상태 복원
            foreach (var track in tracks)
            {
                HistoryService.Instance.LoadFavoriteStatus(track);
                _playlist.Add(track);
            }

            // Show playlist panel if there are tracks
            if (_playlist.Count > 0)
            {
                PlaylistToggle.IsChecked = true;
                TogglePlaylistPanel();
            }

            // Restore current track and position
            if (state.CurrentTrackIndex >= 0 && state.CurrentTrackIndex < _playlist.Count)
            {
                _currentIndex = state.CurrentTrackIndex;
                var track = _playlist[_currentIndex];

                // Load track and seek to saved position, then auto-play
                // (RecordPlay 미호출 — 복원은 실제 재생이 아니므로 play count 증가 없음)
                LoadLyrics(track.FilePath);

                _player.Load(track.FilePath);
                // 볼륨을 Load() 이후에 명시적으로 재적용 (Load 이전엔 _audioFile이 null)
                _player.Volume = (float)(VolumeSlider.Value / 100.0);
                if (state.CurrentPositionSeconds > 0 && state.CurrentPositionSeconds < track.Duration.TotalSeconds)
                {
                    _player.Seek(TimeSpan.FromSeconds(state.CurrentPositionSeconds));
                }

                // Update display and start playback
                UpdateTrackDisplay(track);
                UpdatePlayPauseIcon(true);
                PlaylistBox.SelectedIndex = _currentIndex;
                PlaylistBox.ScrollIntoView(PlaylistBox.SelectedItem);

                ProgressSlider.Maximum = track.Duration.TotalSeconds;
                ProgressSlider.Value   = state.CurrentPositionSeconds;
                TotalTimeText.Text     = track.DurationText;
                CurrentTimeText.Text   = TimeSpan.FromSeconds(state.CurrentPositionSeconds).ToString(@"m\:ss");

                // 자동 재생
                _player.Play();
            }
            else
            {
                TitleText.Text  = "No track selected";
                ArtistText.Text = "";
            }
        }

        #endregion

        #region Playlist Search

        private bool PlaylistFilter(object item)
        {
            if (item is not TrackInfo track) return true;
            var query = PlaylistSearchBox?.Text ?? "";
            if (string.IsNullOrEmpty(query)) return true;
            return track.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   track.DisplayArtist.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void PlaylistSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(PlaylistSearchBox.Text);
            SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            _playlistView?.Refresh();
        }

        #endregion

        #region Speed Control

        private void SpeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!float.TryParse(btn.Tag?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float speed)) return;

            _player.Speed = speed;
            UpdateSpeedButtons(speed);
        }

        private void UpdateSpeedButtons(float speed)
        {
            foreach (var child in SpeedButtonPanel.Children.OfType<Button>())
            {
                bool isActive = float.TryParse(
                    child.Tag?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float btnSpeed) && Math.Abs(btnSpeed - speed) < 0.001f;

                child.Background = isActive
                    ? (SolidColorBrush)FindResource("AccentBrush")
                    : Brushes.Transparent;
                child.Foreground = isActive
                    ? (SolidColorBrush)FindResource("TextPrimaryBrush")
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }
        }

        #endregion
    }
}
