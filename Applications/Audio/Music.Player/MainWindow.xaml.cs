using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".wma", ".aac", ".ogg" };

        // 가사 관련
        private readonly ObservableCollection<LrcLine> _lyricsLines = new();
        private int _currentLyricsIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            EnsureResourcesExist();
            _snap = new WindowSnapService(this);

            PlaylistBox.ItemsSource = _playlist;
            LyricsBox.ItemsSource = _lyricsLines;
            _player.PositionChanged += Player_PositionChanged;
            _player.PlaybackStopped += Player_PlaybackStopped;

            // Enable drag & drop for main window
            AllowDrop = true;
            Drop += MainWindow_Drop;
            DragEnter += MainWindow_DragEnter;

            // Restore saved playlist state after window is loaded
            Loaded += async (s, e) => await RestorePlaylistStateAsync();
        }

        private void EnsureResourcesExist()
        {
            try
            {
                var exePath = AppContext.BaseDirectory;
                var resourcePath = System.IO.Path.Combine(exePath, "Resources");
                var appIcoPath = System.IO.Path.Combine(resourcePath, "app.ico");

                if (!File.Exists(appIcoPath))
                {
                    IconGenerator.GenerateAppIcon(resourcePath);
                }
            }
            catch
            {
                // Icon generation failure should not prevent app from running
            }
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
                nextIndex = _random.Next(_playlist.Count);
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

            _currentIndex = index;
            var track = _playlist[index];

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
                MessageBox.Show("플레이리스트가 비어있습니다.", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show($"저장 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            MessageBox.Show("유효한 파일이 없습니다.", "Load Playlist", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"불러오기 실패: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var historyWindow = new HistoryWindow(async paths =>
            {
                await AddFilesAsync(paths);
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }
            })
            {
                Owner = this
            };
            historyWindow.Show();
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            var libraryWindow = new LibraryWindow(async paths =>
            {
                await AddFilesAsync(paths);
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }
            })
            {
                Owner = this
            };
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
                            // RemoveTrackButton_Click 로직 재사용
                            var fakeButton = new Button { DataContext = selectedTrack };
                            RemoveTrackButton_Click(fakeButton, new RoutedEventArgs());
                        }
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    // 선택된 항목을 위로 이동
                    if (PlaylistBox.SelectedItem is TrackInfo trackUp)
                    {
                        int index = _playlist.IndexOf(trackUp);
                        if (index > 0)
                        {
                            MovePlaylistItem(index, index - 1);
                        }
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    // 선택된 항목을 아래로 이동
                    if (PlaylistBox.SelectedItem is TrackInfo trackDown)
                    {
                        int index = _playlist.IndexOf(trackDown);
                        if (index >= 0 && index < _playlist.Count - 1)
                        {
                            MovePlaylistItem(index, index + 1);
                        }
                    }
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

        protected override void OnClosed(EventArgs e)
        {
            SavePlaylistState();
            _player.Dispose();
            _snap.Dispose();
            base.OnClosed(e);
        }

        #region Command Line Arguments

        public void LoadFilesFromArgs(string[] args)
        {
            if (args.Length == 0) return;

            // 기존 플레이리스트 클리어
            _player.Stop();
            _playlist.Clear();
            _currentIndex = -1;

            // 파일 추가
            foreach (var arg in args)
            {
                if (File.Exists(arg) && SupportedExtensions.Contains(System.IO.Path.GetExtension(arg).ToLower()))
                {
                    AddTrack(arg);
                }
            }

            // 플레이리스트 표시
            if (_playlist.Count > 0)
            {
                // 이미 열려있지 않은 경우에만 열기 (너비 증가 방지)
                if (PlaylistToggle.IsChecked != true)
                {
                    PlaylistToggle.IsChecked = true;
                    TogglePlaylistPanel();
                }

                // 첫 번째 트랙 자동 재생
                PlayTrack(0);
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
                IsRepeatEnabled = _isRepeatEnabled
            };

            PlaylistService.SaveState(state);
        }

        private async Task RestorePlaylistStateAsync()
        {
            var state = PlaylistService.LoadState();
            if (state == null || state.FilePaths.Count == 0)
                return;

            // Restore shuffle and repeat settings (UI thread)
            _isShuffleEnabled = state.IsShuffleEnabled;
            _isRepeatEnabled  = state.IsRepeatEnabled;
            ShuffleButton.IsChecked = _isShuffleEnabled;
            RepeatButton.IsChecked  = _isRepeatEnabled;

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

            // UI 스레드: ObservableCollection 갱신
            foreach (var track in tracks)
                _playlist.Add(track);

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
                _player.Load(track.FilePath);
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
    }
}
