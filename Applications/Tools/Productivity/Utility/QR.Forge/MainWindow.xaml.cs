using QrForge.Models;
using QrForge.Services;
using QrForge.Windows;
using SkiaSharp;
using System.IO;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using ZXing.QrCode.Internal;

namespace QrForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private SKBitmap?                _qrBitmap;
    private readonly QrStyle         _style      = new();
    private CancellationTokenSource? _generateCts;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        // 초기 색상 박스 업데이트
        UpdateColorBoxes();
        // 초기 QR 생성
        GenerateQr();
    }

    // ── 탭 전환 ─────────────────────────────────────────────────────────────
    private void TabInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        PnlUrl.Visibility   = Visibility.Collapsed;
        PnlText.Visibility  = Visibility.Collapsed;
        PnlVCard.Visibility = Visibility.Collapsed;
        PnlWifi.Visibility  = Visibility.Collapsed;

        var tag = (TabInput.SelectedItem as TabItem)?.Tag?.ToString();
        switch (tag)
        {
            case "Url":   PnlUrl.Visibility   = Visibility.Visible; break;
            case "Text":  PnlText.Visibility  = Visibility.Visible; break;
            case "VCard": PnlVCard.Visibility = Visibility.Visible; break;
            case "WiFi":  PnlWifi.Visibility  = Visibility.Visible; break;
        }

        GenerateQr();
    }

    // ── 입력 변경 이벤트 ─────────────────────────────────────────────────────
    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        GenerateQr();
    }

    private void Input_Changed_Combo(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        GenerateQr();
    }

    private void Style_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        _style.Marker = CmbMarker.SelectedIndex switch
        {
            1 => MarkerStyle.Round,
            2 => MarkerStyle.Dot,
            _ => MarkerStyle.Square
        };

        _style.EcLevel = CmbEcLevel.SelectedIndex switch
        {
            0 => ErrorCorrectionLevel.L,
            1 => ErrorCorrectionLevel.M,
            2 => ErrorCorrectionLevel.Q,
            _ => ErrorCorrectionLevel.H
        };

        GenerateQr();
    }

    // ── QR 생성 ──────────────────────────────────────────────────────────────
    // UI 차단 방지: QrService.Render()는 CPU-bound (ZXing + SkiaSharp 512×512 픽셀 루프) → Task.Run
    // 디바운스: 키입력마다 재렌더링 방지 (150ms 대기 후 마지막 요청만 실행)
    private async void GenerateQr()
    {
        if (!IsLoaded) return;

        // 이전 요청 취소 후 150ms 디바운스 대기
        _generateCts?.Cancel();
        _generateCts = new CancellationTokenSource();
        var cts = _generateCts;

        try { await Task.Delay(150, cts.Token); }
        catch (OperationCanceledException) { return; }

        var content = BuildContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            _qrBitmap?.Dispose();
            _qrBitmap = null;
            SkiaView.InvalidateVisual();
            return;
        }

        // 스타일 스냅샷: 배경 스레드와 UI 스레드 동시 접근 방지
        var styleCopy = new QrStyle
        {
            ForeColor = _style.ForeColor,
            BackColor = _style.BackColor,
            Marker    = _style.Marker,
            EcLevel   = _style.EcLevel,
            LogoPath  = _style.LogoPath
        };
        if (!string.IsNullOrEmpty(styleCopy.LogoPath))
            styleCopy.EcLevel = ErrorCorrectionLevel.H;

        SKBitmap? bmp;
        try
        {
            bmp = await Task.Run(() => QrService.Render(content, styleCopy), cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch { return; }

        if (cts.IsCancellationRequested) { bmp?.Dispose(); return; }

        _qrBitmap?.Dispose();
        _qrBitmap = bmp;
        SkiaView.InvalidateVisual();
    }

    private string BuildContent()
    {
        var tag = (TabInput.SelectedItem as TabItem)?.Tag?.ToString();
        return tag switch
        {
            "Url"   => TxtUrl.Text.Trim(),
            "Text"  => TxtText.Text,
            "VCard" => new VCardData
            {
                Name    = TxtVName.Text.Trim(),
                Phone   = TxtVPhone.Text.Trim(),
                Email   = TxtVEmail.Text.Trim(),
                Company = TxtVCompany.Text.Trim(),
                Url     = TxtVUrl.Text.Trim()
            }.ToVCardString(),
            "WiFi" => new WiFiData
            {
                SSID     = TxtWifiSsid.Text.Trim(),
                Password = TxtWifiPass.Text.Trim(),
                Crypto   = CmbWifiEnc.SelectedIndex switch
                {
                    1 => WifiEncryption.WEP,
                    2 => WifiEncryption.None,
                    _ => WifiEncryption.WPA
                }
            }.ToWifiString(),
            _ => string.Empty
        };
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateQr();

    // ── Skia 그리기 ──────────────────────────────────────────────────────────
    private void SkiaView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x1A, 0x1A, 0x2E));

        if (_qrBitmap == null) return;

        var dest = new SKRect(0, 0, e.Info.Width, e.Info.Height);
        canvas.DrawBitmap(_qrBitmap, dest);
    }

    // ── 색상 선택 ────────────────────────────────────────────────────────────
    private void FgColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var color = PickColor(_style.ForeColor);
        if (color.HasValue)
        {
            _style.ForeColor = color.Value;
            UpdateColorBoxes();
            GenerateQr();
        }
    }

    private void BgColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var color = PickColor(_style.BackColor);
        if (color.HasValue)
        {
            _style.BackColor = color.Value;
            UpdateColorBoxes();
            GenerateQr();
        }
    }

    private static SKColor? PickColor(SKColor current)
    {
        using var dlg = new ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(current.Alpha, current.Red, current.Green, current.Blue),
            FullOpen = true
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;
        return new SKColor(dlg.Color.R, dlg.Color.G, dlg.Color.B, dlg.Color.A);
    }

    private void UpdateColorBoxes()
    {
        FgColorBox.Background = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(_style.ForeColor.Alpha, _style.ForeColor.Red,
                                                _style.ForeColor.Green, _style.ForeColor.Blue));
        BgColorBox.Background = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(_style.BackColor.Alpha, _style.BackColor.Red,
                                                _style.BackColor.Green, _style.BackColor.Blue));
    }

    // ── 로고 ────────────────────────────────────────────────────────────────
    private void BtnLogo_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.webp|모든 파일|*.*",
            Title  = "로고 이미지 선택"
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        _style.LogoPath = dlg.FileName;
        TxtLogoPath.Text = System.IO.Path.GetFileName(dlg.FileName);
        TxtLogoPath.Foreground = System.Windows.Media.Brushes.LightGray;
        // 로고 삽입 시 H 레벨 강제
        CmbEcLevel.SelectedIndex = 3;
        GenerateQr();
    }

    private void BtnLogoRemove_Click(object sender, RoutedEventArgs e)
    {
        _style.LogoPath = string.Empty;
        TxtLogoPath.Text = "(없음)";
        TxtLogoPath.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x70));
        GenerateQr();
    }

    // ── 내보내기 ────────────────────────────────────────────────────────────
    private void BtnPng_Click(object sender, RoutedEventArgs e)
    {
        if (_qrBitmap == null) { ShowNoQr(); return; }

        using var dlg = new SaveFileDialog
        {
            Filter   = "PNG 이미지|*.png",
            FileName = "qrcode.png"
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        ExportService.SavePng(_qrBitmap, dlg.FileName);
        System.Windows.MessageBox.Show("PNG 저장 완료!", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSvg_Click(object sender, RoutedEventArgs e)
    {
        var content = BuildContent();
        if (string.IsNullOrWhiteSpace(content)) { ShowNoQr(); return; }

        using var dlg = new SaveFileDialog
        {
            Filter   = "SVG 파일|*.svg",
            FileName = "qrcode.svg"
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        ExportService.SaveSvg(content, _style, dlg.FileName);
        System.Windows.MessageBox.Show("SVG 저장 완료!", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_qrBitmap == null) { ShowNoQr(); return; }

        using var dlg = new SaveFileDialog
        {
            Filter   = "PDF 파일|*.pdf",
            FileName = "qrcode.pdf"
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var content = BuildContent();
        string label = (TabInput.SelectedItem as TabItem)?.Header?.ToString() ?? "";
        ExportService.SaveSinglePdf(_qrBitmap, label, dlg.FileName);
        System.Windows.MessageBox.Show("PDF 저장 완료!", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_qrBitmap == null) { ShowNoQr(); return; }

        var bytes = ExportService.ToPngBytes(_qrBitmap);
        using var ms  = new System.IO.MemoryStream(bytes);
        var bmpSrc    = System.Windows.Media.Imaging.BitmapFrame.Create(
                            ms, System.Windows.Media.Imaging.BitmapCreateOptions.None,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        System.Windows.Clipboard.SetImage(bmpSrc);
        System.Windows.MessageBox.Show("클립보드에 복사되었습니다!", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBatch_Click(object sender, RoutedEventArgs e)
    {
        var win = new BatchWindow(_style) { Owner = this };
        win.ShowDialog();
    }

    private static void ShowNoQr() =>
        System.Windows.MessageBox.Show("먼저 QR 코드를 생성해주세요.", "QR.Forge",
                                       MessageBoxButton.OK, MessageBoxImage.Warning);
}
