using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Win32;
using WebShot.Models;
using WebShot.Services;

namespace WebShot;

public partial class MainWindow : Window
{
    // ── Win32 다크 타이틀바 ──────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ── 서비스 ──────────────────────────────────────────────────────────
    private CaptureService? _capture;
    private readonly HistoryService _history = new();
    private BatchService?  _batchSvc;
    private bool           _webViewReady;
    private bool           _capturing;
    private string         _lastSavedPath = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        ApplyDarkTitleBar();
        Loaded += OnLoaded;
    }

    private void ApplyDarkTitleBar()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        int dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshHistory();
        await InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WebShot", "WebView2");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                null, userDataFolder);
            await WebView.EnsureCoreWebView2Async(env);
            _webViewReady = true;
            _capture = new CaptureService(WebView);
            _batchSvc = new BatchService(_capture, _history);
            _batchSvc.Progress += OnBatchProgress;
            _batchSvc.ItemFailed += OnBatchItemFailed;
            SetStatus("준비 완료");
        }
        catch (Exception ex)
        {
            SetStatus($"WebView2 초기화 실패: {ex.Message}");
        }
    }

    // ── URL 입력 / 캡처 ──────────────────────────────────────────────────

    private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtUrlPlaceholder != null)
            TxtUrlPlaceholder.Visibility = string.IsNullOrEmpty(TxtUrl.Text)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _ = CaptureAsync();
    }

    private void BtnCapture_Click(object sender, RoutedEventArgs e)
        => _ = CaptureAsync();

    private async Task CaptureAsync()
    {
        if (!_webViewReady || _capturing || _capture is null) return;

        var url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            SetStatus("URL을 입력하세요.");
            return;
        }

        // http(s) 없으면 자동 추가
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        _capturing = true;
        BtnCapture.IsEnabled = false;
        SetStatus("캡처 중...");
        TxtSavedPath.Visibility = Visibility.Collapsed;

        // 미리보기 표시
        if (!ChkHidePreview.IsChecked.GetValueOrDefault())
        {
            EmptyState.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }

        try
        {
            var settings = BuildSettings(url);
            string filePath;

            if (ChkPdf.IsChecked.GetValueOrDefault())
                filePath = await _capture.CaptureFullPagePdfAsync(settings);
            else
                filePath = await _capture.CaptureFullPagePngAsync(settings);

            _lastSavedPath = filePath;
            _history.Add(new HistoryEntry
            {
                Url           = url,
                FilePath      = filePath,
                FileType      = ChkPdf.IsChecked.GetValueOrDefault() ? "pdf" : "png",
                ViewportWidth = settings.ViewportWidth
            });
            RefreshHistory();

            SetStatus($"저장 완료: {Path.GetFileName(filePath)}");
            TxtSavedPath.Text = filePath;
            TxtSavedPath.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SetStatus($"캡처 실패: {ex.Message}");
        }
        finally
        {
            _capturing = false;
            BtnCapture.IsEnabled = true;
        }
    }

    // ── 배치 캡처 ────────────────────────────────────────────────────────

    private async void BtnBatch_Click(object sender, RoutedEventArgs e)
    {
        if (!_webViewReady || _batchSvc is null) return;

        var dlg = new OpenFileDialog
        {
            Title  = "URL 목록 TXT 파일 선택",
            Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        BtnBatch.IsEnabled    = false;
        BtnCapture.IsEnabled  = false;
        ProgressPanel.Visibility = Visibility.Visible;

        var settings = BuildSettings(string.Empty);
        int success = await _batchSvc.RunAsync(dlg.FileName, settings);

        ProgressPanel.Visibility = Visibility.Collapsed;
        BtnBatch.IsEnabled   = true;
        BtnCapture.IsEnabled = true;
        RefreshHistory();
        SetStatus($"배치 완료: {success}개 캡처 저장");
    }

    private void BtnCancelBatch_Click(object sender, RoutedEventArgs e)
        => _batchSvc?.Cancel();

    private void OnBatchProgress(int current, int total, string url)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProgressLabel.Text = $"({current}/{total}) {url}";
            PrgBatch.Value = (double)current / total * 100;
        });
    }

    private void OnBatchItemFailed(string url, Exception ex)
    {
        Dispatcher.Invoke(() => SetStatus($"실패: {url} — {ex.Message}"));
    }

    // ── 옵션 ─────────────────────────────────────────────────────────────

    private void CboViewport_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var tag = ((CboViewport.SelectedItem as ComboBoxItem)?.Tag as string) ?? "1280";
        TxtCustomWidth.Visibility = tag == "0" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChkHidePreview_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ChkHidePreview.IsChecked.GetValueOrDefault())
        {
            WebView.Visibility  = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private CaptureSettings BuildSettings(string url)
    {
        var tag = ((CboViewport.SelectedItem as ComboBoxItem)?.Tag as string) ?? "1280";
        int width = tag == "0"
            ? (int.TryParse(TxtCustomWidth.Text, out var w) ? w : 1280)
            : int.Parse(tag);

        var delayTag = ((CboDelay.SelectedItem as ComboBoxItem)?.Tag as string) ?? "1500";
        int delay = int.TryParse(delayTag, out var d) ? d : 1500;

        return new CaptureSettings
        {
            Url           = url,
            ViewportWidth = width,
            DelayMs       = delay,
            CapturePdf    = ChkPdf.IsChecked.GetValueOrDefault()
        };
    }

    // ── 히스토리 ─────────────────────────────────────────────────────────

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        HistoryPanel.Visibility = HistoryPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasItem = LstHistory.SelectedItem is HistoryEntry;
        BtnOpenFile.IsEnabled    = hasItem;
        BtnDeleteEntry.IsEnabled = hasItem;
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (LstHistory.SelectedItem is not HistoryEntry entry) return;
        if (File.Exists(entry.FilePath))
            Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
        else
            SetStatus("파일을 찾을 수 없습니다.");
    }

    private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (LstHistory.SelectedItem is not HistoryEntry entry) return;
        _history.Remove(entry);
        RefreshHistory();
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("히스토리를 모두 지우시겠습니까?", "Web.Shot",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _history.Clear();
        RefreshHistory();
    }

    private void RefreshHistory()
    {
        LstHistory.ItemsSource = null;
        LstHistory.ItemsSource = _history.Entries;
    }

    // ── 기타 UI ──────────────────────────────────────────────────────────

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = CaptureSettings.DefaultOutputFolder;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void TxtSavedPath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSavedPath) && File.Exists(_lastSavedPath))
            Process.Start(new ProcessStartInfo(_lastSavedPath) { UseShellExecute = true });
    }

    private void SetStatus(string message)
        => TxtStatus.Text = $"[{DateTime.Now:HH:mm:ss}]  {message}";
}
