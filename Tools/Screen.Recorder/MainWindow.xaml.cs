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

namespace ScreenRecorder;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly RecordingSettings _settings = RecordingSettings.CreateDefault();
    private ScreenCaptureService? _captureService;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private Int32Rect _selectedRegion;
    private bool _regionSelected;
    private bool _ffmpegAvailable;

    private enum RecordState { Idle, Recording, Paused }
    private RecordState _state = RecordState.Idle;

    private static SolidColorBrush ColorBrush(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        OutputFolderText.Text = _settings.OutputFolder;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) =>
        {
            TimerText.Text = _stopwatch.Elapsed.ToString(@"mm\:ss");
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
        }

        CheckFfmpeg();
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

            var wingetOk = await RunInstallerWithLogAsync(
                "winget", "install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements");

            if (!wingetOk)
            {
                // choco 시도
                UpdateFfmpegLog("winget 실패 → Chocolatey로 재시도 중...");
                FfmpegStatusText.Text = "FFmpeg 설치 중 (Chocolatey)...";

                wingetOk = await RunInstallerWithLogAsync("choco", "install ffmpeg -y");
            }

            if (wingetOk)
            {
                // 설치 후 PATH 갱신을 위해 다시 체크
                _ffmpegAvailable = true;
                FfmpegProgress.IsIndeterminate = false;
                FfmpegProgress.Value = 100;
                FfmpegStatusText.Text = "FFmpeg 설치 완료!";
                FfmpegStatusText.Foreground = ColorBrush("#27AE60");
                UpdateFfmpegLog("설치 성공 — MP4 녹화가 가능합니다");

                await Task.Delay(1500);
                FfmpegPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                FfmpegProgress.IsIndeterminate = false;
                FfmpegProgress.Value = 0;
                FfmpegStatusText.Text = "자동 설치 실패";
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
                RedirectStandardError = true
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

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        Hide();

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var selector = new RegionSelectWindow();
            var result = selector.ShowDialog();

            Show();

            if (result == true && selector.RegionSelected)
            {
                _selectedRegion = selector.SelectedRegion;
                _regionSelected = true;
                RegionInfoText.Text = $"X:{_selectedRegion.X}  Y:{_selectedRegion.Y}  |  {_selectedRegion.Width} × {_selectedRegion.Height}";
                RecordBtn.IsEnabled = true;
            }
        });
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
        }
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
            // MP4 선택인데 FFmpeg 없으면 경고
            if (!IsGifFormat() && !_ffmpegAvailable)
            {
                System.Windows.MessageBox.Show(
                    "MP4 녹화에는 FFmpeg가 필요합니다.\nGIF 형식으로 변경하거나 FFmpeg를 설치해주세요.",
                    "Screen.Recorder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartRecordingAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (!_regionSelected) return;

        var fps = GetSelectedFps();
        var isGif = IsGifFormat();
        var ext = isGif ? "gif" : "mp4";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(_settings.OutputFolder, $"recording_{timestamp}.{ext}");

        Directory.CreateDirectory(_settings.OutputFolder);

        _captureService = new ScreenCaptureService(
            _selectedRegion, fps, outputPath, isGif);

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
        {
            StatusText.Text = "인코딩 중...";
            StatusDot.Fill = ColorBrush("#F39C12");

            try
            {
                var outputPath = await _captureService!.StopAsync();
                SetState(RecordState.Idle);

                var result = System.Windows.MessageBox.Show(
                    $"녹화 완료!\n{outputPath}\n\n파일을 열까요?",
                    "Screen.Recorder", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"인코딩 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                SetState(RecordState.Idle);
            }
        }
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
                FormatCombo.IsEnabled = true;
                FpsCombo.IsEnabled = true;
                StatusText.Text = "대기 중";
                StatusDot.Fill = ColorBrush("#555555");
                _stopwatch.Reset();
                _timer.Stop();
                TimerText.Text = "00:00";
                break;

            case RecordState.Recording:
                RecordBtn.Visibility = Visibility.Collapsed;
                PauseBtn.Content = "⏸";
                PauseBtn.Visibility = Visibility.Visible;
                StopBtn.Visibility = Visibility.Visible;
                SelectRegionBtn.IsEnabled = false;
                FormatCombo.IsEnabled = false;
                FpsCombo.IsEnabled = false;
                StatusText.Text = "녹화 중";
                StatusDot.Fill = ColorBrush("#E74C3C");
                _stopwatch.Start();
                _timer.Start();
                break;

            case RecordState.Paused:
                PauseBtn.Content = "▶";
                StatusText.Text = "일시정지";
                StatusDot.Fill = ColorBrush("#F39C12");
                _stopwatch.Stop();
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _captureService?.Dispose();
        _timer.Stop();
        base.OnClosed(e);
    }
}
