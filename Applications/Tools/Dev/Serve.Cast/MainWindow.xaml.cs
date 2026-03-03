using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ServeCast.Models;
using ServeCast.Services;

namespace ServeCast;

public partial class MainWindow : Window
{
    // ── Win32 ─────────────────────────────────────────────────────────
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int size);

    // ── 상태 ──────────────────────────────────────────────────────────
    private ServerService?                    _server;
    private readonly ObservableCollection<RequestLog> _logs = [];
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

        // 로그 DataGrid 바인딩
        LogGrid.ItemsSource = _logs;

        _initialized = true;
        SetStatus("준비");
    }

    // ── 폴더 선택 ─────────────────────────────────────────────────────
    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description         = "서빙할 폴더를 선택하세요",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (!string.IsNullOrEmpty(TxtFolder.Text) && Directory.Exists(TxtFolder.Text))
            dialog.InitialDirectory = TxtFolder.Text;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtFolder.Text = dialog.SelectedPath;
    }

    // ── 체크박스 토글 ─────────────────────────────────────────────────
    private void ChkCors_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        PanelCors.Visibility = ChkCors.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChkAuth_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        PanelAuth.Visibility = ChkAuth.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChkHttps_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        // 포트 기본값 자동 전환
        if (TxtPort.Text is "8080" or "80" or "443")
            TxtPort.Text = ChkHttps.IsChecked == true ? "443" : "8080";
    }

    // ── 서버 시작 / 정지 ──────────────────────────────────────────────
    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_server is not null)
        {
            await StopServerAsync();
        }
        else
        {
            await StartServerAsync();
        }
    }

    private async Task StartServerAsync()
    {
        // 유효성 검사
        if (string.IsNullOrWhiteSpace(TxtFolder.Text))
        {
            SetStatus("폴더를 먼저 선택해주세요.", error: true);
            return;
        }
        if (!Directory.Exists(TxtFolder.Text))
        {
            SetStatus("선택한 폴더가 존재하지 않습니다.", error: true);
            return;
        }
        if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            SetStatus("유효한 포트 번호를 입력하세요 (1–65535).", error: true);
            return;
        }

        var config = BuildConfig(port);
        SetControlsEnabled(false);
        SetStatus("서버 시작 중…");

        try
        {
            _server = new ServerService(config, OnRequestReceived);
            await _server.StartAsync();

            // 상태 UI 업데이트
            var localUrl = config.LocalUrl;
            var lanIp    = ServerService.GetLocalIp();
            var lanUrl   = $"{config.Scheme}://{lanIp}:{port}";

            TxtLocalUrl.Text = localUrl;
            TxtLanUrl.Text   = lanUrl;

            // QR 코드 (LAN URL 기준)
            try { ImgQr.Source = QrService.Generate(lanUrl); }
            catch { /* QR 생성 실패는 무시 */ }

            PanelStatus.Visibility = Visibility.Visible;
            BtnToggle.Content      = "■  서버 정지";

            SetStatus($"실행 중  ·  {localUrl}");
        }
        catch (Exception ex)
        {
            _server = null;
            SetControlsEnabled(true);
            SetStatus($"시작 실패: {ex.Message}", error: true);
        }
    }

    private async Task StopServerAsync()
    {
        if (_server is null) return;
        SetStatus("서버 정지 중…");
        try
        {
            await _server.StopAsync();
        }
        catch { }
        finally
        {
            _server = null;
        }

        PanelStatus.Visibility = Visibility.Collapsed;
        BtnToggle.Content      = "▶  서버 시작";
        SetControlsEnabled(true);
        SetStatus("준비");
    }

    // ── 요청 수신 콜백 (백그라운드 스레드에서 호출됨) ─────────────────
    private void OnRequestReceived(RequestLog log)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _logs.Add(log);
            TxtLogCount.Text = _logs.Count.ToString();

            if (ChkAutoScroll.IsChecked == true && LogGrid.Items.Count > 0)
                LogGrid.ScrollIntoView(LogGrid.Items[^1]);
        });
    }

    // ── URL 클릭 → 브라우저 열기 ──────────────────────────────────────
    private void TxtUrl_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl(TxtLocalUrl.Text);
    }

    private void TxtLanUrl_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl(TxtLanUrl.Text);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    // ── 로그 지우기 ───────────────────────────────────────────────────
    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
        TxtLogCount.Text = "0";
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────
    private ServerConfig BuildConfig(int port) => new()
    {
        FolderPath    = TxtFolder.Text.Trim(),
        Port          = port,
        UseHttps      = ChkHttps.IsChecked == true,
        EnableCors    = ChkCors.IsChecked  == true,
        CorsOrigins   = TxtCorsOrigins.Text.Trim(),
        EnableAuth    = ChkAuth.IsChecked  == true,
        AuthUsername  = TxtAuthUser.Text.Trim(),
        AuthPassword  = PbAuthPass.Password,
        SpaMode       = ChkSpa.IsChecked   == true,
        ShowDirectory = ChkDir.IsChecked   == true,
    };

    private void SetControlsEnabled(bool enabled)
    {
        TxtFolder.IsEnabled   = enabled;
        TxtPort.IsEnabled     = enabled;
        ChkHttps.IsEnabled    = enabled;
        ChkCors.IsEnabled     = enabled;
        TxtCorsOrigins.IsEnabled = enabled;
        ChkAuth.IsEnabled     = enabled;
        TxtAuthUser.IsEnabled = enabled;
        PbAuthPass.IsEnabled  = enabled;
        ChkSpa.IsEnabled      = enabled;
        ChkDir.IsEnabled      = enabled;
    }

    private void SetStatus(string msg, bool error = false)
    {
        TxtStatus.Text       = msg;
        TxtStatus.Foreground = error
            ? System.Windows.Media.Brushes.Tomato
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x7F, 0x84, 0x9C));
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // App.xaml.cs 에서 닫기를 취소하고 숨김 처리하므로
        // 앱 종료(Shutdown) 경로에서만 서버 정지
        if (_server is not null)
        {
            try { await _server.StopAsync(); } catch { }
        }
        base.OnClosing(e);
    }
}
