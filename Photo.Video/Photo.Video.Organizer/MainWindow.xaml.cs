using System.IO;
using System.Windows;
using Photo.Video.Organizer.Services;
using WinForms = System.Windows.Forms;

namespace Photo.Video.Organizer
{
    public partial class MainWindow : Window
    {
        private readonly FileOrganizer _organizer = new();
        private readonly List<string> _selectedFiles = new();
        private string? _destinationPath;
        private CancellationTokenSource? _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            EnsureResourcesExist();
        }

        /// <summary>
        /// Resources 폴더와 아이콘 파일이 없으면 생성
        /// </summary>
        private void EnsureResourcesExist()
        {
            try
            {
                var exePath = AppContext.BaseDirectory;
                var resourcePath = Path.Combine(exePath, "Resources");
                var appIcoPath = Path.Combine(resourcePath, "app.ico");

                if (!File.Exists(appIcoPath))
                {
                    IconGenerator.GenerateAllIcons(resourcePath);
                }
            }
            catch
            {
                // 아이콘 생성 실패해도 앱은 계속 실행
            }
        }

        #region Drag & Drop

        private void DropZone_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                ShowDragOverState(true);
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            ShowDragOverState(false);
        }

        private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ShowDragOverState(false);

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var items = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                AddFiles(items);
            }
        }

        private void ShowDragOverState(bool show)
        {
            DragOverState.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DefaultState.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

            if (show)
            {
                // Dark theme: Cyan accent border
                DropZone.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(79, 195, 247));
                DropZone.BorderThickness = new Thickness(3);
            }
            else
            {
                // Dark theme: Dark gray border
                DropZone.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(64, 64, 64));
                DropZone.BorderThickness = new Thickness(2);
            }
        }

        #endregion

        #region File Selection

        private void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "사진 및 동영상 선택",
                Filter = "미디어 파일|*.jpg;*.jpeg;*.heic;*.heif;*.png;*.gif;*.bmp;*.webp;*.tiff;*.tif;*.mp4;*.mov;*.m4v;*.3gp;*.avi;*.mkv;*.cr2;*.nef;*.arw;*.dng|" +
                         "이미지 파일|*.jpg;*.jpeg;*.heic;*.heif;*.png;*.gif;*.bmp;*.webp;*.tiff;*.tif;*.cr2;*.nef;*.arw;*.dng|" +
                         "동영상 파일|*.mp4;*.mov;*.m4v;*.3gp;*.avi;*.mkv|" +
                         "모든 파일|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                if (Directory.Exists(item))
                {
                    // 폴더인 경우 하위 파일 모두 추가
                    var files = Directory.GetFiles(item, "*.*", SearchOption.AllDirectories)
                        .Where(f => MediaDateExtractor.IsSupportedMediaFile(f));
                    foreach (var file in files)
                    {
                        AddFileIfNotExists(file);
                    }
                }
                else if (File.Exists(item) && MediaDateExtractor.IsSupportedMediaFile(item))
                {
                    AddFileIfNotExists(item);
                }
            }

            UpdateFileList();
            UpdateStartButton();
        }

        private void AddFileIfNotExists(string filePath)
        {
            if (!_selectedFiles.Contains(filePath))
            {
                _selectedFiles.Add(filePath);
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            _selectedFiles.Clear();
            UpdateFileList();
            UpdateStartButton();
            ResultPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateFileList()
        {
            FileListBox.ItemsSource = _selectedFiles.Select(f => Path.GetFileName(f)).ToList();
            FileCountText.Text = $"{_selectedFiles.Count}개";
            ClearFilesButton.Visibility = _selectedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = _selectedFiles.Count > 0 ? $"{_selectedFiles.Count}개 파일 선택됨" : "준비됨";
        }

        #endregion

        #region Destination Selection

        private void SelectDestination_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "사진/동영상을 정리할 폴더를 선택하세요",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _destinationPath = dialog.SelectedPath;
                DestinationPathText.Text = _destinationPath;
                // Dark theme: Light text color
                DestinationPathText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(238, 238, 238));
                UpdateStartButton();
            }
        }

        #endregion

        #region Folder Structure

        private FolderStructure GetSelectedFolderStructure()
        {
            var selectedItem = FolderStructureCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var tag = selectedItem?.Tag?.ToString();

            return tag switch
            {
                "YearMonthDay" => FolderStructure.YearMonthDay,
                _ => FolderStructure.YearMonth
            };
        }

        #endregion

        #region Start/Cancel

        private void UpdateStartButton()
        {
            StartButton.IsEnabled = _selectedFiles.Count > 0 && !string.IsNullOrEmpty(_destinationPath);
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0 || string.IsNullOrEmpty(_destinationPath))
                return;

            // UI 상태 변경
            ShowProgressState(true);
            StartButton.IsEnabled = false;
            ResultPanel.Visibility = Visibility.Collapsed;

            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                var percent = (int)((double)p.current / p.total * 100);
                ProgressBar.Value = percent;
                ProgressPercent.Text = $"{percent}%";
                ProgressText.Text = $"파일 처리 중... ({p.current}/{p.total})";
                ProgressFileName.Text = p.fileName;
            });

            try
            {
                // 폴더 구조 옵션 가져오기
                var folderStructure = GetSelectedFolderStructure();

                var result = await _organizer.OrganizeFilesAsync(
                    _selectedFiles,
                    _destinationPath,
                    folderStructure,
                    progress,
                    _cancellationTokenSource.Token);

                ShowResult(result);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "작업이 취소되었습니다";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"오류가 발생했습니다: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowProgressState(false);
                StartButton.IsEnabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowProgressState(bool show)
        {
            ProgressState.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DefaultState.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            DropZone.AllowDrop = !show;
        }

        private void ShowResult(FileOrganizer.OrganizeSummary result)
        {
            ResultPanel.Visibility = Visibility.Visible;
            ResultSuccessText.Text = $"성공: {result.SuccessCount}개";
            ResultDuplicateText.Text = $"중복 건너뜀: {result.DuplicateCount}개";
            ResultDuplicateText.Visibility = result.DuplicateCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultSkippedText.Text = $"건너뜀: {result.SkippedCount}개";
            ResultSkippedText.Visibility = result.SkippedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultErrorText.Text = $"오류: {result.ErrorCount}개";
            ResultErrorText.Visibility = result.ErrorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            StatusText.Text = $"완료: {result.SuccessCount}개 파일 정리됨";

            // 완료 후 파일 목록 초기화
            _selectedFiles.Clear();
            UpdateFileList();
            UpdateStartButton();

            // 성공 메시지 - 커스텀 다이얼로그 사용
            if (result.SuccessCount > 0)
            {
                var dialog = new CompletionDialog(result.SuccessCount)
                {
                    Owner = this
                };

                dialog.ShowDialog();

                if (dialog.OpenFolder && !string.IsNullOrEmpty(_destinationPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", _destinationPath);
                }
            }
        }

        #endregion

        #region Title Bar

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
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

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
