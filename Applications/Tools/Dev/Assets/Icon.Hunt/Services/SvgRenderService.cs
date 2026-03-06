using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using Path = System.IO.Path;

namespace IconHunt.Services;

public static class SvgRenderService
{
    private static readonly WpfDrawingSettings _settings = new()
    {
        IncludeRuntime = true,
        TextAsGeometry = true
    };

    // SVG 파일 → DrawingImage (WPF Image.Source에 사용)
    public static DrawingImage? RenderFile(string filePath)
    {
        try
        {
            var reader = new FileSvgReader(_settings);
            var drawing = reader.Read(filePath);
            if (drawing == null) return null;
            var img = new DrawingImage(drawing);
            img.Freeze(); // 백그라운드 스레드에서 생성 후 UI 스레드 전달을 위해 필수
            return img;
        }
        catch { return null; }
    }

    // SVG 문자열 → DrawingImage (임시 파일 경유)
    public static DrawingImage? RenderString(string svgContent)
    {
        try
        {
            var tmp = Path.GetTempFileName() + ".svg";
            File.WriteAllText(tmp, svgContent, System.Text.Encoding.UTF8);
            try
            {
                return RenderFile(tmp);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
        catch { return null; }
    }

    // SVG 문자열 → BitmapSource (PNG 썸네일용)
    public static BitmapSource? RenderToBitmap(string svgContent, int width = 48, int height = 48)
    {
        try
        {
            var img = RenderString(svgContent);
            if (img == null) return null;

            var drawingVisual = new DrawingVisual();
            using var ctx = drawingVisual.RenderOpen();
            ctx.DrawImage(img, new System.Windows.Rect(0, 0, width, height));

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }
        catch { return null; }
    }

    // SVG → PNG 파일로 저장
    public static bool SaveAsPng(string svgContent, string outputPath, int size)
    {
        try
        {
            var bitmap = RenderToBitmap(svgContent, size, size);
            if (bitmap == null) return false;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.OpenWrite(outputPath);
            encoder.Save(stream);
            return true;
        }
        catch { return false; }
    }
}
