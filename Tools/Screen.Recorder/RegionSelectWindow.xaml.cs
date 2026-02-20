using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenRecorder;

public partial class RegionSelectWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDragging;

    /// <summary>사용자가 선택한 화면 영역 (스크린 좌표)</summary>
    public Int32Rect SelectedRegion { get; private set; }

    /// <summary>영역 선택 완료 여부</summary>
    public bool RegionSelected { get; private set; }

    public RegionSelectWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 전체 가상 스크린(다중 모니터 포함) 덮기
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isDragging = true;

        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
        HintLabel.Visibility = Visibility.Collapsed;

        Canvas.SetLeft(SelectionBorder, _startPoint.X);
        Canvas.SetTop(SelectionBorder, _startPoint.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;

        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(OverlayCanvas);

        var x = Math.Min(_startPoint.X, current.X);
        var y = Math.Min(_startPoint.Y, current.Y);
        var w = Math.Abs(current.X - _startPoint.X);
        var h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = w;
        SelectionBorder.Height = h;

        // 크기 표시 (물리 픽셀 기준)
        var dpiPreview = VisualTreeHelper.GetDpi(this);
        SizeText.Text = $"{(int)Math.Round(w * dpiPreview.DpiScaleX)} × {(int)Math.Round(h * dpiPreview.DpiScaleY)}";
        Canvas.SetLeft(SizeLabel, x);
        Canvas.SetTop(SizeLabel, y - 30);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        var current = e.GetPosition(OverlayCanvas);

        var x = (int)Math.Min(_startPoint.X, current.X);
        var y = (int)Math.Min(_startPoint.Y, current.Y);
        var w = (int)Math.Abs(current.X - _startPoint.X);
        var h = (int)Math.Abs(current.Y - _startPoint.Y);

        // 최소 크기 체크 (너무 작으면 무시)
        if (w < 10 || h < 10)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            HintLabel.Visibility = Visibility.Visible;
            return;
        }

        // DPI 스케일 적용 → 물리 픽셀(CopyFromScreen 좌표계)로 변환
        var dpi     = VisualTreeHelper.GetDpi(this);
        var screenX = (int)Math.Round((Left + x) * dpi.DpiScaleX);
        var screenY = (int)Math.Round((Top  + y) * dpi.DpiScaleY);
        var physW   = (int)Math.Round(w * dpi.DpiScaleX);
        var physH   = (int)Math.Round(h * dpi.DpiScaleY);

        // 짝수로 맞추기 (인코더 호환성)
        physW = physW % 2 == 0 ? physW : physW - 1;
        physH = physH % 2 == 0 ? physH : physH - 1;

        SelectedRegion = new Int32Rect(screenX, screenY, physW, physH);
        RegionSelected = true;
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RegionSelected = false;
            DialogResult = false;
            Close();
        }
    }
}
