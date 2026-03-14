using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImgCompare.Services;

namespace ImgCompare;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private WriteableBitmap? _wbA, _wbB;
    private string? _pathA, _pathB;
    private double _zoom = 1.0;
    private bool _syncScrolling = false;

    static readonly string[] ImgExts = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".gif"];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        AmplifySlider.ValueChanged += (_, ev) => AmplifyText.Text = $"×{ev.NewValue:F0}";
        AmplifyText.Text = $"×{AmplifySlider.Value:F0}";

        // 와이프 캔버스 크기 바인딩
        WipeCanvas.SizeChanged += (_, _) =>
        {
            WipeLine.Height = WipeCanvas.ActualHeight;
        };

        StatusBar.Text = "이미지 A/B를 드래그 앤 드롭하거나 버튼으로 열어주세요.";
    }

    // ─── 파일 열기 ────────────────────────────────────────────────────
    void BtnOpenA_Click(object sender, RoutedEventArgs e) => OpenImage(true);
    void BtnOpenB_Click(object sender, RoutedEventArgs e) => OpenImage(false);

    void OpenImage(bool isA)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "이미지|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.gif|모든 파일|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        LoadImage(dlg.FileName, isA);
    }

    void LoadImage(string path, bool isA)
    {
        try
        {
            var wb = ImageMetricsCalculator.LoadToWriteable(path);
            var fileInfo = new FileInfo(path);
            string name = System.IO.Path.GetFileName(path);
            long sizeKb = fileInfo.Length / 1024;

            if (isA)
            {
                _wbA = wb; _pathA = path;
                ImgA.Source = ApplyZoom(wb);
                ImgWipeA.Source = wb;
                LabelA.Text = $"A: {name} ({wb.PixelWidth}×{wb.PixelHeight}, {sizeKb}KB)";
            }
            else
            {
                _wbB = wb; _pathB = path;
                ImgB.Source = ApplyZoom(wb);
                ImgWipeB.Source = wb;
                LabelB.Text = $"B: {name} ({wb.PixelWidth}×{wb.PixelHeight}, {sizeKb}KB)";
            }

            UpdateResolutionInfo();
            StatusBar.Text = $"로드: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"이미지 로드 오류: {ex.Message}", "Img.Compare", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void UpdateResolutionInfo()
    {
        if (_wbA != null && _wbB != null)
        {
            ResolutionText.Text = $"A: {_wbA.PixelWidth}×{_wbA.PixelHeight}\nB: {_wbB.PixelWidth}×{_wbB.PixelHeight}";
            long szA = _pathA != null ? new FileInfo(_pathA).Length / 1024 : 0;
            long szB = _pathB != null ? new FileInfo(_pathB).Length / 1024 : 0;
            FileSizeText.Text = $"A: {szA}KB  B: {szB}KB";
        }
    }

    // ─── 비교 ─────────────────────────────────────────────────────────
    void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        if (_wbA == null || _wbB == null)
        {
            MessageBox.Show("이미지 A와 B를 모두 열어주세요.", "Img.Compare"); return;
        }

        var metrics = ImageMetricsCalculator.Calculate(_wbA, _wbB);

        MseText.Text = $"{metrics.Mse:F2}";
        PsnrText.Text = double.IsInfinity(metrics.Psnr) ? "∞" : $"{metrics.Psnr:F2}";
        SsimText.Text = $"{metrics.Ssim:F4}";
        MaeText.Text = $"{metrics.Mae:F2}";
        DiffPctText.Text = $"{metrics.DiffPercent:F2}%";

        // SSIM 색상 강조
        SsimText.Foreground = metrics.Ssim >= 0.99 ? new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A))
            : metrics.Ssim >= 0.95 ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F))
            : new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));

        StatusBar.Text = $"SSIM: {metrics.Ssim:F4}  |  PSNR: {(double.IsInfinity(metrics.Psnr) ? "∞" : metrics.Psnr.ToString("F2"))}dB  |  다른 픽셀: {metrics.DiffPercent:F2}%";
    }

    // ─── 히트맵 ───────────────────────────────────────────────────────
    void BtnHeatmap_Click(object sender, RoutedEventArgs e)
    {
        if (_wbA == null || _wbB == null)
        {
            MessageBox.Show("이미지 A와 B를 모두 열어주세요.", "Img.Compare"); return;
        }

        double amp = AmplifySlider.Value;
        var heatmap = ImageMetricsCalculator.BuildDiffHeatmap(_wbA, _wbB, amp);
        ImgHeatmap.Source = heatmap;
        StatusBar.Text = $"히트맵 생성 완료 (증폭 ×{amp:F0})";
    }

    // ─── 줌 ───────────────────────────────────────────────────────────
    BitmapSource ApplyZoom(WriteableBitmap wb)
    {
        if (Math.Abs(_zoom - 1.0) < 0.001) return wb;
        var transform = new ScaleTransform(_zoom, _zoom);
        var tb = new TransformedBitmap(wb, transform);
        return tb;
    }

    void ApplyZoomToImages()
    {
        if (_wbA != null) ImgA.Source = ApplyZoom(_wbA);
        if (_wbB != null) ImgB.Source = ApplyZoom(_wbB);
    }

    void BtnZoomIn_Click(object sender, RoutedEventArgs e) { _zoom = Math.Min(_zoom * 1.25, 8.0); ApplyZoomToImages(); }
    void BtnZoomOut_Click(object sender, RoutedEventArgs e) { _zoom = Math.Max(_zoom / 1.25, 0.1); ApplyZoomToImages(); }
    void BtnZoomReset_Click(object sender, RoutedEventArgs e) { _zoom = 1.0; ApplyZoomToImages(); }
    void BtnFit_Click(object sender, RoutedEventArgs e)
    {
        if (_wbA == null) return;
        double viewW = ScrollA.ViewportWidth;
        _zoom = viewW / _wbA.PixelWidth;
        ApplyZoomToImages();
    }

    // ─── 동기 스크롤 ──────────────────────────────────────────────────
    void ScrollA_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        ScrollB.ScrollToHorizontalOffset(e.HorizontalOffset);
        ScrollB.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

    void ScrollB_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        ScrollA.ScrollToHorizontalOffset(e.HorizontalOffset);
        ScrollA.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

    // ─── 와이프 뷰 ────────────────────────────────────────────────────
    void WipeCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(WipeCanvas);
        double x = pos.X;
        System.Windows.Controls.Canvas.SetLeft(WipeLine, x);

        // 이미지 B의 클립을 x 기준으로 자름
        ImgWipeB.Clip = new RectangleGeometry(new Rect(x, 0, WipeCanvas.ActualWidth - x, WipeCanvas.ActualHeight));
    }

    // ─── 드래그 앤 드롭 ───────────────────────────────────────────────
    void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        var imgs = files.Where(f => ImgExts.Contains(System.IO.Path.GetExtension(f).ToLower())).ToList();
        if (imgs.Count >= 1 && _wbA == null) LoadImage(imgs[0], true);
        else if (imgs.Count >= 1) LoadImage(imgs[0], imgs.Count == 1 ? _wbA == null : true);
        if (imgs.Count >= 2) LoadImage(imgs[1], false);
    }

    // ─── 픽셀 정보 ────────────────────────────────────────────────────
    void Img_MouseDown(object sender, MouseButtonEventArgs e) { }
    void Img_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is Image img && img.Source is BitmapSource bmp)
        {
            var pos = e.GetPosition(img);
            int px = (int)(pos.X / _zoom), py = (int)(pos.Y / _zoom);
            if (px >= 0 && py >= 0 && px < bmp.PixelWidth && py < bmp.PixelHeight)
            {
                var cropped = new CroppedBitmap(bmp, new Int32Rect(px, py, 1, 1));
                var arr = new byte[4];
                cropped.CopyPixels(arr, 4, 0);
                string which = img == ImgA ? "A" : "B";
                StatusBar.Text = $"[{which}] ({px}, {py})  R:{arr[2]} G:{arr[1]} B:{arr[0]}";
            }
        }
    }
}
