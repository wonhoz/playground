using System.IO;
using System.Windows;
using System.Windows.Input;
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
        private string? _lastLogFilePath;
        private AppSettings _settings = AppSettings.Load();

        public MainWindow()
        {
            InitializeComponent();
            RestoreSettings();
        }

        #region Settings

        private void RestoreSettings()
        {
            if (!string.IsNullOrEmpty(_settings.LastDestinationPath) && Directory.Exists(_settings.LastDestinationPath))
            {
                _destinationPath = _settings.LastDestinationPath;
                DestinationPathText.Text = _destinationPath;
                DestinationPathText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(238, 238, 238));
            }

            FolderStructureCombo.SelectedIndex = _settings.FolderStructureIndex;
            CustomPatternBox.Text = _settings.CustomPattern;
            AutoRotateCheckBox.IsChecked = _settings.AutoRotate;
            SaveLogCheckBox.IsChecked = _settings.SaveLog;
            MoveFilesCheckBox.IsChecked = _settings.MoveFiles;

            UpdateRecentDestinations();
            UpdateStartButton();
        }

        private void SaveSettings()
        {
            _settings.LastDestinationPath = _destinationPath;
            _settings.FolderStructureIndex = FolderStructureCombo.SelectedIndex;
            _settings.CustomPattern = CustomPatternBox.Text;
            _settings.AutoRotate = AutoRotateCheckBox.IsChecked == true;
            _settings.SaveLog = SaveLogCheckBox.IsChecked == true;
            _settings.MoveFiles = MoveFilesCheckBox.IsChecked == true;
            _settings.Save();
        }

        #endregion

        #region Keyboard Shortcuts

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Return when StartButton.IsEnabled:
                    Start_Click(sender, e);
                    break;
                case Key.Escape when _cancellationTokenSource != null:
                    Cancel_Click(sender, e);
                    break;
                case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                    SelectFiles_Click(sender, e);
                    break;
            }
        }

        private void FileListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
                RemoveSelectedFiles();
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
            => RemoveSelectedFiles();

        private void RemoveSelectedFiles()
        {
            if (FileListBox.SelectedItems.Count == 0) return;

            var selectedIndices = new HashSet<int>();
            var items = FileListBox.Items;
            foreach (var sel in FileListBox.SelectedItems)
            {
                int idx = items.IndexOf(sel);
                if (idx >= 0)
                    selectedIndices.Add(idx);
            }

            for (int i = _selectedFiles.Count - 1; i >= 0; i--)
            {
                if (selectedIndices.Contains(i))
                    _selectedFiles.RemoveAt(i);
            }
            UpdateFileList();
            UpdateStartButton();
        }

        #endregion

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
                e.Effects = System.Windows.DragDropEffects.Copy;
            else
                e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private async void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ShowDragOverState(false);
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var items = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                await AddFilesAsync(items);
            }
        }

        private void ShowDragOverState(bool show)
        {
            DragOverState.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DefaultState.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

            DropZone.BorderBrush = new System.Windows.Media.SolidColorBrush(show
                ? System.Windows.Media.Color.FromRgb(79, 195, 247)
                : System.Windows.Media.Color.FromRgb(64, 64, 64));
            DropZone.BorderThickness = new Thickness(show ? 3 : 2);
        }

        #endregion

        #region File Selection

        private async void SelectFiles_Click(object sender, RoutedEventArgs e)
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
                await AddFilesAsync(dialog.FileNames);
        }

        private async Task AddFilesAsync(IEnumerable<string> items)
        {
            StatusText.Text = "파일 탐색 중...";
            var newFiles = new List<string>();
            var existing = new HashSet<string>(_selectedFiles, StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    if (Directory.Exists(item))
                    {
                        var files = Directory.GetFiles(item, "*.*", SearchOption.AllDirectories)
                            .Where(f => MediaDateExtractor.IsSupportedMediaFile(f));
                        foreach (var file in files)
                        {
                            if (existing.Add(file))
                            {
                                newFiles.Add(file);
                                Dispatcher.InvokeAsync(
                                    () => StatusText.Text = $"파일 탐색 중... {newFiles.Count}개",
                                    System.Windows.Threading.DispatcherPriority.Background);
                            }
                        }
                    }
                    else if (File.Exists(item) && MediaDateExtractor.IsSupportedMediaFile(item))
                    {
                        if (existing.Add(item))
                            newFiles.Add(item);
                    }
                }
            });

            foreach (var file in newFiles)
                _selectedFiles.Add(file);

            UpdateFileList();
            UpdateStartButton();
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            _selectedFiles.Clear();
            UpdateFileList();
            UpdateStartButton();
            ResultPanel.Visibility = Visibility.Collapsed;
            OpenFolderButton.Visibility = Visibility.Collapsed;
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
                SetDestination(dialog.SelectedPath);
            }
        }

        private void SetDestination(string path)
        {
            _destinationPath = path;
            DestinationPathText.Text = _destinationPath;
            DestinationPathText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(238, 238, 238));
            _settings.AddRecentDestination(path);
            UpdateRecentDestinations();
            UpdateStartButton();
            SaveSettings();
        }

        private void UpdateRecentDestinations()
        {
            RecentDestPanel.Children.Clear();
            foreach (var path in _settings.RecentDestinations)
            {
                if (path == _destinationPath) continue;
                if (!Directory.Exists(path)) continue;

                var btn = new System.Windows.Controls.Button
                {
                    Content = path,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 2, 0, 0),
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(158, 158, 158)),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                };

                var capturedPath = path;
                btn.Click += (_, _) => SetDestination(capturedPath);
                btn.MouseEnter += (_, _) => btn.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(238, 238, 238));
                btn.MouseLeave += (_, _) => btn.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(158, 158, 158));

                RecentDestPanel.Children.Add(btn);
            }
            RecentDestPanel.Visibility = RecentDestPanel.Children.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Folder Structure

        private (FolderStructure structure, string? customPattern) GetSelectedFolderOptions()
        {
            var selectedItem = FolderStructureCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var tag = selectedItem?.Tag?.ToString();

            return tag switch
            {
                "YearMonthDay" => (FolderStructure.YearMonthDay, null),
                "Custom" => (FolderStructure.Custom, CustomPatternBox.Text),
                _ => (FolderStructure.YearMonth, null)
            };
        }

        private void FolderStructureCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var selectedItem = FolderStructureCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var tag = selectedItem?.Tag?.ToString();
            if (CustomPatternPanel != null)
                CustomPatternPanel.Visibility = tag == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            SaveSettings();
        }

        #endregion

        #region Preview

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0 || string.IsNullOrEmpty(_destinationPath)) return;

            var (folderStructure, customPattern) = GetSelectedFolderOptions();

            // 커스텀 패턴 유효성 검사
            if (folderStructure == FolderStructure.Custom)
            {
                var error = FileOrganizer.ValidateCustomPattern(customPattern ?? "");
                if (error != null)
                {
                    DarkMessageBox.Show($"패턴 오류: {error}", "유효하지 않은 패턴",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            PreviewButton.IsEnabled = false;
            StatusText.Text = "미리 보기 계산 중...";

            try
            {
                var entries = await _organizer.PreviewFilesAsync(
                    _selectedFiles, _destinationPath, folderStructure, customPattern);

                var moveFiles = MoveFilesCheckBox.IsChecked == true;
                var dialog = new PreviewDialog(entries, _destinationPath, moveFiles) { Owner = this };
                dialog.ShowDialog();
            }
            finally
            {
                PreviewButton.IsEnabled = true;
                StatusText.Text = $"{_selectedFiles.Count}개 파일 선택됨";
            }
        }

        #endregion

        #region Start/Cancel

        private void UpdateStartButton()
        {
            var ready = _selectedFiles.Count > 0 && !string.IsNullOrEmpty(_destinationPath);
            StartButton.IsEnabled = ready;
            PreviewButton.IsEnabled = ready;
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0 || string.IsNullOrEmpty(_destinationPath))
                return;

            // 커스텀 패턴 유효성 검사
            var (folderStructure, customPattern) = GetSelectedFolderOptions();
            if (folderStructure == FolderStructure.Custom)
            {
                var error = FileOrganizer.ValidateCustomPattern(customPattern ?? "");
                if (error != null)
                {
                    DarkMessageBox.Show($"패턴 오류: {error}", "유효하지 않은 패턴",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // UI 상태 변경
            ShowProgressState(true);
            StartButton.IsEnabled = false;
            PreviewButton.IsEnabled = false;
            ResultPanel.Visibility = Visibility.Collapsed;
            OpenFolderButton.Visibility = Visibility.Collapsed;

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
                var autoRotate = AutoRotateCheckBox.IsChecked == true;
                var moveFiles = MoveFilesCheckBox.IsChecked == true;

                var result = await _organizer.OrganizeFilesAsync(
                    _selectedFiles, _destinationPath,
                    folderStructure, customPattern,
                    autoRotate, moveFiles,
                    progress, _cancellationTokenSource.Token);

                if (SaveLogCheckBox.IsChecked == true)
                {
                    var logPath = await OrganizeLogger.SaveLogAsync(result, _destinationPath);
                    result.LogFilePath = logPath;
                }

                SaveSettings();
                ShowResult(result);
            }
            catch (OperationCanceledException)
            {
                ResultPanel.Visibility = Visibility.Collapsed;
                StatusText.Text = "작업이 취소되었습니다";
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"오류가 발생했습니다: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowProgressState(false);
                UpdateStartButton();
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

            // 요약 헤더
            var rotatedNote = result.RotatedCount > 0 ? $"  (회전 적용 {result.RotatedCount}개)" : "";
            ResultSummaryText.Text = $"총 {result.TotalFiles}개 파일 중 {result.SuccessCount}개 정리 완료{rotatedNote}";

            ResultImageText.Text = $"▪ 이미지  {result.ImageCount}개 정리됨";
            ResultImageText.Visibility = result.ImageCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultVideoText.Text = $"▪ 동영상  {result.VideoCount}개 정리됨";
            ResultVideoText.Visibility = result.VideoCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            var hasSkippedOrError = result.DuplicateCount > 0 || result.SkippedCount > 0 || result.ErrorCount > 0;
            ResultDivider.Visibility = result.SuccessCount > 0 && hasSkippedOrError ? Visibility.Visible : Visibility.Collapsed;

            ResultDuplicateText.Text = $"▸ 중복 건너뜀  {result.DuplicateCount}개";
            ResultDuplicateText.Visibility = result.DuplicateCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultSkippedText.Text = $"▸ 미지원 형식  {result.SkippedCount}개";
            ResultSkippedText.Visibility = result.SkippedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultErrorText.Text = $"▸ 오류  {result.ErrorCount}개";
            ResultErrorText.Visibility = result.ErrorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            PopulateDetailList(result);

            _lastLogFilePath = result.LogFilePath;
            if (result.LogFilePath != null)
            {
                OpenLogButtonText.Text = $"로그 보기  {Path.GetFileName(result.LogFilePath)}";
                OpenLogButton.Visibility = Visibility.Visible;
                StatusText.Text = $"완료: {result.SuccessCount}개 정리됨  |  로그: {Path.GetFileName(result.LogFilePath)}";
            }
            else
            {
                OpenLogButton.Visibility = Visibility.Collapsed;
                StatusText.Text = $"완료: {result.SuccessCount}개 파일 정리됨";
            }

            // 결과 패널 폴더 열기 버튼 표시
            OpenFolderButton.Visibility = result.SuccessCount > 0 && !string.IsNullOrEmpty(_destinationPath)
                ? Visibility.Visible : Visibility.Collapsed;

            _selectedFiles.Clear();
            UpdateFileList();
            UpdateStartButton();

            if (result.SuccessCount > 0)
            {
                var dialog = new CompletionDialog(result.SuccessCount) { Owner = this };
                dialog.ShowDialog();

                if (dialog.OpenFolder && !string.IsNullOrEmpty(_destinationPath))
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", _destinationPath); } catch { }
                }
            }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastLogFilePath) && File.Exists(_lastLogFilePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _lastLogFilePath,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void OpenDestinationFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_destinationPath) && Directory.Exists(_destinationPath))
            {
                try { System.Diagnostics.Process.Start("explorer.exe", _destinationPath); } catch { }
            }
        }

        private bool _detailExpanded = false;

        private void ToggleDetail_Click(object sender, RoutedEventArgs e)
        {
            _detailExpanded = !_detailExpanded;
            DetailListBox.Visibility = _detailExpanded ? Visibility.Visible : Visibility.Collapsed;
            ToggleDetailArrow.Text = _detailExpanded ? "▼" : "▶";
            ToggleDetailText.Text = _detailExpanded ? "상세 목록 접기" : "상세 목록 보기";
        }

        private void PopulateDetailList(FileOrganizer.OrganizeSummary result)
        {
            // 성공 파일 + 실패/건너뜀 모두 포함
            var items = result.Results.Select(r =>
            {
                string icon, color;
                if (r.Success)
                {
                    icon = r.Moved ? "→" : "✓";
                    color = "#66BB6A";
                }
                else if (r.IsSkippedAsDuplicate) { icon = "◈"; color = "#64B5F6"; }
                else if (r.ErrorMessage?.Contains("건너뜀") == true) { icon = "◇"; color = "#FFC107"; }
                else { icon = "✕"; color = "#EF5350"; }

                return new
                {
                    StatusIcon = icon,
                    FileName = Path.GetFileName(r.SourcePath),
                    StatusColor = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color))
                };
            }).ToList();

            DetailListBox.ItemsSource = items;

            // 비성공 항목이 있으면 토글 버튼 표시
            var hasNonSuccess = result.Results.Any(r => !r.Success);
            ToggleDetailButton.Visibility = Visibility.Visible;
            _detailExpanded = false;
            DetailListBox.Visibility = Visibility.Collapsed;
            ToggleDetailArrow.Text = "▶";

            var nonSuccessCount = result.Results.Count(r => !r.Success);
            ToggleDetailText.Text = hasNonSuccess
                ? $"상세 목록 보기  (성공 {result.SuccessCount}개 + 비성공 {nonSuccessCount}개)"
                : $"상세 목록 보기  ({result.SuccessCount}개 성공)";
        }

        #endregion

        #region Title Bar

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new HelpDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                MaximizeButton_Click(sender, e);
            else
                DragMove();
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
            SaveSettings();
            Close();
        }

        #endregion
    }
}
