using Microsoft.Win32;

namespace DeepDiff.Views;

public partial class ImageCompareView : UserControl
{
    private readonly MainWindow _main;
    private readonly ImageDiffService _svc = new();

    private double _zoom = 1.0;
    private ImageDiffService.ImageDiffResult? _result;
    private string _viewMode = "SideBySide";

    public ImageCompareView(MainWindow main, string? leftPath = null, string? rightPath = null)
    {
        _main = main;
        InitializeComponent();

        if (leftPath  != null) TxtLeftPath.Text  = leftPath;
        if (rightPath != null) TxtRightPath.Text = rightPath;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(TxtLeftPath.Text) || !string.IsNullOrEmpty(TxtRightPath.Text))
                RunCompare();
        };
    }

    private async void RunCompare()
    {
        string left  = TxtLeftPath.Text.Trim();
        string right = TxtRightPath.Text.Trim();
        TbStatus.Text = "이미지 로드 중...";

        _result = await Task.Run(() => _svc.Compare(left, right));
        UpdateView();
        // 레이아웃 완료 후 맞춤 적용
        await Dispatcher.InvokeAsync(() => BtnZoomFit_Click(this, new RoutedEventArgs()),
            System.Windows.Threading.DispatcherPriority.Loaded);

        if (_result.LeftImage != null)
            TbLeftSize.Text = $"왼쪽: {_result.LeftWidth}×{_result.LeftHeight}";
        if (_result.RightImage != null)
            TbRightSize.Text = $"오른쪽: {_result.RightWidth}×{_result.RightHeight}";

        if (_result.DiffImage != null)
        {
            TbDiffStats.Text = $"차이: {_result.DiffPixels:N0}px ({_result.DiffPercent:F2}%)";
            TbStatus.Text = _result.DiffPercent < 0.01 ? "이미지 동일" : $"총 {_result.TotalPixels:N0}픽셀 중 {_result.DiffPixels:N0}개 차이";
        }
        else TbStatus.Text = _result.LeftImage == null || _result.RightImage == null ? "이미지 로드 실패" : "비교 완료";
    }

    private void UpdateView()
    {
        if (_result == null) return;
        ApplyZoom(_zoom, false);

        switch (_viewMode)
        {
            case "SideBySide":
                SideBySidePanel.Visibility = Visibility.Visible;
                SinglePanel.Visibility     = Visibility.Collapsed;
                LeftImage.Source  = _result.LeftImage;
                RightImage.Source = _result.RightImage;
                break;

            case "Overlay":
                SideBySidePanel.Visibility = Visibility.Collapsed;
                SinglePanel.Visibility     = Visibility.Visible;
                SingleImage.Source = _result.LeftImage; // TODO: blend
                break;

            case "PixelDiff":
                SideBySidePanel.Visibility = Visibility.Collapsed;
                SinglePanel.Visibility     = Visibility.Visible;
                SingleImage.Source = _result.DiffImage ?? _result.LeftImage;
                break;
        }
    }

    private void ViewMode_Changed(object s, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _viewMode = RbSideBySide.IsChecked == true ? "SideBySide"
                  : RbOverlay.IsChecked    == true ? "Overlay"
                  : "PixelDiff";
        UpdateView();
    }

    private void BtnZoomIn_Click(object s, RoutedEventArgs e)  => ApplyZoom(_zoom * 1.25, true);
    private void BtnZoomOut_Click(object s, RoutedEventArgs e) => ApplyZoom(_zoom / 1.25, true);
    private void BtnZoom100_Click(object s, RoutedEventArgs e) => ApplyZoom(1.0, true);
    private void BtnZoomFit_Click(object s, RoutedEventArgs e)
    {
        if (_result?.LeftImage == null) return;
        double fitZ = Math.Min(
            MainScroll.ActualWidth  / _result.LeftWidth,
            MainScroll.ActualHeight / _result.LeftHeight) * 0.95;
        ApplyZoom(Math.Max(0.05, fitZ), true);
    }

    private void ApplyZoom(double z, bool updateImages)
    {
        _zoom = Math.Clamp(z, 0.05, 20.0);
        TbZoom.Text = $"{_zoom:P0}";

        var scale = new ScaleTransform(_zoom, _zoom);
        LeftImage.LayoutTransform  = scale;
        RightImage.LayoutTransform = scale;
        SingleImage.LayoutTransform = scale;
    }

    private void BtnLeftBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickImage(TxtLeftPath.Text);
        if (path != null) { TxtLeftPath.Text = path; RunCompare(); }
    }

    private void BtnRightBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickImage(TxtRightPath.Text);
        if (path != null) { TxtRightPath.Text = path; RunCompare(); }
    }

    private void BtnSwap_Click(object s, RoutedEventArgs e)
    {
        (TxtLeftPath.Text, TxtRightPath.Text) = (TxtRightPath.Text, TxtLeftPath.Text);
        RunCompare();
    }

    private void BtnCompare_Click(object s, RoutedEventArgs e) => RunCompare();
    private void TxtPath_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) RunCompare(); }

    private void UserControl_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Add || e.Key == Key.OemPlus) ApplyZoom(_zoom * 1.25, true);
        else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) ApplyZoom(_zoom / 1.25, true);
        else if (e.Key == Key.F) BtnZoomFit_Click(s, e);
        else if (e.Key == Key.Tab)
        {
            if (RbSideBySide.IsChecked == true) RbOverlay.IsChecked = true;
            else if (RbOverlay.IsChecked == true) RbPixelDiff.IsChecked = true;
            else RbSideBySide.IsChecked = true;
            e.Handled = true;
        }
    }

    private static string? PickImage(string? initial)
    {
        var dlg = new OpenFileDialog
        {
            Title = "이미지 파일 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp|모든 파일|*.*"
        };
        if (!string.IsNullOrEmpty(initial) && File.Exists(initial))
            dlg.InitialDirectory = Path.GetDirectoryName(initial);
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
