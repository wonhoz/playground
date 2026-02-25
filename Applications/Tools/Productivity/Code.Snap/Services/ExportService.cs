namespace CodeSnap.Services;

public static class ExportService
{
    /// <summary>
    /// 캔버스를 2× DPI 고해상도 PNG로 저장
    /// </summary>
    public static void SavePng(FrameworkElement canvas)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG 이미지|*.png",
            DefaultExt = ".png",
            FileName = "code-snap"
        };
        if (dlg.ShowDialog() != true) return;

        var rtb = RenderCanvas(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Open(dlg.FileName, FileMode.Create);
        encoder.Save(fs);
    }

    /// <summary>
    /// 캔버스를 클립보드에 이미지로 복사
    /// </summary>
    public static void CopyToClipboard(FrameworkElement canvas)
    {
        var rtb = RenderCanvas(canvas);
        System.Windows.Clipboard.SetImage(rtb);
    }

    private static RenderTargetBitmap RenderCanvas(FrameworkElement canvas)
    {
        canvas.Measure(new System.Windows.Size(canvas.ActualWidth, canvas.ActualHeight));
        canvas.Arrange(new Rect(new System.Windows.Size(canvas.ActualWidth, canvas.ActualHeight)));
        canvas.UpdateLayout();

        var source = PresentationSource.FromVisual(canvas);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        var rtb = new RenderTargetBitmap(
            (int)(canvas.ActualWidth  * dpiX),
            (int)(canvas.ActualHeight * dpiY),
            96 * dpiX,
            96 * dpiY,
            PixelFormats.Pbgra32);

        rtb.Render(canvas);
        return rtb;
    }
}
