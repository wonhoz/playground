using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenRecorder.Models;
using ScreenRecorder.Services;
using System.Text;

namespace ScreenRecorder;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // 글로벌 단축키
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private const int HotkeyRecord  = 1;  // F9
    private const int HotkeyPause   = 2;  // F10
    private const int HotkeyStop    = 3;  // F11
    private const uint VK_F9  = 0x78;
    private const uint VK_F10 = 0x79;
    private const uint VK_F11 = 0x7A;

    private readonly RecordingSettings _settings = RecordingSettings.Load();
    private ScreenCaptureService? _captureService;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private Int32Rect _selectedRegion;
    private bool _regionSelected;
    private bool _ffmpegAvailable;

    private enum RecordState { Idle, Countdown, Recording, Paused }
    private RecordState _state = RecordState.Idle;
    private CancellationTokenSource? _countdownCts;
    private HwndSource? _hwndSource;
    private HudOverlay? _hud;

    private static SolidColorBrush ColorBrush(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        OutputFolderText.Text = _settings.OutputFolder;
        FileNamePrefixBox.Text = _settings.FileNamePrefix;

        // 저장된 최대 녹화 시간 복원
        MaxTimeCombo.SelectedIndex = _settings.MaxRecordingSeconds switch
        {
            30    => 1,
            60    => 2,
            180   => 3,
            300   => 4,
            600   => 5,
            _     => 0
        };

        // 저장된 FPS 복원
        FpsCombo.SelectedIndex = _settings.FrameRate switch
        {
            10 => 0,
            15 => 1,
            24 => 2,
            30 => 3,
            _ => 1
        };

        // 저장된 포맷 복원
        FormatCombo.SelectedIndex = _settings.OutputFormat == "gif" ? 1 : 0;

        // 저장된 커서 설정 복원
        CursorCheckBox.IsChecked = _settings.ShowCursor;

        // 저장된 오디오 설정 복원
        AudioCheckBox.IsChecked = _settings.RecordAudio;

        // 저장된 영역 복원
        if (_settings.LastRegionWidth > 0 && _settings.LastRegionHeight > 0)
        {
            _selectedRegion = new Int32Rect(
                _settings.LastRegionX, _settings.LastRegionY,
                _settings.LastRegionWidth, _settings.LastRegionHeight);
            _regionSelected = true;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += async (_, _) =>
        {
            var elapsed = _stopwatch.Elapsed;
            var timeText = elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
            TimerText.Text = timeText;

            // HUD 시간 갱신
            _hud?.UpdateState(isRecording: _state == RecordState.Recording, timeText);

            // 최대 녹화 시간 체크 — _stopwatch 기준이므로 일시정지 시간 제외
            if (_settings.MaxRecordingSeconds > 0 &&
                elapsed.TotalSeconds >= _settings.MaxRecordingSeconds &&
                _state == RecordState.Recording)
            {
                await StopRecordingAsync();
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));

            _hwndSource = source;
            _hwndSource.AddHook(WndProc);
            RegisterHotKey(source.Handle, HotkeyRecord, 0, VK_F9);
            RegisterHotKey(source.Handle, HotkeyPause,  0, VK_F10);
            RegisterHotKey(source.Handle, HotkeyStop,   0, VK_F11);
        }

        // 복원된 영역 정보 표시
        if (_regionSelected)
        {
            RegionInfoText.Text = $"X:{_selectedRegion.X}  Y:{_selectedRegion.Y}  |  {_selectedRegion.Width} × {_selectedRegion.Height}";
            RecordBtn.IsEnabled = true;
        }

        LastRegionBtn.IsEnabled = _settings.LastRegionWidth > 0;

        CheckFfmpeg();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            switch ((int)wParam)
            {
                case HotkeyRecord:
                    Record_Click(this, new RoutedEventArgs());
                    handled = true;
                    break;
                case HotkeyPause:
                    if (_state is RecordState.Recording or RecordState.Paused)
                        Pause_Click(this, new RoutedEventArgs());
                    handled = true;
                    break;
                case HotkeyStop:
                    if (_state is RecordState.Recording or RecordState.Paused)
                        Stop_Click(this, new RoutedEventArgs());
                    handled = true;
                    break;
            }
        }
        return nint.Zero;
    }

    private void CheckFfmpeg()
    {
        _ffmpegAvailable = EncoderService.IsFfmpegAvailable();

        if (_ffmpegAvailable)
        {
            FfmpegPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            FfmpegPanel.Visibility = Visibility.Visible;
            FfmpegStatusText.Text = "FFmpeg 미설치 — MP4 녹화 불가 (GIF는 가능)";
        }
    }

    private async void InstallFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        InstallFfmpegBtn.IsEnabled = false;
        InstallFfmpegBtn.Content = "설치 중...";
        FfmpegProgress.Visibility = Visibility.Visible;
        FfmpegProgress.IsIndeterminate = true;
        FfmpegLogText.Visibility = Visibility.Visible;

        try
        {
            // winget 시도
            UpdateFfmpegLog("winget으로 FFmpeg 설치 시도 중...");
            FfmpegStatusText.Text = "FFmpeg 설치 중 (winget)...";
            FfmpegStatusText.Foreground = ColorBrush("#F39C12");

            await RunInstallerWithLogAsync(
                "winget", "install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements");

            // 설치 완료 후 (또는 이미 설치) 다시 경로 탐색
            var found = EncoderService.IsFfmpegAvailable();

            if (!found)
            {
                // choco 시도
                UpdateFfmpegLog("winget 후 ffmpeg.exe 미발견 → Chocolatey로 재시도 중...");
                FfmpegStatusText.Text = "FFmpeg 설치 중 (Chocolatey)...";

                await RunInstallerWithLogAsync("choco", "install ffmpeg -y");
                found = EncoderService.IsFfmpegAvailable();
            }

            if (found)
            {
                _ffmpegAvailable = true;
                FfmpegProgress.IsIndeterminate = false;
                FfmpegProgress.Value = 100;
                FfmpegStatusText.Text = "FFmpeg 확인 완료!";
                FfmpegStatusText.Foreground = ColorBrush("#27AE60");
                UpdateFfmpegLog("MP4 녹화가 가능합니다");

                await Task.Delay(1500);
                FfmpegPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                FfmpegProgress.IsIndeterminate = false;
                FfmpegProgress.Value = 0;
                FfmpegStatusText.Text = "FFmpeg 경로를 찾을 수 없습니다";
                FfmpegStatusText.Foreground = ColorBrush("#E74C3C");
                UpdateFfmpegLog("수동 설치: 터미널에서 winget install Gyan.FFmpeg 실행");
                InstallFfmpegBtn.Content = "재시도";
                InstallFfmpegBtn.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            FfmpegProgress.IsIndeterminate = false;
            FfmpegStatusText.Text = "설치 오류 발생";
            FfmpegStatusText.Foreground = ColorBrush("#E74C3C");
            UpdateFfmpegLog($"오류: {ex.Message}");
            InstallFfmpegBtn.Content = "재시도";
            InstallFfmpegBtn.IsEnabled = true;
        }
    }

    private void UpdateFfmpegLog(string text)
    {
        FfmpegLogText.Text = text;
    }

    private async Task<bool> RunInstallerWithLogAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                UpdateFfmpegLog($"{command}을(를) 찾을 수 없습니다");
                return false;
            }

            // stdout 실시간 읽기
            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    Dispatcher.BeginInvoke(() => UpdateFfmpegLog(args.Data.Trim()));
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    Dispatcher.BeginInvoke(() => UpdateFfmpegLog(args.Data.Trim()));
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            UpdateFfmpegLog($"{command} 실행 실패");
            return false;
        }
    }

    private void SetRegion(Int32Rect region)
    {
        _selectedRegion = region;
        _regionSelected = true;
        RegionInfoText.Text = $"X:{region.X}  Y:{region.Y}  |  {region.Width} × {region.Height}";
        RecordBtn.IsEnabled = true;
        LastRegionBtn.IsEnabled = true;

        _settings.LastRegionX = region.X;
        _settings.LastRegionY = region.Y;
        _settings.LastRegionWidth = region.Width;
        _settings.LastRegionHeight = region.Height;
        _settings.Save();
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        Hide();

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var selector = new RegionSelectWindow();
            var result = selector.ShowDialog();

            Show();

            if (result == true && selector.RegionSelected)
                SetRegion(selector.SelectedRegion);
        });
    }

    private void SelectFullScreen_Click(object sender, RoutedEventArgs e)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var physW = (int)(SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
        var physH = (int)(SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY);
        physW = physW % 2 == 0 ? physW : physW - 1;
        physH = physH % 2 == 0 ? physH : physH - 1;
        SetRegion(new Int32Rect(0, 0, physW, physH));
    }

    private void FullScreen_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length <= 1) return;

        var menu = new System.Windows.Controls.ContextMenu
        {
            Background = ColorBrush("#2D2D2D"),
            BorderBrush = ColorBrush("#444444"),
            Foreground = ColorBrush("#E0E0E0")
        };

        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var bounds = screen.Bounds;
            var label = $"모니터 {i + 1}: {bounds.Width}×{bounds.Height}";
            if (screen.Primary) label += " (기본)";

            var item = new System.Windows.Controls.MenuItem
            {
                Header = label,
                Foreground = ColorBrush("#E0E0E0"),
                Tag = screen
            };
            item.Click += (_, _) =>
            {
                var s = (System.Windows.Forms.Screen)item.Tag;
                var b = s.Bounds;
                var w = b.Width % 2 == 0 ? b.Width : b.Width - 1;
                var h = b.Height % 2 == 0 ? b.Height : b.Height - 1;
                SetRegion(new Int32Rect(b.X, b.Y, w, h));
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void UseLastRegion_Click(object sender, RoutedEventArgs e)
    {
        SetRegion(new Int32Rect(
            _settings.LastRegionX, _settings.LastRegionY,
            _settings.LastRegionWidth, _settings.LastRegionHeight));
    }

    private void FormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (IsGifFormat())
        {
            // GIF는 FFmpeg 없이도 가능 → 경고 패널 숨김
            FfmpegPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            // MP4로 전환 시 FFmpeg 상태 다시 반영
            if (!_ffmpegAvailable)
                FfmpegPanel.Visibility = Visibility.Visible;
        }

        _settings.OutputFormat = IsGifFormat() ? "gif" : "mp4";
        _settings.Save();

        // GIF 선택 시 오디오 체크박스 비활성화
        if (IsGifFormat() && AudioCheckBox.IsChecked == true)
        {
            AudioCheckBox.IsChecked = false;
            _settings.RecordAudio = false;
            _settings.Save();
        }
        AudioCheckBox.IsEnabled = !IsGifFormat();
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "저장 폴더 선택",
            InitialDirectory = _settings.OutputFolder
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.OutputFolder = dialog.FolderName;
            OutputFolderText.Text = dialog.FolderName;
            _settings.Save();
        }
    }

    private void MaxTimeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.MaxRecordingSeconds = MaxTimeCombo.SelectedIndex switch
        {
            1 => 30,
            2 => 60,
            3 => 180,
            4 => 300,
            5 => 600,
            _ => 0
        };
        _settings.Save();
    }

    private void RefreshFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        CheckFfmpeg();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var msg = new StringBuilder();
        msg.AppendLine("Screen.Recorder 사용법");
        msg.AppendLine();
        msg.AppendLine("【 영역 선택 】");
        msg.AppendLine("  ▣ 영역 선택 — 드래그로 녹화 범위 지정");
        msg.AppendLine("  ⬜ 전체 화면 — 기본 모니터 전체 캡처");
        msg.AppendLine("     (우클릭: 멀티 모니터 선택)");
        msg.AppendLine("  ↩ 이전 영역 — 마지막으로 선택한 영역 재사용");
        msg.AppendLine();
        msg.AppendLine("【 글로벌 단축키 】");
        msg.AppendLine("  F9  — 녹화 시작 / 카운트다운 취소");
        msg.AppendLine("  F10 — 일시정지 / 재개");
        msg.AppendLine("  F11 — 녹화 정지 및 저장");
        msg.AppendLine();
        msg.AppendLine("【 설정 】");
        msg.AppendLine("  출력 형식: MP4 (FFmpeg 필요) / GIF");
        msg.AppendLine("  FPS: 10 / 15 / 24 / 30");
        msg.AppendLine("  최대 시간: 실제 녹화 시간 기준 (일시정지 제외) 자동 정지");
        msg.AppendLine("  마우스 포인터 포함 여부 선택 가능");
        msg.AppendLine("  시스템 오디오 녹음: MP4 + FFmpeg 필요 (dshow 가상 장치)");
        msg.AppendLine("  파일명: 접두사 지정 (기본: recording)");
        msg.AppendLine();
        msg.AppendLine("【 녹화 완료 】");
        msg.AppendLine("  완료 시 파일 경로가 클립보드에 자동 복사됩니다.");
        msg.AppendLine("  [예] 파일 열기  |  [아니요] 폴더 열기  |  [취소] 닫기");
        msg.AppendLine("  파일 크기가 함께 표시됩니다.");
        msg.AppendLine();
        msg.AppendLine("【 미니 HUD 】");
        msg.AppendLine("  녹화 시작 시 화면 우하단에 플로팅 HUD가 표시됩니다.");
        msg.AppendLine("  HUD에서 일시정지/정지 가능, 드래그로 위치 이동 가능합니다.");
        msg.AppendLine();
        msg.AppendLine("【 FFmpeg 】");
        msg.AppendLine("  MP4/오디오 녹화에 FFmpeg가 필요합니다.");
        msg.AppendLine("  '설치' 버튼으로 winget/choco 자동 설치,");
        msg.AppendLine("  '↻' 버튼으로 설치 후 재검색 가능합니다.");
        msg.AppendLine("  GIF는 FFmpeg 없이도 녹화 가능합니다.");

        System.Windows.MessageBox.Show(msg.ToString(), "사용법", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FpsCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.FrameRate = GetSelectedFps();
        _settings.Save();
    }

    private void CursorCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.ShowCursor = CursorCheckBox.IsChecked == true;
        _settings.Save();
    }

    private void AudioCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var wantAudio = AudioCheckBox.IsChecked == true;
        // 오디오는 MP4 + FFmpeg 조합에서만 지원
        if (wantAudio && (IsGifFormat() || !_ffmpegAvailable))
        {
            AudioCheckBox.IsChecked = false;
            System.Windows.MessageBox.Show(
                "오디오 녹음은 MP4 형식과 FFmpeg가 모두 필요합니다.",
                "Screen.Recorder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _settings.RecordAudio = wantAudio;
        _settings.Save();
    }

    private void FileNamePrefixBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;

        // 파일명 불법 문자 제거
        var invalidChars = Path.GetInvalidFileNameChars();
        var raw = FileNamePrefixBox.Text;
        var cleaned = new string(raw.Where(c => !invalidChars.Contains(c)).ToArray());

        if (cleaned != raw)
        {
            // 커서 위치 보정 후 텍스트 교체
            var caretIndex = Math.Max(0, FileNamePrefixBox.CaretIndex - (raw.Length - cleaned.Length));
            FileNamePrefixBox.Text = cleaned;
            FileNamePrefixBox.CaretIndex = Math.Min(caretIndex, cleaned.Length);
        }

        var prefix = cleaned.Trim();
        if (string.IsNullOrEmpty(prefix))
        {
            _settings.FileNamePrefix = "recording";
        }
        else
        {
            _settings.FileNamePrefix = prefix;
        }
        _settings.Save();
    }

    private void FileNamePrefixBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FileNamePrefixBox.Text))
            FileNamePrefixBox.Text = "recording";
    }

    private int GetSelectedFps()
    {
        return FpsCombo.SelectedIndex switch
        {
            0 => 10,
            1 => 15,
            2 => 24,
            3 => 30,
            _ => 15
        };
    }

    private bool IsGifFormat() => FormatCombo.SelectedIndex == 1;

    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_state == RecordState.Idle)
        {
            if (!IsGifFormat() && !_ffmpegAvailable)
            {
                System.Windows.MessageBox.Show(
                    "MP4 녹화에는 FFmpeg가 필요합니다.\nGIF 형식으로 변경하거나 FFmpeg를 설치해주세요.",
                    "Screen.Recorder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _countdownCts = new CancellationTokenSource();
            SetState(RecordState.Countdown);

            try
            {
                for (var i = 3; i >= 1; i--)
                {
                    StatusText.Text = $"{i}초 후 녹화 시작";
                    TimerText.Text = $"0{i}";
                    StatusDot.Fill = ColorBrush(i == 1 ? "#E74C3C" : "#F39C12");
                    await Task.Delay(1000, _countdownCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                SetState(RecordState.Idle);
                return;
            }

            await StartRecordingAsync();
        }
        else if (_state == RecordState.Countdown)
        {
            _countdownCts?.Cancel();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (!_regionSelected) return;

        var fps = GetSelectedFps();
        var isGif = IsGifFormat();
        var ext = isGif ? "gif" : "mp4";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var prefix = string.IsNullOrWhiteSpace(_settings.FileNamePrefix) ? "recording" : _settings.FileNamePrefix;
        var outputPath = Path.Combine(_settings.OutputFolder, $"{prefix}_{timestamp}.{ext}");

        Directory.CreateDirectory(_settings.OutputFolder);

        var captureMouse = CursorCheckBox.IsChecked == true;
        var recordAudio  = !isGif && _ffmpegAvailable && (AudioCheckBox.IsChecked == true);
        _captureService = new ScreenCaptureService(
            _selectedRegion, fps, outputPath, isGif, captureMouse, recordAudio, _settings.AudioDevice);

        SetState(RecordState.Recording);

        try
        {
            await _captureService.StartAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"녹화 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetState(RecordState.Idle);
        }
    }

    private async Task StopRecordingAsync()
    {
        StatusText.Text = "인코딩 중...";
        StatusDot.Fill = ColorBrush("#F39C12");

        string? completedPath = null;
        try
        {
            completedPath = await _captureService!.StopAsync();
            SetState(RecordState.Idle);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"인코딩 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetState(RecordState.Idle);
        }
        finally
        {
            _captureService?.Dispose();
            _captureService = null;
        }

        if (completedPath is not null)
        {
            var fileSize = new FileInfo(completedPath).Length;
            var sizeText = fileSize switch
            {
                >= 1024 * 1024 => $"{fileSize / (1024.0 * 1024.0):F1} MB",
                >= 1024        => $"{fileSize / 1024.0:F1} KB",
                _              => $"{fileSize} B"
            };

            // 경로를 클립보드에 자동 복사
            try { System.Windows.Clipboard.SetText(completedPath); } catch { }

            var result = System.Windows.MessageBox.Show(
                $"녹화 완료!\n{completedPath}\n파일 크기: {sizeText}\n\n경로가 클립보드에 복사되었습니다.\n\n[예] 파일 열기  |  [아니요] 폴더 열기  |  [취소] 닫기",
                "Screen.Recorder", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(completedPath) { UseShellExecute = true });
            else if (result == MessageBoxResult.No)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{completedPath}\"") { UseShellExecute = true });
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_state == RecordState.Recording)
        {
            _captureService?.Pause();
            SetState(RecordState.Paused);
        }
        else if (_state == RecordState.Paused)
        {
            _captureService?.Resume();
            SetState(RecordState.Recording);
        }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_state is RecordState.Recording or RecordState.Paused)
            await StopRecordingAsync();
    }

    private void SetState(RecordState state)
    {
        _state = state;

        switch (state)
        {
            case RecordState.Idle:
                RecordBtn.Content = "⏺ 녹화 시작";
                RecordBtn.IsEnabled = _regionSelected;
                RecordBtn.Visibility = Visibility.Visible;
                PauseBtn.Visibility = Visibility.Collapsed;
                StopBtn.Visibility = Visibility.Collapsed;
                SelectRegionBtn.IsEnabled = true;
                FullScreenBtn.IsEnabled = true;
                LastRegionBtn.IsEnabled = _settings.LastRegionWidth > 0;
                FormatCombo.IsEnabled = true;
                FpsCombo.IsEnabled = true;
                StatusText.Text = "대기 중";
                StatusDot.Fill = ColorBrush("#555555");
                _stopwatch.Reset();
                _timer.Stop();
                TimerText.Text = "00:00";
                _hud?.Close();
                _hud = null;
                break;

            case RecordState.Countdown:
                RecordBtn.Content = "✕ 취소";
                RecordBtn.IsEnabled = true;
                RecordBtn.Visibility = Visibility.Visible;
                PauseBtn.Visibility = Visibility.Collapsed;
                StopBtn.Visibility = Visibility.Collapsed;
                SelectRegionBtn.IsEnabled = false;
                FullScreenBtn.IsEnabled = false;
                LastRegionBtn.IsEnabled = false;
                FormatCombo.IsEnabled = false;
                FpsCombo.IsEnabled = false;
                StatusDot.Fill = ColorBrush("#F39C12");
                break;

            case RecordState.Recording:
                RecordBtn.Visibility = Visibility.Collapsed;
                PauseBtn.Content = "⏸";
                PauseBtn.Visibility = Visibility.Visible;
                StopBtn.Visibility = Visibility.Visible;
                SelectRegionBtn.IsEnabled = false;
                FullScreenBtn.IsEnabled = false;
                LastRegionBtn.IsEnabled = false;
                FormatCombo.IsEnabled = false;
                FpsCombo.IsEnabled = false;
                StatusText.Text = "녹화 중";
                StatusDot.Fill = ColorBrush("#E74C3C");
                _stopwatch.Start();
                _timer.Start();
                // HUD 없으면 생성
                if (_hud is null)
                {
                    _hud = new HudOverlay();
                    _hud.PauseRequested += () => Pause_Click(this, new RoutedEventArgs());
                    _hud.StopRequested  += async () => await Dispatcher.InvokeAsync(async () =>
                    {
                        if (_state is RecordState.Recording or RecordState.Paused)
                            await StopRecordingAsync();
                    });
                    _hud.Show();
                }
                _hud.UpdateState(isRecording: true, "00:00");
                break;

            case RecordState.Paused:
                PauseBtn.Content = "▶";
                StatusText.Text = "일시정지";
                StatusDot.Fill = ColorBrush("#F39C12");
                _hud?.UpdateState(isRecording: false, TimerText.Text);
                _stopwatch.Stop();
                break;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_state is RecordState.Recording or RecordState.Paused)
        {
            var result = System.Windows.MessageBox.Show(
                "녹화가 진행 중입니다. 창을 닫으면 현재 녹화 내용이 저장되지 않습니다.\n\n정말 종료하시겠습니까?",
                "Screen.Recorder", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource is not null)
        {
            UnregisterHotKey(_hwndSource.Handle, HotkeyRecord);
            UnregisterHotKey(_hwndSource.Handle, HotkeyPause);
            UnregisterHotKey(_hwndSource.Handle, HotkeyStop);
        }
        _captureService?.Dispose();
        _hud?.Close();
        _timer.Stop();
        base.OnClosed(e);
    }
}
