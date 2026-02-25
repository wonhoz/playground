namespace WordCloud.Services;

public static class ExportService
{
    public static void SavePng(SKBitmap bitmap)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "PNG로 저장",
            Filter     = "PNG 이미지|*.png",
            DefaultExt = "png",
            FileName   = $"wordcloud_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        using var stream = File.OpenWrite(dlg.FileName);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
    }

    public static void SaveJpeg(SKBitmap bitmap)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "JPEG로 저장",
            Filter     = "JPEG 이미지|*.jpg;*.jpeg",
            DefaultExt = "jpg",
            FileName   = $"wordcloud_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        using var stream = File.OpenWrite(dlg.FileName);
        bitmap.Encode(stream, SKEncodedImageFormat.Jpeg, 95);
    }

    public static void CopyToClipboard(SKBitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;

        var decoder = BitmapDecoder.Create(ms,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();

        System.Windows.Clipboard.SetImage(frame);
    }
}
