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
    private readonly HistoryService  _history = new();
    private readonly SettingsService _settings = new();
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
        LoadSettings();
        RefreshHistory();
        await InitWebViewAsync();
    }

    // ── 설정 로드/저장 ───────────────────────────────────────────────────

    // 경로 TextBox 끝부분(폴더명)이 보이도록 캐럿을 끝으로 이동
    private void SetFolderPath(string path)
    {
        TxtOutputFolder.Text       = path;
        TxtOutputFolder.CaretIndex = path.Length;
    }

    private void LoadSettings()
    {
        var s = _settings.Current;

        // 뷰포트
        foreach (ComboBoxItem item in CboViewport.Items)
        {
            if (item.Tag is string tag && tag == s.ViewportWidth.ToString())
            {
                CboViewport.SelectedItem = item;
                break;
            }
        }
        TxtCustomWidth.Text = s.ViewportWidth.ToString();

        // 딜레이
        foreach (ComboBoxItem item in CboDelay.Items)
        {
            if (item.Tag is string tag && tag == s.DelayMs.ToString())
            {
                CboDelay.SelectedItem = item;
                break;
            }
        }

        ChkPdf.IsChecked         = s.CapturePdf;
        ChkHidePreview.IsChecked = s.HidePreview;
        SetFolderPath(s.OutputFolder);
    }

    private void SaveSettings()
    {
        var s = _settings.Current;
        var vs = BuildSettings(string.Empty);
        s.ViewportWidth = vs.ViewportWidth;
        s.DelayMs       = vs.DelayMs;
        s.CapturePdf    = ChkPdf.IsChecked.GetValueOrDefault();
        s.HidePreview   = ChkHidePreview.IsChecked.GetValueOrDefault();
        s.OutputFolder  = TxtOutputFolder.Text;
        _settings.Save();
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
            _capture  = new CaptureService(WebView);
            _batchSvc = new BatchService(_capture, _history);
            _batchSvc.Progress    += OnBatchProgress;
            _batchSvc.ItemFailed  += OnBatchItemFailed;
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

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.GetText()?.Trim() ?? string.Empty;
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            TxtUrl.Text = text;
            TxtUrl.CaretIndex = text.Length;
            SetStatus("클립보드에서 URL을 붙여넣었습니다.");
        }
        else
        {
            SetStatus("클립보드에 유효한 URL이 없습니다.");
        }
    }

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
            WebView.Visibility    = Visibility.Visible;
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
            SaveSettings();

            SetStatus($"저장 완료: {Path.GetFileName(filePath)}");
            TxtSavedPath.Text       = filePath;
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
        try
        {
            int success = await _batchSvc.RunAsync(dlg.FileName, settings);
            var failed  = _batchSvc.FailedUrls;
            if (failed.Count > 0)
            {
                var summary = string.Join("\n", failed.Select(u => $"  • {u}"));
                MessageBox.Show(
                    $"{success}개 성공 / {failed.Count}개 실패:\n\n{summary}",
                    "배치 완료", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            SetStatus($"배치 완료: {success}개 성공 / {failed.Count}개 실패");
        }
        catch (Exception ex)
        {
            SetStatus($"배치 오류: {ex.Message}");
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            BtnBatch.IsEnabled   = true;
            BtnCapture.IsEnabled = true;
            RefreshHistory();
        }
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
            WebView.Visibility    = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private void BtnChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "캡처 저장 폴더 선택",
            InitialDirectory = TxtOutputFolder.Text
        };
        if (dlg.ShowDialog() != true) return;
        SetFolderPath(dlg.FolderName);
        _settings.Current.OutputFolder = dlg.FolderName;
        _settings.Save();
    }

    private CaptureSettings BuildSettings(string url)
    {
        var tag = ((CboViewport.SelectedItem as ComboBoxItem)?.Tag as string) ?? "1280";
        int width = tag == "0"
            ? (int.TryParse(TxtCustomWidth.Text, out var w) && w is >= 320 and <= 7680 ? w : 1280)
            : int.Parse(tag);

        var delayTag = ((CboDelay.SelectedItem as ComboBoxItem)?.Tag as string) ?? "1000";
        int delay = int.TryParse(delayTag, out var d) ? d : 1000;

        var outputFolder = TxtOutputFolder.Text is { Length: > 0 } f ? f : CaptureSettings.DefaultOutputFolder;

        return new CaptureSettings
        {
            Url           = url,
            ViewportWidth = width,
            DelayMs       = delay,
            CapturePdf    = ChkPdf.IsChecked.GetValueOrDefault(),
            OutputFolder  = outputFolder
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

        // URL 재사용
        if (LstHistory.SelectedItem is HistoryEntry entry)
            TxtUrl.Text = entry.Url;

        // PNG 미리보기
        ShowPreview(LstHistory.SelectedItem as HistoryEntry);
    }

    private void ShowPreview(HistoryEntry? entry)
    {
        if (entry is null || entry.FileType != "png" || !File.Exists(entry.FilePath))
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
            ImgPreview.Source       = null;
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(entry.FilePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 272; // 패널 너비 맞춤
            bmp.EndInit();
            bmp.Freeze();
            ImgPreview.Source       = bmp;
            PreviewPanel.Visibility = Visibility.Visible;
        }
        catch
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
        }
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
        PreviewPanel.Visibility = Visibility.Collapsed;
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("히스토리를 모두 지우시겠습니까?", "Web.Shot",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _history.Clear();
        RefreshHistory();
        PreviewPanel.Visibility = Visibility.Collapsed;
    }

    private void RefreshHistory()
    {
        var selected = LstHistory.SelectedItem as HistoryEntry;
        LstHistory.ItemsSource = null;
        LstHistory.ItemsSource = _history.Entries;

        // 선택 상태 복원
        if (selected is not null)
        {
            var restored = _history.Entries.FirstOrDefault(e => e.FilePath == selected.FilePath);
            if (restored is not null)
                LstHistory.SelectedItem = restored;
        }
    }

    // ── 도움말 ───────────────────────────────────────────────────────────

    private void BtnHelp_Click(object sender, RoutedEventArgs e) => ShowHelp();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F1)
            ShowHelp();
    }

    private static void ShowHelp()
    {
        MessageBox.Show(
            "📷  Web.Shot — 단축키 & 사용법\n" +
            "────────────────────────────────\n" +
            "Enter          URL 캡처 실행\n" +
            "F1             이 도움말 표시\n" +
            "\n" +
            "──  기능  ───────────────────────\n" +
            "📋  클립보드 URL을 입력창에 붙여넣기\n" +
            "📷  현재 URL 전체 페이지 캡처 (PNG)\n" +
            "⚡  TXT 파일의 URL 목록 일괄 캡처\n" +
            "📁  캡처 저장 폴더 열기\n" +
            "🕐  히스토리 패널 토글\n" +
            "\n" +
            "──  옵션  ───────────────────────\n" +
            "뷰포트     캡처 시 브라우저 너비\n" +
            "딜레이     페이지 렌더링 대기 시간\n" +
            "PDF 저장  PNG 대신 PDF로 캡처\n" +
            "저장 폴더  [변경] 버튼으로 경로 설정\n" +
            "\n" +
            "──  히스토리 패널  ──────────────\n" +
            "항목 클릭  URL 재사용 + PNG 미리보기\n" +
            "📂 열기   파일 탐색기에서 파일 열기\n" +
            "🗑 삭제   히스토리 항목 제거\n" +
            "\n" +
            "TXT 배치 파일 형식:\n" +
            "  https://example.com\n" +
            "  https://another.com\n" +
            "  # 주석은 #으로 시작",
            "Web.Shot 도움말",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── 기타 UI ──────────────────────────────────────────────────────────

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _settings.Current.OutputFolder;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void TxtSavedPath_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSavedPath) && File.Exists(_lastSavedPath))
            Process.Start(new ProcessStartInfo(_lastSavedPath) { UseShellExecute = true });
    }

    private void SetStatus(string message)
        => TxtStatus.Text = $"[{DateTime.Now:HH:mm:ss}]  {message}";
}
