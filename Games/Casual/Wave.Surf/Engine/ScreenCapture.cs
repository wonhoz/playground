using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WaveSurf.Engine;

/// <summary>묘기 성공 시 PNG 자동 캡처 저장</summary>
public static class ScreenCapture
{
    private static readonly string SaveFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "WaveSurf", "Captures");

    public static string? Capture(Visual target, int width, int height)
    {
        try
        {
            Directory.CreateDirectory(SaveFolder);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(target);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            string fileName = $"wave_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string path = Path.Combine(SaveFolder, fileName);

            using var stream = File.Create(path);
            encoder.Save(stream);

            return path;
        }
        catch
        {
            return null;
        }
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(SaveFolder);
        System.Diagnostics.Process.Start("explorer.exe", SaveFolder);
    }
}
