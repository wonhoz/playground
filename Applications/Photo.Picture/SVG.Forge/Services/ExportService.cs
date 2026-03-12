using System.Windows.Media.Imaging;

namespace SVG.Forge.Services;

public static class ExportService
{
    public static void ExportPng(System.Windows.UIElement element, double width, double height,
        string path, double dpi = 96)
    {
        var scale = dpi / 96.0;
        var rtb = new RenderTargetBitmap(
            (int)(width * scale), (int)(height * scale),
            dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
