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

    private enum RecordState { Idle, Recording, Paused }
    private RecordState _state = RecordState.Idle;

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
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        // 메인 윈도우를 잠시 숨기고 영역 선택
        Hide();

        // 약간 지연 후 선택 윈도우 표시 (메인 윈도우 사라짐 대기)
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
            StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F39C12")!);

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
                StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555")!);
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
                StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E74C3C")!);
                _stopwatch.Start();
                _timer.Start();
                break;

            case RecordState.Paused:
                PauseBtn.Content = "▶";
                StatusText.Text = "일시정지";
                StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F39C12")!);
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
