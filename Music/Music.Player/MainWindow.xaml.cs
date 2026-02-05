using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Music.Player.Models;
using Music.Player.Services;
using NAudio.Wave;

namespace Music.Player
{
    public partial class MainWindow : Window
    {
        private readonly AudioPlayer _player = new();
        private readonly ObservableCollection<TrackInfo> _playlist = new();
        private readonly Random _random = new();

        private int _currentIndex = -1;
        private bool _isDraggingSlider;
        private bool _isShuffleEnabled;
        private bool _isRepeatEnabled;

        private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".wma", ".aac", ".ogg" };

        public MainWindow()
        {
            InitializeComponent();
            EnsureResourcesExist();

            PlaylistBox.ItemsSource = _playlist;
            _player.PositionChanged += Player_PositionChanged;
            _player.PlaybackStopped += Player_PlaybackStopped;

            // Enable drag & drop for main window
            AllowDrop = true;
            Drop += MainWindow_Drop;
            DragEnter += MainWindow_DragEnter;

            // Restore saved playlist state after window is loaded
            Loaded += (s, e) => RestorePlaylistState();
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
                if (!_isDraggingSlider)
                {
                    ProgressSlider.Value = position.TotalSeconds;
                    CurrentTimeText.Text = position.ToString(@"m\:ss");
                }
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
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            _player.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
            {
                CurrentTimeText.Text = TimeSpan.FromSeconds(e.NewValue).ToString(@"m\:ss");
            }
        }

        #endregion

        #region Playlist

        private void PlaylistToggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePlaylistPanel();
        }

        private void TogglePlaylistPanel()
        {
            if (PlaylistToggle.IsChecked == true)
            {
                PlaylistPanel.Visibility = Visibility.Visible;
                PlaylistColumn.Width = new GridLength(220);
                Width = Math.Max(Width, 600);
            }
            else
            {
                PlaylistPanel.Visibility = Visibility.Collapsed;
                PlaylistColumn.Width = new GridLength(0);
            }
        }

        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Add Music Files",
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a;*.wma;*.aac;*.ogg|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            _player.Stop();
            _playlist.Clear();
            _currentIndex = -1;
            UpdatePlayPauseIcon(false);
            TitleText.Text = "No track selected";
            ArtistText.Text = "";
            AlbumArtImage.Visibility = Visibility.Collapsed;
            DefaultIcon.Visibility = Visibility.Visible;
            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = "0:00";
            ProgressSlider.Value = 0;
        }

        private void PlaylistBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                PlayTrack(PlaylistBox.SelectedIndex);
            }
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => SupportedExtensions.Contains(System.IO.Path.GetExtension(f).ToLower()));
                    foreach (var file in files)
                    {
                        AddTrack(file);
                    }
                }
                else if (File.Exists(path) && SupportedExtensions.Contains(System.IO.Path.GetExtension(path).ToLower()))
                {
                    AddTrack(path);
                }
            }

            // Auto-play first track if nothing is playing
            if (_currentIndex < 0 && _playlist.Count > 0)
            {
                PlayTrack(0);
            }
        }

        private void AddTrack(string filePath)
        {
            if (_playlist.Any(t => t.FilePath == filePath)) return;

            var track = TrackInfo.FromFile(filePath);
            _playlist.Add(track);
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

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);

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

        private void PlaylistBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SavePlaylistState();
            _player.Dispose();
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
                    var track = TrackInfo.FromFile(arg);
                    _playlist.Add(track);
                }
            }

            // 플레이리스트 표시
            if (_playlist.Count > 0)
            {
                PlaylistToggle.IsChecked = true;
                TogglePlaylistPanel();

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

        private void RestorePlaylistState()
        {
            var state = PlaylistService.LoadState();
            if (state == null || state.FilePaths.Count == 0)
                return;

            // Restore shuffle and repeat settings
            _isShuffleEnabled = state.IsShuffleEnabled;
            _isRepeatEnabled = state.IsRepeatEnabled;
            ShuffleButton.IsChecked = _isShuffleEnabled;
            RepeatButton.IsChecked = _isRepeatEnabled;

            // Add tracks to playlist
            foreach (var filePath in state.FilePaths)
            {
                var track = TrackInfo.FromFile(filePath);
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

                // Update display without auto-playing
                UpdateTrackDisplay(track);
                PlaylistBox.SelectedIndex = _currentIndex;
                PlaylistBox.ScrollIntoView(PlaylistBox.SelectedItem);

                ProgressSlider.Maximum = track.Duration.TotalSeconds;
                TotalTimeText.Text = track.DurationText;

                // Load track and seek to saved position (paused)
                _player.Load(track.FilePath);
                if (state.CurrentPositionSeconds > 0 && state.CurrentPositionSeconds < track.Duration.TotalSeconds)
                {
                    _player.Seek(TimeSpan.FromSeconds(state.CurrentPositionSeconds));
                    ProgressSlider.Value = state.CurrentPositionSeconds;
                    CurrentTimeText.Text = TimeSpan.FromSeconds(state.CurrentPositionSeconds).ToString(@"m\:ss");
                }
            }
        }

        #endregion
    }
}
